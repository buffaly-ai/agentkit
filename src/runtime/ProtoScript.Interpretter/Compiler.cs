using BasicUtilities;
using BasicUtilities.Collections;
using Ontology;
using Ontology.Simulation;
using ProtoScript.Diagnostics;
using ProtoScript.Interpretter.Compiled;
using ProtoScript.Interpretter.Compiling;
using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;
using ProtoScript.Parsers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using TypeInfo = ProtoScript.Interpretter.RuntimeInfo.TypeInfo;

namespace ProtoScript.Interpretter
{
	public class CompilerDiagnostic
	{
		public Statement? Statement;
		public Expression? Expression;
		public Diagnostic Diagnostic;

		public override string ToString()
		{
			return $"CompilerDiagnostic: {Diagnostic} at {Statement?.Info?.ToString() ?? "unknown"}";
		}
	}
	public class Compiler
	{
		public enum CompilationMode
		{
			Strict,
			BestEffort
		}

		public SymbolTable Symbols = new SymbolTable();
		public Map<string, object> References = new Map<string, object>();

		public List<CompilerDiagnostic> Diagnostics = new List<CompilerDiagnostic>();
		public List<string> DisabledFiles = new List<string>();
		public string Source = string.Empty;
		public List<File> Files = new List<File>();
		public bool AllowParallelism = false;
		public CompilationMode ProjectCompilationMode { get; set; } = CompilationMode.Strict;
		private static readonly Dictionary<string, Assembly> s_assemblyPathCache =
			new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
		private static readonly object s_assemblyPathCacheLock = new object();
		private static readonly ConcurrentDictionary<string, object> s_shadowCopyLocks =
			new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, ReferenceAssemblyInfo> _referenceAssemblyInfos =
			new Dictionary<string, ReferenceAssemblyInfo>(StringComparer.OrdinalIgnoreCase);

		public void AddDiagnostic(Diagnostic diagnostic, Statement? statement, Expression? expression)
		{
			Diagnostics.Add(new CompilerDiagnostic() { Diagnostic = diagnostic, Statement = statement, Expression = expression });
		}

		public void AddDiagnostic(string strMessage, Statement? statement, Expression? expression)
		{
			Diagnostics.Add(new CompilerDiagnostic() { Diagnostic = new Diagnostic(strMessage), Statement = statement, Expression = expression });
		}

		private void RegisterBestEffortFileSkip(string filePath, string explanation)
		{
			if (ProjectCompilationMode != CompilationMode.BestEffort)
				return;

			if (string.IsNullOrWhiteSpace(filePath))
				return;

			if (!DisabledFiles.Any(x => StringUtil.EqualNoCase(x, filePath)))
			{
				DisabledFiles.Add(filePath);
			}

			this.AddDiagnostic($"Best-effort: skipped file after failure during include parse: {filePath}. {explanation}", null, null);
		}

		private TypeInfo ResolveTypeInfo(ProtoScript.Type type, Statement? statement, Expression? expression)
		{
			if (null == type)
			{
				this.AddDiagnostic(new Diagnostic("Type is missing"), statement, expression);
				return null;
			}

			TypeInfo baseTypeInfo = Symbols.GetTypeInfo(type.TypeName);
			if (null == baseTypeInfo)
			{
				this.AddDiagnostic(new UnknownType(type.TypeName), statement, expression);
				return null;
			}

			DotNetTypeInfo dotNetTypeInfo = baseTypeInfo as DotNetTypeInfo;
			if (null != dotNetTypeInfo && dotNetTypeInfo.Type.IsGenericTypeDefinition)
			{
				int expected = dotNetTypeInfo.Type.GetGenericArguments().Length;
				int provided = type.ElementTypes.Count;
				if (expected != provided)
				{
					string example = BuildGenericTypeExample(type.TypeName, expected);
					this.AddDiagnostic(new Diagnostic($"Generic type {type.TypeName} expects {expected} type argument(s), but {provided} were provided. Example: {example}"), statement, expression);
					return null;
				}
			}

			try
			{
				return Symbols.GetTypeInfo(type);
			}
			catch (Exception ex)
			{
				this.AddDiagnostic(new Diagnostic($"Failed to resolve type {type}: {ex.Message}"), statement, expression);
				return null;
			}
		}

		private string BuildGenericTypeExample(string typeName, int count)
		{
			List<string> args = new List<string>(count);
			for (int i = 0; i < count; i++)
			{
				args.Add(count == 1 ? "T" : "T" + (i + 1));
			}

			return typeName + "<" + string.Join(", ", args) + ">";
		}
		public void Initialize()
		{
			this.Symbols.EnterGlobalScope();

			//Base types
			this.Symbols.InsertSymbol("bool", new TypeInfo(typeof(bool)));
			this.Symbols.InsertSymbol("string", new TypeInfo(typeof(string)));
			this.Symbols.InsertSymbol("int", new TypeInfo(typeof(int)));
			this.Symbols.InsertSymbol("StringRef", new TypeInfo(typeof(StringReference)));
			this.Symbols.InsertSymbol("stringref", new TypeInfo(typeof(StringReference)));
			this.Symbols.InsertSymbol("Function", new TypeInfo(typeof(FunctionRuntimeInfo)));

			//Default imports
			string strCode = @"
reference Ontology Ontology; 
reference Ontology.Simulation Ontology.Simulation;

import Ontology Ontology.Collection Collection;
import Ontology Ontology.Prototype Prototype;

import Ontology.Simulation Ontology.Simulation.StringWrapper String;
import Ontology.Simulation Ontology.Simulation.IntWrapper Int;
import Ontology.Simulation Ontology.Simulation.IntWrapper Integer;
import Ontology.Simulation Ontology.Simulation.DoubleWrapper Double;
import Ontology.Simulation Ontology.Simulation.BoolWrapper Bool;
import Ontology.Simulation Ontology.Simulation.BoolWrapper Boolean;

";
			File file = ProtoScript.Parsers.Files.ParseFileContents(strCode);
			this.Compile(file);
		}

		public List<Compiled.Statement> CompileProject(string strProjectFile)
		{
			Compiler compiler = this;

			File file = ProtoScript.Parsers.Files.Parse(strProjectFile);
			bool bIgnoreIncludeErrors = ProjectCompilationMode == CompilationMode.BestEffort;
			ConcurrentDictionary<string, string> includeParseFailures = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			Logs.DebugLog.CreateTimer("CompileProject.ParseFiles");

			List<File> lstFiles = GetAllIncludedFiles(
				file,
				AllowParallelism,
				bIgnoreIncludeErrors,
				(path, err) =>
				{
					string explanation = string.IsNullOrWhiteSpace(err?.Message)
						? "Included file failed to parse"
						: err.Message;

					includeParseFailures.TryAdd(path, explanation);
				});
			lstFiles.Remove(file);
			lstFiles.Insert(0, file);

			if (bIgnoreIncludeErrors)
			{
				HashSet<string> failedIncludePaths = new HashSet<string>(includeParseFailures.Keys, StringComparer.OrdinalIgnoreCase);
				lstFiles = lstFiles.Where(fileIncluded =>
					!failedIncludePaths.Contains(fileIncluded.Info?.FullName ?? string.Empty)).ToList();
			}

			Logs.DebugLog.WriteTimer("CompileProject.ParseFiles");

			List<Compiled.Statement> statements = CompileFileList(lstFiles);

			if (bIgnoreIncludeErrors)
			{
				foreach (var includeFailure in includeParseFailures)
				{
					compiler.RegisterBestEffortFileSkip(includeFailure.Key, includeFailure.Value);
				}
			}

			return statements;
		}

		//>Compile a single file
		public List<Compiled.Statement> CompileSingleFile(string strFilePath)
		{
			File file = ProtoScript.Parsers.Files.Parse(strFilePath);
			bool bIgnoreIncludeErrors = ProjectCompilationMode == CompilationMode.BestEffort;
			ConcurrentDictionary<string, string> includeParseFailures = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			List<File> lstFiles = new List<File> { file };
			lstFiles.AddRange(GetAllIncludedFiles(
				file,
				AllowParallelism,
				bIgnoreIncludeErrors,
				(path, err) =>
				{
					string explanation = string.IsNullOrWhiteSpace(err?.Message)
						? "Included file failed to parse"
						: err.Message;

					includeParseFailures.TryAdd(path, explanation);
				}));

			if (bIgnoreIncludeErrors)
			{
				HashSet<string> failedIncludePaths = new HashSet<string>(includeParseFailures.Keys, StringComparer.OrdinalIgnoreCase);
				lstFiles = lstFiles.Where(fileIncluded =>
					!failedIncludePaths.Contains(fileIncluded.Info?.FullName ?? string.Empty)).ToList();
			}

			List<Compiled.Statement> statements = CompileFileList(lstFiles);

			if (bIgnoreIncludeErrors)
			{
				foreach (var includeFailure in includeParseFailures)
				{
					this.RegisterBestEffortFileSkip(includeFailure.Key, includeFailure.Value);
				}
			}

			return statements;
		}


		protected List<Compiled.Statement> CompileFileList(List<File> lstFiles)
		{
			Logs.DebugLog.CreateTimer("CompileProject.CompileFileList");

			Compiler compiler = this;
			compiler.Files = lstFiles;
			File? currentFile = null;
			string currentStage = "initialization";

			try
			{
				List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();
				List<File> activeFiles = new List<File>(lstFiles);
				DisabledFiles.Clear();

				bool TrySkipFailedFile(File fileCurrent, string explanation)
				{
					if (ProjectCompilationMode != CompilationMode.BestEffort)
						return false;

					string filePath = fileCurrent.Info?.FullName ?? "(unknown)";
					if (!DisabledFiles.Any(x => StringUtil.EqualNoCase(x, filePath)))
						DisabledFiles.Add(filePath);

					activeFiles.Remove(fileCurrent);
					this.AddDiagnostic(
						$"Best-effort: skipped file after failure during {currentStage}: {filePath}. {explanation}",
						null,
						null);
					return true;
				}

				bool TrySkipFailedFileFromException(File fileCurrent, Exception err)
				{
					string explanation = err is ProtoScriptCompilerException exCompiler
						? (string.IsNullOrWhiteSpace(exCompiler.Explanation) ? exCompiler.Message : exCompiler.Explanation)
						: $"{err.GetType().Name}: {err.Message}";
					return TrySkipFailedFile(fileCurrent, explanation);
				}

				bool IsNonBlockingImportOrReferenceDiagnostic(CompilerDiagnostic diagnostic, string stageName)
				{
					if (diagnostic.Statement == null)
						return false;

					if (stageName != "DeclareFilePrototypes")
						return false;

					return diagnostic.Statement is ImportStatement || diagnostic.Statement is ReferenceStatement;
				}

				bool TrySkipFailedFileFromStageDiagnostics(File fileCurrent, int diagnosticsBefore)
				{
					if (ProjectCompilationMode != CompilationMode.BestEffort)
						return false;

					string filePath = fileCurrent.Info?.FullName ?? string.Empty;
					for (int i = diagnosticsBefore; i < this.Diagnostics.Count; i++)
					{
						CompilerDiagnostic diagnostic = this.Diagnostics[i];
						string? diagnosticFilePath = GetDiagnosticFilePath(diagnostic);
						if (string.IsNullOrWhiteSpace(diagnosticFilePath))
							continue;
						if (!StringUtil.EqualNoCase(diagnosticFilePath, filePath))
							continue;

						if (IsNonBlockingImportOrReferenceDiagnostic(diagnostic, currentStage))
							continue;

						string diagnosticMessage = diagnostic.Diagnostic?.Message ?? "Compiler diagnostic generated.";
						TrySkipFailedFile(fileCurrent, "Compiler diagnostic: " + diagnosticMessage);
						return true;
					}

					return false;
				}

				static string? GetDiagnosticFilePath(CompilerDiagnostic diagnostic)
				{
					return diagnostic.Statement?.Info?.File ?? diagnostic.Expression?.Info?.File;
				}

				void RunStage(string stageName, Action<File> action, Func<File, bool>? filter = null)
				{
					currentStage = stageName;
					Logs.DebugLog.CreateTimer("CompileProject." + stageName);
					foreach (File fileCurrent in activeFiles.ToList())
					{
						if (filter != null && !filter(fileCurrent))
							continue;

						currentFile = fileCurrent;
						int diagnosticsBefore = this.Diagnostics.Count;
						try
						{
							action(fileCurrent);
						}
						catch (ProtoScriptCompilerException ex)
						{
							if (TrySkipFailedFileFromException(fileCurrent, ex))
								continue;
							ex.m_strProtoScript = fileCurrent.RawCode;
							ex.File = fileCurrent.Info?.FullName;
							throw;
						}
						catch (Exception err)
						{
							if (TrySkipFailedFileFromException(fileCurrent, err))
								continue;
							throw;
						}

						if (ProjectCompilationMode == CompilationMode.BestEffort)
						{
							TrySkipFailedFileFromStageDiagnostics(fileCurrent, diagnosticsBefore);
						}
					}
					Logs.DebugLog.WriteTimer("CompileProject." + stageName);
				}

				RunStage("Precompiled", fileCurrent => PreCompiler.LoadPrecompiled(fileCurrent.RawCode, this.Symbols), x => x.IsPrecompiled);

				RunStage("DeclareNamespaces", fileCurrent =>
				{
					foreach (NamespaceDefinition ns in fileCurrent.Namespaces)
					{
						lstStatements.AddRange(NamespaceCompiler.DeclarePrototypes(ns, this));
					}
				});

				RunStage("DeclareFilePrototypes", fileCurrent =>
				{
					lstStatements.AddRange(compiler.DeclareFilePrototypes(fileCurrent));
				});

				RunStage("DeclareNamespaces2", fileCurrent =>
				{
					foreach (NamespaceDefinition ns in fileCurrent.Namespaces)
					{
						NamespaceCompiler.Declare(ns, this);
					}
				});

				RunStage("DeclareFileTypeOfs", fileCurrent => compiler.DeclareFileTypeOfs(fileCurrent));
				RunStage("DefinePrototypeFields", fileCurrent => compiler.DefineFilePrototypeFields(fileCurrent));
				RunStage("DeclarePrototypeFunctions", fileCurrent => compiler.DeclareFilePrototypeFunctions(fileCurrent));
				RunStage("DeclareFileFunctions", fileCurrent => lstStatements.AddRange(compiler.DeclareFileFunctions(fileCurrent)));
				RunStage("DeclareExternalVariables", fileCurrent => compiler.DeclareFileExternalVariables(fileCurrent));
				RunStage("CompileFileFunctions", fileCurrent => lstStatements.AddRange(compiler.CompileFileFunctions(fileCurrent)));

				RunStage("DefineNamespaces", fileCurrent =>
				{
					foreach (NamespaceDefinition ns in fileCurrent.Namespaces)
					{
						lstStatements.AddRange(NamespaceCompiler.Define(ns, this));
					}
				});

				RunStage("DefinePrototypes", fileCurrent =>
				{
					lstStatements.AddRange(PrototypeCompiler.DefinePrototypes(fileCurrent, this));
				});

				RunStage("CompileAnnotations", fileCurrent =>
				{
					lstStatements.AddRange(compiler.CompileFileFunctionAnnotations(fileCurrent));
				});

				RunStage("CompileStatements", fileCurrent =>
				{
					lstStatements.AddRange(compiler.CompileFileStatements(fileCurrent));
				});

			Logs.DebugLog.WriteTimer("CompileProject.CompileFileList");

				return lstStatements;
			}
			catch (ProtoScriptCompilerException)
			{
				throw;
			}
			catch (Exception err)
			{
				StatementParsingInfo info = new StatementParsingInfo
				{
					StartingOffset = 0,
					Length = 1,
					File = currentFile?.Info?.FullName
				};

				string explanation = $"Compilation failed during {currentStage}: {err.GetType().Name}: {err.Message}";
				ProtoScriptCompilerException wrapped = new ProtoScriptCompilerException(info, explanation);
				wrapped.File = currentFile?.Info?.FullName ?? string.Empty;
				wrapped.m_strProtoScript = currentFile?.RawCode ?? string.Empty;
				throw wrapped;
			}
		}


		public List<Compiled.Statement> Compile(PrototypeDefinition prototypeDefinition)
		{
			Compiler compiler = this;
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			lstStatements.Add(PrototypeCompiler.DeclarePrototype(prototypeDefinition, compiler));
			PrototypeCompiler.DeclarePrototypeTypeOfs(prototypeDefinition, compiler);
			PrototypeCompiler.DefinePrototypeFields(prototypeDefinition, compiler);
			PrototypeCompiler.DeclarePrototypeFunctions(prototypeDefinition, compiler);
			lstStatements.AddRange(PrototypeCompiler.DefinePrototype(prototypeDefinition, compiler));


			return lstStatements;
		}


		static public List<File> GetAllIncludedFiles(
			File file,
			bool bAllowParallelism,
			bool bIgnoreErrors = false,
			Action<string, Exception>? includeParseFailureHandler = null)
		{
			List<File> lstFiles = new List<File>();

			//Note: testing shows parallelism has no effect on performance.
			if (bAllowParallelism)
			{
				GetIncludedFilesRecursive(file, lstFiles, bIgnoreErrors, includeParseFailureHandler);
			}
			else
			{
				GetIncludedFiles(file, lstFiles, bIgnoreErrors, includeParseFailureHandler);
			}

			return lstFiles;
		}

		///	Recursively gather all included files in parallel.
		static private void GetIncludedFilesRecursive(
			File file,
			List<File> lstFiles,
			bool bIgnoreErrors,
			Action<string, Exception>? includeParseFailureHandler)
		{
			//TODO: Doesn't currently work because the order of defining prototypes still matters.

			ConcurrentDictionary<string, byte> seen = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
			ConcurrentQueue<File> queue = new ConcurrentQueue<File>();
			ConcurrentBag<File> bag = new ConcurrentBag<File>();

			// ── seed ───────────────────────────────────────────────────────────────
			seen.TryAdd(file.Info.FullName, 0);
			queue.Enqueue(file);
			bag.Add(file);

			const int BatchSize = 32;

			// ── breadth-first expansion ────────────────────────────────────────────
			while (!queue.IsEmpty)
			{
				List<File> batch = new List<File>(BatchSize);
				while (batch.Count < BatchSize && queue.TryDequeue(out File f))
					batch.Add(f);

				Parallel.ForEach(batch, fileCurrent =>
				{
					string rootDir = GetFileDirectory(fileCurrent);

					foreach (IncludeStatement inc in fileCurrent.Includes)
					{
						IEnumerable<string> paths =
						inc.FileName.Contains('*')
						? Directory.GetFiles(rootDir,
						inc.FileName,
						inc.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
						: new[] { BuildIncludePath(rootDir, inc.FileName) };

						foreach (string path in paths)
						{
							File? sub = TryParse(path, bIgnoreErrors, inc, fileCurrent, includeParseFailureHandler);
							if (sub != null)
							{
								if (seen.TryAdd(sub.Info.FullName, 0))
								{
									bag.Add(sub);
									queue.Enqueue(sub);
								}
								else
								{
									Logs.DebugLog.WriteEvent("**** WARNING **** File already included", sub.Info.FullName);
								}
							}
							else if (bIgnoreErrors)
							{
								FileInfo infoPlaceholder = new FileInfo(path);
								File placeholder = new File();
								placeholder.Info = infoPlaceholder;
								bag.Add(placeholder);
							}
						}
					}
				});
			}

			lstFiles.AddRange(bag);
		}



		static private void GetIncludedFiles(
			File file,
			List<File> lstFiles,
			bool bIgnoreErrors,
			Action<string, Exception>? includeParseFailureHandler)
		{
			string strRootDir = GetFileDirectory(file);

			foreach (IncludeStatement include in file.Includes)
			{
				if (include.FileName.Contains("*"))
				{
					foreach (string strFile in Directory.GetFiles(strRootDir, include.FileName, include.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
					{
						File? fileSub = TryParse(strFile, bIgnoreErrors, include, file, includeParseFailureHandler);
						if (fileSub != null)
						{
							if (!lstFiles.Any(x => StringUtil.EqualNoCase(x.Info.FullName, fileSub.Info.FullName)))
							{
								lstFiles.Add(fileSub);
								GetIncludedFiles(fileSub, lstFiles, bIgnoreErrors, includeParseFailureHandler);
							}
							else
							{
								//This is just to see if we are wasting time parsing, if so rewrite to check first
								Logs.DebugLog.WriteEvent("**** WARNING **** File already included", fileSub.Info.FullName);
							}
						}
						else if (bIgnoreErrors)
						{
							FileInfo infoPlaceholder = new FileInfo(strFile);
							File placeholder = new File();
							placeholder.Info = infoPlaceholder;
							lstFiles.Add(placeholder);
						}
					}
				}
				else
				{
					string path = BuildIncludePath(strRootDir, include.FileName);
					File? fileSub = TryParse(path, bIgnoreErrors, include, file, includeParseFailureHandler);
					if (fileSub != null)
					{
						if (!lstFiles.Any(x => StringUtil.EqualNoCase(x.Info.FullName, fileSub.Info.FullName)))
						{
							lstFiles.Add(fileSub);
							GetIncludedFiles(fileSub, lstFiles, bIgnoreErrors, includeParseFailureHandler);
						}
						else
						{
							Logs.DebugLog.WriteEvent("**** WARNING **** File already included", fileSub.Info.FullName);
						}
					}
					else if (bIgnoreErrors)
					{
						FileInfo infoPlaceholder = new FileInfo(path);
						File placeholder = new File();
						placeholder.Info = infoPlaceholder;
						lstFiles.Add(placeholder);
					}
				}
			}
		}


		// Resolve a ProtoScript file's directory with platform-native path rules instead of assuming Windows separators.
		static private string GetFileDirectory(File file)
		{
			string fullName = file.Info?.FullName ?? string.Empty;
			string? directory = Path.GetDirectoryName(fullName);
			if (string.IsNullOrWhiteSpace(directory))
				return Directory.GetCurrentDirectory();
			return directory;
		}


		// Compose include paths through System.IO so Unix project files do not resolve under literal Project.pts\ aliases.
		static private string BuildIncludePath(string rootDirectory, string includeFileName)
		{
			if (Path.IsPathRooted(includeFileName))
				return includeFileName;
			return Path.Combine(rootDirectory, includeFileName);
		}




		// Parse one include target and preserve include-site source location for missing-file diagnostics.
		static private ProtoScript.File ? TryParse(
			string strFile,
			bool bIgnoreErrors,
			IncludeStatement? includeStatement = null,
			File? sourceFile = null,
			Action<string, Exception>? includeParseFailureHandler = null)
		{
			try
			{
				if (Parsers.Settings.AllowPrecompiled && System.IO.File.Exists(strFile + ".json"))
				{
					return new File() { Info = new FileInfo(strFile), RawCode = FileUtil.ReadFile(strFile + ".json"), IsPrecompiled = true };
				}

				return ProtoScript.Parsers.Files.Parse(strFile);
			}
			catch (Parsers.Files.FileDoesNotExistException err)
			{
				if (bIgnoreErrors)
				{
					includeParseFailureHandler?.Invoke(strFile, err);
					return null;
				}

				StatementParsingInfo info = includeStatement?.Info ?? new StatementParsingInfo
				{
					StartingOffset = 0,
					Length = 1,
					File = sourceFile?.Info?.FullName ?? strFile
				};

				string includePath = includeStatement?.FileName ?? strFile;
				string explanation = "Included file does not exist: " + strFile + " (from include: " + includePath + ")";
				ProtoScriptCompilerException wrapped = new ProtoScriptCompilerException(info, explanation);
				wrapped.File = info.File ?? string.Empty;
				wrapped.m_strProtoScript = sourceFile?.RawCode ?? string.Empty;
				throw wrapped;
			}
			catch (Exception err)
			{
				if (bIgnoreErrors)
				{
					includeParseFailureHandler?.Invoke(strFile, err);
					return null;
				}

				throw;
			}
		}




		public Compiled.File Compile(ProtoScript.File fileCurrent)
		{
			Compiled.File file = new Compiled.File();
			file.Statements = CompileFileList(new List<File> { fileCurrent });
			return file;
		}

		public List<Compiled.Statement> DeclareFilePrototypes(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			foreach (ReferenceStatement statement in file.References)
			{
				compiler.Compile(statement);
			}

			foreach (ImportStatement statement in file.Imports)
			{
				compiler.Compile(statement);
			}

			foreach (PrototypeDefinition protoDef in file.PrototypeDefinitions)
			{
				Compiled.Statement statement = PrototypeCompiler.DeclarePrototype(protoDef, compiler);
				if (null != statement)
					lstStatements.Add(statement);
			}

			foreach (Compiled.Statement statement in lstStatements)
			{
				statement.Info.File = file.Info?.FullName;
			}



			return lstStatements;
		}

		public MethodEvaluation GetAnnotationMethodEvaluation(AnnotationExpression annotation)
		{
			return annotation.GetAnnotationMethodEvaluation();
		}

		public List<Compiled.Statement> AnnotateFile(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			foreach (PrototypeDefinition protoDef in file.PrototypeDefinitions)
			{
				lstStatements.AddRange(PrototypeCompiler.AnnotatePrototype(protoDef, this));
			}

			return lstStatements;
		}

		public List<Compiled.Statement> DeclareFileTypeOfs(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			foreach (PrototypeDefinition protoDef in file.PrototypeDefinitions)
			{
				PrototypeCompiler.DeclarePrototypeTypeOfs(protoDef, compiler);
			}

			return null;
		}

		public List<Compiled.Statement> DefineFilePrototypeFields(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			foreach (PrototypeDefinition protoDef in file.PrototypeDefinitions)
			{
				lstStatements.AddRange(PrototypeCompiler.DefinePrototypeFields(protoDef, this));
			}

			return lstStatements;
		}


		public List<Compiled.Statement> DeclareFilePrototypeFunctions(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			foreach (PrototypeDefinition protoDef in file.PrototypeDefinitions)
			{
				lstStatements.AddRange(PrototypeCompiler.DeclarePrototypeFunctions(protoDef, compiler));
			}

			return lstStatements;
		}
		public List<Compiled.Statement> DeclareFileFunctions(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			foreach (Statement statement in file.Statements)
			{
				if (statement is FunctionDefinition functionDefinition)
					lstStatements.Add(DeclareFunction(functionDefinition));
			}

			return lstStatements;
		}

		public List<Compiled.Statement> CompileFileFunctions(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();


			foreach (Statement statement in file.Statements)
			{
				if (statement is FunctionDefinition functionDefinition)
					Compile(functionDefinition);
			}

			return lstStatements;
		}

		public void DeclareFileExternalVariables(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			foreach (Statement statement in file.Statements)
			{
				if (statement is not VariableDeclaration variableDeclaration || !variableDeclaration.IsExternal)
					continue;

				TypeInfo typeInfo = ResolveTypeInfo(variableDeclaration.Type, variableDeclaration, variableDeclaration.Type);
				if (null == typeInfo)
					continue;

				if (Symbols.ActiveScope().TryGetSymbol(variableDeclaration.VariableName, out object existing))
				{
					if (existing is VariableRuntimeInfo existingVariableInfo)
					{
						if (!SimpleInterpretter.IsAssignableFrom(existingVariableInfo.Type, typeInfo)
							|| !SimpleInterpretter.IsAssignableFrom(typeInfo, existingVariableInfo.Type))
						{
							this.AddDiagnostic(
								new Diagnostic($"Extern declaration {variableDeclaration.VariableName} conflicts with an existing declaration"),
								variableDeclaration,
								variableDeclaration.Type);
						}
					}
					else
					{
						this.AddDiagnostic(
							new Diagnostic($"Extern declaration {variableDeclaration.VariableName} conflicts with an existing symbol"),
							variableDeclaration,
							variableDeclaration.Type);
					}

					continue;
				}

				VariableRuntimeInfo info = new VariableRuntimeInfo
				{
					Type = typeInfo,
					OriginalType = typeInfo.Clone()
				};
				info.Index = Symbols.LocalStack.Add(info);
				Symbols.InsertSymbol(variableDeclaration.VariableName, info);
			}
		}


		public List<Compiled.Statement> CompileFileFunctionAnnotations(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();


			foreach (Statement statement in file.Statements)
			{
				if ((statement is FunctionDefinition functionDefinition))
					lstStatements.AddRange(CompileFunctionAnnotations(functionDefinition));
			}

			return lstStatements;
		}

		public List<Compiled.Statement> CompileFileStatements(ProtoScript.File file)
		{
			Compiler compiler = this;
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			//N20240929-01 - Use global scope since the local scope is no longer saved per file
			Scope localScope = Symbols.GetGlobalScope();

			try
			{
				foreach (Statement statement in file.Statements)
				{
					if (statement is FunctionDefinition)
					{
						continue;
					}

					Compiled.Statement? compiled = compiler.Compile(statement);
					if (compiled != null)
						lstStatements.Add(compiled);
				}
			}
			finally
			{

			}

			return lstStatements;
		}


		public Compiled.Statement Compile(Statement statement)
		{
			switch (statement)
			{
				case VariableDeclaration v:
					return Compile(v);

				case ExpressionStatement e:
					return Compile(e);

				case ReturnStatement r:
					return Compile(r);

				case IfStatement i:
					return Compile(i);

				case ForEachStatement f:
					return Compile(f);

				case CodeBlockStatement c:
					return Compile(c);

				case DoStatement d:
					return DoCompiler.Compile(d, this);

				case WhileStatement w:
					return WhileCompiler.Compile(w, this);

				case TryStatement t:
					return TryCompiler.Compile(t, this);

				case ThrowStatement th:
					return TryCompiler.Compile(th, this);

				default:
					throw new ProtoScriptCompilerException(
						statement.Info,
						"Unknown statement type");
			}
		}


		public Compiled.Expression? CompileRootIdentifier(string strValue, StatementParsingInfo info)
		{
			if (strValue == "global")
				return new GetGlobalStack() { Index = -1, InferredType = new Namespace() { Scope = Symbols.GetGlobalScope() }, Info = info };

			Scope scope;
			object? obj = Symbols.GetSymbolAndScope(strValue, out scope);

			if (null == obj)
			{
				if (strValue.Contains("<"))
				{
					ProtoScript.Type parsedType = ProtoScript.Parsers.Types.Parse(strValue);
					obj = ResolveTypeInfo(parsedType, null, parsedType);
				}

			}

			if (obj is null)
				return null;

			switch (obj)
			{
				case FieldTypeInfo fieldTypeInfo:

					return new PrototypeFieldReference()
					{
						Left = CompileRootIdentifier("this", info),
						Right = new GetGlobalStack() { Index = fieldTypeInfo.Index, InferredType = fieldTypeInfo.FieldInfo, Info = info },
						InferredType = fieldTypeInfo.FieldInfo,
						FieldInfo = fieldTypeInfo,
						Info = info
					};
				case PrototypeTypeInfo pti:
					return new GetGlobalStack { Index = pti.Index, InferredType = pti, Info = info };

				case ValueRuntimeInfo vri when scope.ScopeType == Scope.ScopeTypes.Method:
					return new GetLocalStack { Index = vri.Index, InferredType = vri.Type, Info = info };

				case ValueRuntimeInfo vri:        // file / block scope
					return new GetStack { Index = vri.Index, InferredType = vri.Type, Scope = scope, Info = info };

				case DotNetTypeInfo dti:
					return new GetGlobalStack { Index = dti.Index, InferredType = dti, Info = info };

				case FunctionRuntimeInfo fri:
					return new GetGlobalStack
					{
						Index = fri.Index,
						InferredType = new TypeInfo(typeof(FunctionRuntimeInfo)),
						Info = info
					};

				case Namespace ns:
					return new GetGlobalStack { Index = ns.Index, InferredType = ns, Info = info };

				// assemblies are ignored by design
				case System.Reflection.Assembly:
					return null;

				default:
					return null;
			}
		}

		public Compiled.ExpressionStatement Compile(ExpressionStatement statement)
		{
			Compiled.Expression? expression = Compile(statement.Expression);
			if (expression == null)
				return null;

			return new Compiled.ExpressionStatement
			{
				Expression = expression,
				Info = statement.Info
			};
		}

		public Compiled.VariableDeclaration Compile(VariableDeclaration statement)
		{
			if (statement.IsExternal && statement.Initializer != null)
			{
				this.AddDiagnostic(new Diagnostic("Extern declarations cannot have initializers"), statement, statement.Initializer);
				return null;
			}

			if (statement.IsExternal && Symbols.ActiveScope().ScopeType != Scope.ScopeTypes.Global && Symbols.ActiveScope().ScopeType != Scope.ScopeTypes.File)
			{
				this.AddDiagnostic(new Diagnostic("Extern declarations are only valid at file scope"), statement, null);
				return null;
			}

			TypeInfo typeInfo = ResolveTypeInfo(statement.Type, statement, statement.Type);
			if (null == typeInfo)
				return null;


			if (Symbols.ActiveScope().TryGetSymbol(statement.VariableName, out object oExisting))
			{
				if (statement.IsExternal && oExisting is VariableRuntimeInfo predeclaredExternal)
				{
					Compiled.VariableDeclaration externalDeclaration = new Compiled.VariableDeclaration();
					externalDeclaration.Info = statement.Info;
					externalDeclaration.RuntimeInfo = predeclaredExternal;
					return externalDeclaration;
				}

				this.AddDiagnostic(new Diagnostic($"{statement.VariableName} already declared in local scope"), statement, null);
				return null;
			}

			Compiled.VariableDeclaration declaration = new Compiled.VariableDeclaration();
			declaration.Info = statement.Info;

			if (typeInfo is PrototypeTypeInfo || typeInfo is DotNetTypeInfo)
			{
				VariableRuntimeInfo info = new VariableRuntimeInfo();

				info.Type = typeInfo;
				info.Index = Symbols.LocalStack.Add(info);
				info.OriginalType = typeInfo.Clone();

				Symbols.InsertSymbol(statement.VariableName, info);

				if (null != statement.Initializer)
				{
					declaration.Initializer = Compile(statement.Initializer);


					//This causes every phrase[0] to fail, so let's limit it to just .net types
					if (typeInfo is DotNetTypeInfo)
					{
						if (declaration.Initializer == null)
						{
							return null;
						}

						if (!SimpleInterpretter.IsAssignableFrom(declaration.Initializer.InferredType, typeInfo))
						{
							this.AddDiagnostic(new CannotConvert(declaration.Initializer.InferredType.ToString(), typeInfo.ToString()), statement, null);
							return null;
						}
					}
				}

				declaration.RuntimeInfo = info;
			}

			else if (typeInfo is TypeInfo)
			{
				VariableRuntimeInfo info = new VariableRuntimeInfo();
				info.Type = typeInfo as TypeInfo;
				info.OriginalType = info.Type.Clone();
				info.Index = Symbols.LocalStack.Add(info);

				Symbols.InsertSymbol(statement.VariableName, info);

				if (null != statement.Initializer)
				{
					declaration.Initializer = Compile(statement.Initializer);

					if (null != declaration.Initializer && !SimpleInterpretter.IsAssignableFrom(declaration.Initializer.InferredType, typeInfo))
					{
						this.AddDiagnostic(new CannotConvert(declaration.Initializer.InferredType.ToString(), typeInfo.ToString()), statement, null);
						return null;
					}
				}

				declaration.RuntimeInfo = info;
			}

			else
				throw new ProtoScriptCompilerException(
					statement.Info,
					$"Unsupported variable declaration type category for '{statement.VariableName}': {typeInfo.GetType().FullName}.");

			return declaration;
		}

		public Compiled.Expression Compile(Expression expression)
		{
			if (null == expression)
				throw new ProtoScriptCompilerException(new StatementParsingInfo(), "Encountered a null expression while compiling.");

			if (null == expression.Terms || expression.Terms.Count == 0)
				return CompileTerm(expression);

			if (expression.Terms.Count > 1)
				throw new ProtoScriptCompilerException(expression.Info, $"Expression contained {expression.Terms.Count} top-level terms when exactly 1 was expected.");

			foreach (Expression term in expression.Terms)
			{
				return CompileTerm(term);
			}

			throw new ProtoScriptCompilerException(
				expression.Info,
				"Failed to compile expression: no term produced a compiled result.");
		}

		public Compiled.Expression CompileTerm(Expression term)
		{
			if (term is BinaryOperator)
				return Compile(term as BinaryOperator);

			if (term is UnaryOperator)
				return Compile(term as UnaryOperator);

			if (term is Identifier)
				return Compile(term as Identifier);

			if (term is NewObjectExpression)
				return Compile(term as NewObjectExpression);

			if (term is MethodEvaluation)
				return Compile(term as MethodEvaluation);

			if (term is Literal)
				return Compile(term as Literal);

			if (term is ExpressionList)
				return Compile(term as ExpressionList);

			if (term is CategorizationOperator)
				return Compile(term as CategorizationOperator);

			if (term is Expression && term.Terms.Count > 0)
				return Compile(term as Expression);

			throw new ProtoScriptCompilerException(term.Info, "Unsupported expression term");
		}

		public Compiled.Expression Compile(CategorizationOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledMiddle = Compile(op.Middle);
			GetGlobalStack identifier = compiledMiddle as GetGlobalStack;
			if (null == identifier)
			{
				this.AddDiagnostic(new Diagnostic("Categorization must be on a Prototype"), null, op);
				return null;
			}

			if (!(identifier.InferredType is PrototypeTypeInfo))
			{
				if (!ReflectionUtil.HasBaseType(identifier.InferredType.Type, typeof(Prototype)))
				{
					this.AddDiagnostic(new Diagnostic("Categorization must be on a Prototype"), null, op);
					return null;
				}
			}

			Compiled.ScopedExpressionList compiledRight = null;
			Scope scope = new Scope(Scope.ScopeTypes.Block);
			Symbols.EnterScope(scope);

			try
			{
				TypeInfo infoThis = identifier.InferredType;
				ValueRuntimeInfo infoThisInstance = new ValueRuntimeInfo();
				infoThisInstance.Index = scope.Stack.Add(infoThisInstance);
				infoThisInstance.OriginalType = infoThis.Clone();
				infoThisInstance.Type = infoThis.Clone();
				scope.InsertSymbol("this", infoThisInstance);

				compiledRight = Compile(op.Right);
				compiledRight.Scope = scope;

			}
			finally
			{
				Symbols.LeaveScope();
			}

			return new Compiled.CategorizationOperator
			{
				Left = compiledLeft,
				Middle = compiledMiddle,
				Right = compiledRight,
				InferredType = new TypeInfo(typeof(bool)),
				Info = op.Info
			};
		}

		public Compiled.ScopedExpressionList Compile(ScopedExpressionList expr)
		{
			Compiled.ScopedExpressionList lst = new Compiled.ScopedExpressionList();
			lst.Info = expr.Info;
			lst.InferredType = new TypeInfo(typeof(bool));

			foreach (Expression expression in expr.Expressions)
			{
				lst.Expressions.Add(Compile(expression));
			}

			return lst;
		}

		public Compiled.Expression Compile(ExpressionList exp)
		{
			if (exp.Expressions.Count != 1)
				throw new ProtoScriptCompilerException(
					exp.Info,
					$"Expression list must contain exactly one expression, but found {exp.Expressions.Count}.");

			return Compile(exp.Expressions.First());
		}

		public Compiled.Expression Compile(BinaryOperator exp)
		{
			if (exp.Value == "=")
			{
				return CompileAssignmentOperator(exp);
			}

			if (exp.Value == "typeof")
			{
				return CompileTypeOfOperator(exp);
			}

			if (exp is IndexOperator)
			{
				return Compile(exp as IndexOperator);
			}

			if (exp.Value == "=>")
			{
				return CompileLambda(exp);
			}

			if (exp.Value == "==")
			{
				return CompileEquals(exp);
			}

			if (exp.Value == "!=")
			{
				return CompileNotEquals(exp);
			}

			if (exp.Value == "as")
				return CompileCastingOperator(exp);

			if (exp.Value == "cast")
				return CompileCastingOperator2(exp);

			if (exp.Value == "||")
				return CompileOrOperator(exp);

			if (exp.Value == "&&")
				return CompileAndOperator(exp);

			if (exp.Value == "??")
				return CompileNullCoalescingOperator(exp);

			if (exp.Value == "?")
				return CompileConditionalOperator(exp);

			if (exp.Value == "." || exp.Value == "?.")
				return CompileDotOperator(exp);

			if (exp.Value == "+")
				return CompileAddOperator(exp);

			if (exp.Value == ">" || exp.Value == "<" || exp.Value == ">=" || exp.Value == "<=")
				return CompileComparisonOperator(exp);

			if (exp.Value == "+=")
				return CompileAddAssignmentOperator(exp);

			throw new ProtoScriptCompilerException(exp.Info, $"Unsupported operator encountered: '{exp.Value}'");
		}

		public NewInstance.ObjectInitializer Compile(NewObjectExpression.ObjectInitializer initializer, Prototype prototype)
		{
			var tuple = SimpleInterpretter.ResolveProperty(prototype, initializer.Name);
			if (null == tuple || null == tuple.Item1 || null == tuple.Item2)
			{
				this.AddDiagnostic(new Diagnostic("Could not find property field: " + initializer.Name), null, initializer);
				return null;
			}

			Compiled.Expression expr = Compile(initializer.Value);

			return new NewInstance.ObjectInitializer()
			{
				Property = tuple.Item1,
				Value = expr
			};

		}

		public Compiled.Expression CompileDotOperator(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			if (null == compiledLeft)
			{
				if (op.Left is Identifier leftIdentifier && op.Right is MethodEvaluation rightMethod && IsPrimitiveTypeAlias(leftIdentifier.Value))
				{
					this.AddDiagnostic(new Diagnostic($"Cannot resolve '{leftIdentifier.Value}.{rightMethod.MethodName}'. Primitive type aliases are not callable static .NET types in this context."), null, op);
				}

				return null;
			}

			if (op.Right is Identifier)
			{
				Identifier identifier = op.Right as Identifier;
				string strPropertyName = identifier.Value;

				if (compiledLeft.InferredType is PrototypeTypeInfo)
				{
					PrototypeTypeInfo prototypeTypeInfo = compiledLeft.InferredType as PrototypeTypeInfo;
					TypeInfo infoType = prototypeTypeInfo.Scope.GetSymbol(strPropertyName) as TypeInfo;

					//Don't use "is" here we only want the base type
					if (null != infoType && infoType.GetType() == typeof(PrototypeTypeInfo))
					{
						return new GetGlobalStack() { Index = infoType.Index, InferredType = infoType };
					}

					FieldTypeInfo infoField = GetFieldInfo(prototypeTypeInfo, strPropertyName);
					if (null != infoField)
					{
						PrototypeFieldReference res = new PrototypeFieldReference()
						{
							Left = compiledLeft,
							Right = new GetGlobalStack() { Index = infoField.Index, InferredType = infoField.FieldInfo, Info = op.Right.Info },
							InferredType = infoField.FieldInfo,
							FieldInfo = infoField,
							Info = op.Info
						};
						res.IsNullConditional = op.Value == "?.";
						return res;
					}


					//Try resolving as a method 
					//{
					//	var tuple2 = SimpleInterpretter.ResolveMethod(prototypeTypeInfo.Prototype, strPropertyName);

					//	if (null != tuple2)
					//	{
					//		Prototype protoProp = tuple2.Item1;
					//		Prototype protoPrototype = tuple2.Item2;

					//		if (!(protoProp.PrototypeName != "ProtoScript.Interpretter.RuntimeInfo.FunctionRuntimeInfo"))
					//			throw new Exception("Unexpected");

					//		PrototypeTypeInfo typeInfoParent = Symbols.GetGlobalScope().GetSymbol(protoPrototype.PrototypeName) as PrototypeTypeInfo;
					//		FunctionRuntimeInfo functionRuntimeInfo = typeInfoParent.Scope.GetSymbol(strPropertyName) as FunctionRuntimeInfo;

					//		return new GetStack() { Scope = typeInfoParent.Scope, Index = functionRuntimeInfo.Index, InferredType = new TypeInfo(typeof(FunctionRuntimeInfo)) };
					//	}
					//}

					{
						FunctionRuntimeInfo functionRuntimeInfo = MethodCompiler.ResolveMethod2(prototypeTypeInfo.Prototype, strPropertyName, this.Symbols);

						if (null != functionRuntimeInfo)
						{
							PrototypeTypeInfo typeInfoParent = Symbols.GetGlobalScope().GetSymbol(functionRuntimeInfo.ParentPrototype.PrototypeName) as PrototypeTypeInfo;

							return new GetStack() { Scope = typeInfoParent.Scope, Index = functionRuntimeInfo.Index, InferredType = new TypeInfo(typeof(FunctionRuntimeInfo)) };
						}
					}
				}

				else if (compiledLeft.InferredType is Namespace)
				{
					Namespace ns = compiledLeft.InferredType as Namespace;
					TypeInfo infoType = ns.Scope.GetSymbol(strPropertyName) as TypeInfo;
					if (null == infoType)
					{
						this.AddDiagnostic($"Cannot find {strPropertyName} in namespace", null, op);
					}


					if (infoType is PrototypeTypeInfo || infoType is Namespace)
					{
						return new GetGlobalStack() { Index = infoType.Index, InferredType = infoType };
					}
					else
					{
						this.AddDiagnostic("Can only reference prototype or namespace within a namespace", null, op);
						return null;
					}
				}

				Compiled.Expression objCur = compiledLeft;
				//Try as a .NET property 
				{
					Compiled.Expression? memberInfo = GetDotNetMemberReference(op.Info, strPropertyName, objCur);
					if (null != memberInfo)
					{
						if (memberInfo is DotNetFieldReference df)
							df.IsNullConditional = op.Value == "?.";
						else if (memberInfo is DotNetPropertyReference dp)
							dp.IsNullConditional = op.Value == "?.";
						return memberInfo;
					}
				}

				this.AddDiagnostic(new Diagnostic($"Could not find property {strPropertyName}"), null, op);
				return null;
			}
			else if (op.Right is MethodEvaluation)
			{
				Compiled.Expression compiledRight = CompileMethodEvaluationInternal(op.Right as MethodEvaluation, compiledLeft);
				if (compiledRight is DotNetMethodEvaluation dme)
					dme.IsNullConditional = op.Value == "?.";
				return compiledRight;
			}
			else if (op.Right is BinaryOperator && (op.Right as BinaryOperator).Value == ".")
			{

			}

			this.AddDiagnostic(new Diagnostic("Could not compile expression"), null, op);
			return null;
		}

		internal static Compiled.Expression? GetDotNetMemberReference(StatementParsingInfo info, string strPropertyName, Compiled.Expression objCur)
		{
			// Some symbols can carry an inferred type shell without a bound CLR type.
			// Treat these as non-.NET members instead of crashing during lookup.
			if (objCur?.InferredType?.Type == null)
				return null;

			System.Reflection.FieldInfo fieldInfo = objCur.InferredType.Type.GetField(strPropertyName);
			if (null != fieldInfo)
			{
				return new DotNetFieldReference()
				{
					Field = fieldInfo,
					Info = info,
					InferredType = new TypeInfo(fieldInfo.FieldType),
					Object = objCur
				};
			}

			System.Reflection.PropertyInfo propertyInfo = objCur.InferredType.Type.GetProperty(strPropertyName);
			if (null != propertyInfo)
			{
				return new DotNetPropertyReference()
				{
					Property = propertyInfo,
					Info = info,
					InferredType = new TypeInfo(propertyInfo.PropertyType),
					Object = objCur
				};
			}

			return null;
		}

		public FieldTypeInfo GetFieldInfo(PrototypeTypeInfo prototypeTypeInfo, string strPropertyName)
		{

			//Check the primary prototype
			{
				PrototypeTypeInfo prototypeFieldInfo = prototypeTypeInfo.Scope.GetSymbol(strPropertyName) as PrototypeTypeInfo;
				if (null != prototypeFieldInfo)
				{
					FieldTypeInfo infoField = null;

					//check first for an initializer locally
					if (prototypeFieldInfo is FieldTypeInfo)
						infoField = prototypeFieldInfo as FieldTypeInfo;
					else
						infoField = Symbols.GetGlobalScope().GetSymbol(prototypeFieldInfo.Prototype.PrototypeName) as FieldTypeInfo;

					return infoField;
				}
			}

			//Check the typeofs (especially for external, which won't have temporary prototype)
			foreach (int protoTypeOf in prototypeTypeInfo.Prototype.GetAllParents())
			{
				PrototypeTypeInfo parentTypeInfo = Symbols.GetGlobalScope().GetSymbol(Prototypes.GetPrototypeName(protoTypeOf)) as PrototypeTypeInfo;

				//Types created via CSharp don't have Scopes (for now, you may want to fix that later)
				if (null != parentTypeInfo)
				{
					if (null == parentTypeInfo.Scope)
					{
						this.AddDiagnostic("No scope setup for prototype: " + parentTypeInfo.Prototype.PrototypeName, null, null);
						return null;
					}

					PrototypeTypeInfo prototypeFieldInfo = parentTypeInfo.Scope.GetSymbol(strPropertyName) as PrototypeTypeInfo;
					if (null != prototypeFieldInfo)
					{
						if (prototypeFieldInfo is FieldTypeInfo)
							return (FieldTypeInfo)prototypeFieldInfo;

						FieldTypeInfo infoField = Symbols.GetGlobalScope().GetSymbol(prototypeFieldInfo.Prototype.PrototypeName) as FieldTypeInfo;
						return infoField;
					}
				}
			}

			var tuple = SimpleInterpretter.ResolveProperty(prototypeTypeInfo.Prototype, strPropertyName);
			if (null != tuple)
			{
				Prototype protoProp = tuple.Item1;

				FieldTypeInfo fieldTypeInfo = Symbols.GetGlobalScope().GetSymbol(protoProp.PrototypeName) as FieldTypeInfo;
				if (null != fieldTypeInfo)
					return fieldTypeInfo;
			}

			return null;
		}

		private static bool IsSupportedStringConcatenationOperand(TypeInfo? typeInfo)
		{
			if (typeInfo == null)
				return false;

			if (SimpleInterpretter.IsAssignableFrom(typeInfo, new TypeInfo(typeof(string))))
				return true;

			if (SimpleInterpretter.IsAssignableFrom(typeInfo, new TypeInfo(typeof(int))))
				return true;

			if (SimpleInterpretter.IsAssignableFrom(typeInfo, new TypeInfo(typeof(bool))))
				return true;

			return false;
		}

		public Compiled.Expression CompileAddOperator(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledRight = Compile(op.Right);
			if (compiledLeft == null || compiledRight == null)
			{
				this.AddDiagnostic(new Diagnostic("Could not compile one side of string concatenation"), null, op);
				return null;
			}

			bool isIntegerAddition =
				SimpleInterpretter.IsAssignableFrom(compiledLeft.InferredType, new TypeInfo(typeof(int))) &&
				SimpleInterpretter.IsAssignableFrom(compiledRight.InferredType, new TypeInfo(typeof(int)));

			if (!isIntegerAddition && !IsSupportedStringConcatenationOperand(compiledLeft.InferredType))
			{
				this.AddDiagnostic(new Diagnostic("Only integer addition and string concatenation are supported"), null, op);
				return null;
			}

			if (!isIntegerAddition && !IsSupportedStringConcatenationOperand(compiledRight.InferredType))
			{
				this.AddDiagnostic(new Diagnostic("Only integer addition and string concatenation are supported"), null, op);
				return null;
			}

			return new Compiled.AddOperator
			{
				Left = compiledLeft,
				Right = compiledRight,
				InferredType = new TypeInfo(isIntegerAddition ? typeof(int) : typeof(string)),
			};
		}
		public Compiled.Expression CompileAndOperator(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledRight = Compile(op.Right);

			return new Compiled.AndOperator
			{
				Left = compiledLeft,
				Right = compiledRight,
				InferredType = new TypeInfo(typeof(bool)),
			};
		}
		public Compiled.Expression CompileOrOperator(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledRight = Compile(op.Right);

			return new Compiled.OrOperator
			{
				Left = compiledLeft,
				Right = compiledRight,
				InferredType = new TypeInfo(typeof(bool))
			};
		}

		public Compiled.Expression CompileComparisonOperator(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledRight = Compile(op.Right);
			if (compiledLeft == null || compiledRight == null)
			{
				this.AddDiagnostic(new Diagnostic("Could not compile one side of comparison"), null, op);
				return null;
			}

			if (compiledLeft.InferredType == null || compiledRight.InferredType == null)
			{
				this.AddDiagnostic(
					new Diagnostic(
						$"Comparison operator '{op.Value}' requires typed operands, but got left={DescribeType(compiledLeft.InferredType)} right={DescribeType(compiledRight.InferredType)}"),
					null,
					op);
				return null;
			}

			if (!SimpleInterpretter.IsAssignableFrom(compiledLeft.InferredType, new TypeInfo(typeof(int))))
			{
				this.AddDiagnostic(
					new Diagnostic(
						$"Only integer comparisons supported for operator '{op.Value}', but left operand type is {DescribeType(compiledLeft.InferredType)}"),
					null,
					op);
				return null;
			}

			if (!SimpleInterpretter.IsAssignableFrom(compiledRight.InferredType, new TypeInfo(typeof(int))))
			{
				this.AddDiagnostic(
					new Diagnostic(
						$"Only integer comparisons supported for operator '{op.Value}', but right operand type is {DescribeType(compiledRight.InferredType)}"),
					null,
					op);
				return null;
			}

			return new Compiled.ComparisonOperator
			{
				Left = compiledLeft,
				Right = compiledRight,
				Operator = op.Value,
				InferredType = new TypeInfo(typeof(bool)),
				Info = op.Info
			};
		}

		private static string DescribeType(TypeInfo? typeInfo)
		{
			if (typeInfo == null)
				return "(null)";

			if (typeInfo.Type != null)
				return typeInfo.Type.Name;

			return typeInfo.ToString();
		}

		public Compiled.Expression CompileNullCoalescingOperator(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledRight = Compile(op.Right);

			if (compiledLeft == null || compiledRight == null)
			{
				this.AddDiagnostic(new Diagnostic("Could not compile null-coalescing operator"), null, op);
				return null;
			}

			TypeInfo inferredType = InferConditionalOperatorType(compiledLeft.InferredType, compiledRight.InferredType, op);

			return new Compiled.NullCoalescingOperator
			{
				Left = compiledLeft,
				Right = compiledRight,
				InferredType = inferredType,
				Info = op.Info
			};
		}

		public Compiled.Expression CompileConditionalOperator(BinaryOperator op)
		{
			if (!(op.Right is BinaryOperator) || (op.Right as BinaryOperator).Value != ":")
			{
				this.AddDiagnostic(new Diagnostic("Malformed ternary operator"), null, op);
				return null;
			}

			BinaryOperator opColon = op.Right as BinaryOperator;

			Compiled.Expression compiledCondition = Compile(op.Left);
			Compiled.Expression compiledWhenTrue = Compile(opColon.Left);
			Compiled.Expression compiledWhenFalse = Compile(opColon.Right);

			if (compiledCondition == null || compiledWhenTrue == null || compiledWhenFalse == null)
			{
				this.AddDiagnostic(new Diagnostic("Could not compile ternary operator"), null, op);
				return null;
			}

			TypeInfo inferredType = InferConditionalOperatorType(compiledWhenTrue.InferredType, compiledWhenFalse.InferredType, op);

			return new Compiled.ConditionalOperator
			{
				Condition = compiledCondition,
				TrueExpression = compiledWhenTrue,
				FalseExpression = compiledWhenFalse,
				InferredType = inferredType,
				Info = op.Info
			};
		}

		private TypeInfo InferConditionalOperatorType(TypeInfo typeWhenTrue, TypeInfo typeWhenFalse, BinaryOperator op)
		{
			if (typeWhenTrue == null)
				return typeWhenFalse;

			if (typeWhenFalse == null)
				return typeWhenTrue;

			if (SimpleInterpretter.IsAssignableFrom(typeWhenTrue, typeWhenFalse))
				return typeWhenFalse;

			if (SimpleInterpretter.IsAssignableFrom(typeWhenFalse, typeWhenTrue))
				return typeWhenTrue;

			this.AddDiagnostic(new Diagnostic($"Ternary branches are incompatible: {typeWhenTrue} and {typeWhenFalse}"), null, op);
			return typeWhenTrue;
		}


		public Compiled.Expression CompileCastingOperator(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);

			//The right side must be an Identifier and a Type. We can't resolve it normally 
			//or it won't go to global stack to get the correct symbols
			string strType = null;
			if (op.Right is Identifier)
				strType = (op.Right as Identifier).Value;

			else if (op.Right is PrototypeStringLiteral)
			{
				GetGlobalStack op2 = Compile(op.Right as PrototypeStringLiteral) as GetGlobalStack;
				if (null == op2)
				{
					this.AddDiagnostic(new Diagnostic("Cannot compile target of casting operator"), null, op.Right);
					return null;
				}

				return new Compiled.CastingOperator
				{
					Left = compiledLeft,
					Right = op2,
					InferredType = op2.InferredType
				};
			}

			else if (op.Right is BinaryOperator && (op.Right as BinaryOperator).Value == ".")
			{
				GetGlobalStack op2 = Compile(op.Right) as GetGlobalStack;
				if (null == op2)
				{
					this.AddDiagnostic(new Diagnostic("Cannot compile target of casting operator"), null, op.Right);
					return null;
				}

				return new Compiled.CastingOperator
				{
					Left = compiledLeft,
					Right = op2,
					InferredType = op2.InferredType
				};
			}

			else
			{
				this.AddDiagnostic(new Diagnostic("Cannot compile target of casting operator"), null, op);
				return null;
			}

			TypeInfo typeInfo = Symbols.GetTypeInfo(strType);
			if (null == typeInfo)
			{
				this.AddDiagnostic(new UnknownType(strType), null, op);
				return null;
			}

			return new Compiled.CastingOperator
			{
				Left = compiledLeft,
				Right = new GetGlobalStack() { Index = typeInfo.Index, InferredType = typeInfo },
				InferredType = typeInfo
			};
		}

		public Compiled.Expression CompileCastingOperator2(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);

			//The right side must be an Identifier and a Type. We can't resolve it normally 
			//or it won't go to global stack to get the correct symbols
			string strType = null;
			if (op.Right is Identifier)
				strType = (op.Right as Identifier).Value;

			else if (op.Right is PrototypeStringLiteral)
			{
				GetGlobalStack op2 = Compile(op.Right as PrototypeStringLiteral) as GetGlobalStack;
				return new Compiled.CastingOperator2
				{
					Left = compiledLeft,
					Right = op2,
					InferredType = op2.InferredType
				};
			}

			else
			{
				this.AddDiagnostic(new Diagnostic("Cannot compile target of casting operator"), null, op);
				return null;
			}

			TypeInfo typeInfo = Symbols.GetTypeInfo(strType);
			if (null == typeInfo)
			{
				this.AddDiagnostic(new UnknownType(strType), null, op);
				return null;
			}

			return new Compiled.CastingOperator2
			{
				Left = compiledLeft,
				Right = new GetGlobalStack() { Index = typeInfo.Index, InferredType = typeInfo },
				InferredType = typeInfo
			};
		}

		public Compiled.Expression CompileEquals(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledRight = Compile(op.Right);

			return new Compiled.EqualsOperator
			{
				Left = compiledLeft,
				Right = compiledRight,
				InferredType = new TypeInfo(typeof(bool))
			};
		}

		public Compiled.Expression CompileNotEquals(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledRight = Compile(op.Right);

			return new Compiled.NotOperator
			{
				Right =
					new Compiled.EqualsOperator
					{
						Left = compiledLeft,
						Right = compiledRight,
						InferredType = new TypeInfo(typeof(bool))
					}
			};
		}

		public Compiled.Expression CompileLambda(BinaryOperator exp)
		{
			Compiled.LambdaOperator op = new LambdaOperator();
			FunctionRuntimeInfo funcInfo = new FunctionRuntimeInfo();
			op.Function = funcInfo;

			funcInfo.Scope = new Scope(Scope.ScopeTypes.Lambda);
			Symbols.EnterScope(funcInfo.Scope);

			try
			{
				ParameterRuntimeInfo infoParam = new ParameterRuntimeInfo();
				infoParam.Type = new TypeInfo(typeof(Prototype));
				infoParam.OriginalType = new TypeInfo(typeof(Prototype));
				infoParam.Index = funcInfo.Scope.Stack.Add(infoParam);

				funcInfo.Parameters.Add(infoParam);
				funcInfo.Scope.InsertSymbol((exp.Left as Identifier).Value, infoParam);

				Compiled.Expression compiledExpression = Compile(exp.Right);
				Compiled.ExpressionStatement compiledStatement = new Compiled.ExpressionStatement();
				compiledStatement.Expression = compiledExpression;

				funcInfo.Statements.Add(compiledStatement);
			}
			finally
			{
				Symbols.LeaveScope();
			}

			return op;
		}

		public Compiled.Expression Compile(UnaryOperator exp)
		{
			if (exp.Value == "!")
			{
				return CompileNotOperator(exp);
			}

			if (exp.Value == "+" || exp.Value == "-")
			{
				Compiled.Expression compiledRight = Compile(exp.Right);
				if (compiledRight is Compiled.Literal literal)
				{
					if (exp.Value == "+")
					{
						return literal;
					}

					if (literal.Value is int intValue)
					{
						return new Compiled.Literal
						{
							Value = -intValue,
							InferredType = new TypeInfo(typeof(int)),
							Info = exp.Info
						};
					}

					if (literal.Value is double doubleValue)
					{
						return new Compiled.Literal
						{
							Value = -doubleValue,
							InferredType = new TypeInfo(typeof(double)),
							Info = exp.Info
						};
					}
				}

				throw new ProtoScriptCompilerException(exp.Info, "Unary +/- currently supports numeric literals only");
			}

			else if (exp is IsInitializedOperator)
			{
				return Compile(exp as IsInitializedOperator);
			}

			throw new ProtoScriptCompilerException(exp.Info, "Unsupported unary operator");
		}
		public Compiled.Expression Compile(IsInitializedOperator exp)
		{
			Compiled.IsInitializedOperator op = new Compiled.IsInitializedOperator();
			Compiled.Expression compiledRight = Compile(exp.Right);
			if (!(compiledRight is PrototypeFieldReference))
			{
				this.AddDiagnostic(new Diagnostic("Expected a prototype field reference"), null, exp);
				return null;
			}

			PrototypeFieldReference reference = compiledRight as PrototypeFieldReference;
			reference.AllowLazyInitializaton = false;

			op.Right = reference;

			return op;
		}

		public Compiled.Expression Compile(IndexOperator exp)
		{
			Compiled.Expression compiledLeft = Compile(exp.Left);
			Compiled.Expression compiledRight = Compile(exp.Right);

			return new Compiled.IndexOperator
			{
				Left = compiledLeft,
				Right = compiledRight,
				InferredType = new TypeInfo(typeof(Prototype)),
				Info = exp.Info
			};
		}

		public Compiled.Expression CompileAssignmentOperator(BinaryOperator exp)
		{
			Compiled.Expression compiledLeft = Compile(exp.Left);
			Compiled.Expression compiledRight = Compile(exp.Right);

			if (null == compiledLeft)
			{
				this.AddDiagnostic(new Diagnostic("Cannot evaluate left side"), null, exp);
				return null;
			}

			if (null == compiledRight)
			{
				this.AddDiagnostic(new Diagnostic("Cannot evaluate right side"), null, exp);
				return null;
			}

			if (!SimpleInterpretter.IsAssignableFrom(compiledRight.InferredType, compiledLeft.InferredType))
			{
				this.AddDiagnostic(new CannotConvert(compiledRight.InferredType.ToString(), compiledLeft.InferredType.ToString()), null, exp);
			}

			return new AssignmentOperator
			{
				Left = compiledLeft,
				Right = compiledRight,
				Info = exp.Info
			};
		}

		public Compiled.Expression CompileAddAssignmentOperator(BinaryOperator op)
		{
			Compiled.Expression compiledLeft = Compile(op.Left);
			Compiled.Expression compiledRight = Compile(op.Right);

			if (!SimpleInterpretter.IsAssignableFrom(compiledLeft.InferredType, new TypeInfo(typeof(string))))
			{
				this.AddDiagnostic(new Diagnostic("Only string concatenation supported"), null, op);
				return null;
			}

			if (!IsSupportedStringConcatenationOperand(compiledRight.InferredType))
			{
				this.AddDiagnostic(new Diagnostic("Only string concatenation supported"), null, op);
				return null;
			}

			return new AssignmentOperator
			{
				Left = compiledLeft,
				Right = new Compiled.AddOperator
				{
					Left = compiledLeft,
					Right = compiledRight,
					InferredType = new TypeInfo(typeof(string))
				},
				Info = op.Info
			};
		}

		public Compiled.Expression CompileTypeOfOperator(BinaryOperator exp)
		{
			Compiled.Expression compiledLeft = Compile(exp.Left);
			Compiled.Expression compiledRight = Compile(exp.Right);

			//Support for external prototypes with unparseable names like System.String[value]
			//use "System.String[value]" instead
			if (compiledRight is Compiled.Literal)
			{
				Compiled.Literal literal = compiledRight as Compiled.Literal;
				string strValue = literal.Value as string;
				compiledRight = CompileRootIdentifier(strValue, compiledRight.Info);
			}

			if (null == compiledRight)
			{
				this.AddDiagnostic(new Diagnostic("Could not locate right side prototype"), null, exp);
				return null;
			}

			//N20221230-02 - Prevents a difficult to find bug
			if (compiledRight.InferredType is FieldTypeInfo)
			{
				this.AddDiagnostic(new Diagnostic("Prototype mapped to field"), null, exp);
				return null;
			}

			return new TypeOfOperator
			{
				Left = compiledLeft,
				Right = compiledRight
			};
		}

		public Compiled.Expression CompileNotOperator(UnaryOperator exp)
		{
			Compiled.Expression compiledRight = Compile(exp.Right);

			return new NotOperator
			{
				Right = compiledRight
			};
		}



		public Compiled.Expression Compile(NewObjectExpression expr)
		{

			List<Compiled.Expression> lstParams = expr.Parameters.Select(x => Compile(x)).ToList();

			////Right now we don't support new Converts<String>() as it is meaningless
			//if (expr.Type.IsGeneric)
			//{
			//	this.AddDiagnostic("Cannot construct a generic type", null, expr.Type);
			//	return null;
			//}

			//TypeInfo typeInfo = Symbols.GetTypeInfo(expr.Type.TypeName);
			TypeInfo typeInfo = ResolveTypeInfo(expr.Type, null, expr.Type);
			if (null == typeInfo)
				return null;

			if (typeInfo is PrototypeTypeInfo)
			{
				NewInstance newInstance = new NewInstance() { InferredType = typeInfo };
				newInstance.Parameters = lstParams;

				PrototypeTypeInfo prototypeTypeInfo = typeInfo as PrototypeTypeInfo;

				FunctionRuntimeInfo functionRuntimeInfo = prototypeTypeInfo.Scope.GetSymbol(expr.Type.TypeName) as FunctionRuntimeInfo;
				if (null != functionRuntimeInfo)
				{
					newInstance.Constructor = functionRuntimeInfo;
				}

				if (null != expr.Initializers)
				{
					//					Symbols.EnterScope(prototypeTypeInfo.Scope);

					try
					{
						foreach (Expression expInitializer in expr.Initializers)
						{
							if (expInitializer is not NewObjectExpression.ObjectInitializer objectInitializer)
							{
								this.AddDiagnostic(new Diagnostic("Prototype object initializers only support identifier assignments (Name = Value)."), null, expInitializer);
								return null;
							}

							NewInstance.ObjectInitializer initializer = Compile(objectInitializer, prototypeTypeInfo.Prototype);
							if (initializer == null)
								return null;
							newInstance.Initializers.Add(initializer);
						}
					}
					finally
					{
						//Symbols.LeaveScope();
					}
				}

				return newInstance;

			}

			else if (typeInfo is DotNetTypeInfo)
			{
				System.Type type = (typeInfo as DotNetTypeInfo).Type;
				if (lstParams.Any(x => x == null))
				{
					this.AddDiagnostic(new Diagnostic("Cannot resolve parameter"), null, expr);
					return null;
				}

				if (lstParams.Any(x => x.InferredType == null && !IsNullLiteralExpression(x)))
				{
					this.AddDiagnostic(new Diagnostic("Cannot resolve parameter type"), null, expr);
					return null;
				}

				bool isAmbiguous;
				System.Reflection.ConstructorInfo? constructor = ResolveConstructorForNewObject(type, lstParams, out isAmbiguous);

				if (null == constructor)
				{
					string signature = BuildConstructorResolutionSignature(expr.Type?.TypeName ?? type.Name, lstParams);
					string message = $"Cannot resolve constructor for {signature}.";
					if (isAmbiguous)
					{
						message += " Multiple overloads accept the provided null arguments.";
					}
					this.AddDiagnostic(new Diagnostic(message), null, expr);
					return null;
				}

				DotNetNewInstance newInstance = new DotNetNewInstance() { Constructor = constructor, Parameters = lstParams, InferredType = new TypeInfo(type) };

				if (expr.Initializers != null)
				{
					foreach (Expression expInitializer in expr.Initializers)
					{
						if (expInitializer is NewObjectExpression.ObjectInitializer objectInitializer)
						{
							Compiled.Expression memberValueExpression = Compile(objectInitializer.Value);
							if (memberValueExpression == null)
							{
								this.AddDiagnostic(new Diagnostic($"Could not compile initializer value for member '{objectInitializer.Name}'."), null, objectInitializer);
								return null;
							}
				
							newInstance.MemberInitializers.Add(new DotNetNewInstance.MemberInitializer
							{
								Name = objectInitializer.Name,
								Value = memberValueExpression,
								Info = objectInitializer.Info
							});
							continue;
						}
				
						Compiled.Expression collectionValueExpression = Compile(expInitializer);
						if (collectionValueExpression == null)
						{
							this.AddDiagnostic(new Diagnostic("Could not compile collection initializer entry."), null, expInitializer);
							return null;
						}
						newInstance.CollectionInitializers.Add(new DotNetNewInstance.CollectionInitializer
						{
							Value = collectionValueExpression,
							Info = expInitializer.Info
						});
						continue;
					}
				}

				return newInstance;
			}

			throw new ProtoScriptCompilerException(
				expr.Info,
				$"Unsupported new object target type '{expr.Type?.TypeName ?? "(unknown)"}' with resolved symbol type '{typeInfo.GetType().FullName}'.");

		}

		private static bool IsNullLiteralExpression(Compiled.Expression expression)
		{
			return expression is Compiled.Literal literal && literal.Value == null;
		}

		private static bool CanAssignNullToParameter(System.Type parameterType)
		{
			return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;
		}

		private System.Reflection.ConstructorInfo? ResolveConstructorForNewObject(System.Type type, List<Compiled.Expression> parameters, out bool isAmbiguous)
		{
			isAmbiguous = false;

			bool hasNullLiteral = parameters.Any(IsNullLiteralExpression);
			if (!hasNullLiteral)
			{
				List<System.Type> parameterTypes = parameters.Select(x => x.InferredType.Type).ToList();
				return ReflectionUtil.GetConstructor(type, parameterTypes);
			}

			List<System.Reflection.ConstructorInfo> matches = new List<System.Reflection.ConstructorInfo>();
			int bestScore = int.MaxValue;

			foreach (System.Reflection.ConstructorInfo candidate in ReflectionUtil.GetCandidateConstructors(type, parameters.Count))
			{
				ParameterInfo[] candidateParameters = candidate.GetParameters();
				int score = 0;
				bool compatible = true;

				for (int i = 0; i < candidateParameters.Length; i++)
				{
					Compiled.Expression argument = parameters[i];
					System.Type parameterType = candidateParameters[i].ParameterType;

					if (IsNullLiteralExpression(argument))
					{
						if (!CanAssignNullToParameter(parameterType))
						{
							compatible = false;
							break;
						}

						score += 1;
						continue;
					}

					System.Type? argumentType = argument.InferredType?.Type;
					if (argumentType == null)
					{
						compatible = false;
						break;
					}

					if (argumentType == parameterType)
					{
						continue;
					}

					if (parameterType.IsAssignableFrom(argumentType))
					{
						score += 2;
						continue;
					}

					compatible = false;
					break;
				}

				if (!compatible)
				{
					continue;
				}

				if (score < bestScore)
				{
					matches.Clear();
					matches.Add(candidate);
					bestScore = score;
				}
				else if (score == bestScore)
				{
					matches.Add(candidate);
				}
			}

			if (matches.Count > 1)
			{
				isAmbiguous = true;
			}

			return matches.Count == 1 ? matches[0] : null;
		}

		private static string BuildConstructorResolutionSignature(string typeName, List<Compiled.Expression> parameters)
		{
			List<string> parameterNames = new List<string>(parameters.Count);
			foreach (Compiled.Expression parameter in parameters)
			{
				if (IsNullLiteralExpression(parameter))
				{
					parameterNames.Add("null");
				}
				else if (parameter.InferredType?.Type != null)
				{
					parameterNames.Add(parameter.InferredType.Type.Name);
				}
				else if (parameter.InferredType != null)
				{
					parameterNames.Add(parameter.InferredType.ToString());
				}
				else
				{
					parameterNames.Add("?");
				}
			}

			return $"{typeName}({string.Join(", ", parameterNames)})";
		}






		public Compiled.Expression Compile(Identifier identifier)
		{
			return Compile(identifier.Value, identifier);
		}

		public Compiled.Expression Compile(string strPath, Expression exp)
		{

			//Lookup the multi-part identifier first, to support external prototype paths
			{
				Compiled.Expression? objCur = CompileRootIdentifier(strPath, exp.Info);

				if (null != objCur)
					return objCur;
			}

			if (strPath.Contains("."))
			{
				string[] strSplits = StringUtil.Split(strPath, ".");

				Compiled.Expression? objCur = CompileRootIdentifier(strSplits[0], exp.Info);

				if (null == objCur)
				{
					if (IsPrimitiveTypeAlias(strSplits[0]))
					{
						this.AddDiagnostic(new Diagnostic($"Cannot resolve '{strSplits[0]}.{strSplits[1]}'. Primitive type aliases are not callable static .NET types in this context."), null, exp);
						return null;
					}

					this.AddDiagnostic(new Diagnostic(BuildCannotFindIdentifierDiagnostic(strSplits[0], exp)), null, exp);
					return null;
				}

				for (int i = 1; i < strSplits.Length; i++)
				{
					string strPropertyName = strSplits[i];

					if (objCur.InferredType is PrototypeTypeInfo)
					{
						PrototypeTypeInfo prototypeTypeInfo = objCur.InferredType as PrototypeTypeInfo;

						//Try the immediate scope first
						FieldTypeInfo fieldTypeInfo = prototypeTypeInfo.Scope.GetSymbol(strPropertyName) as FieldTypeInfo;
						if (null != fieldTypeInfo)
						{
							objCur = new PrototypeFieldReference()
							{
								Left = objCur,
								Right = new GetGlobalStack() { Index = fieldTypeInfo.Index, InferredType = fieldTypeInfo.FieldInfo, Info = exp.Info },
								InferredType = fieldTypeInfo.FieldInfo,
								FieldInfo = fieldTypeInfo,
								Info = exp.Info
							};

							continue;
						}

						//Try the prototype hierarchy
						{
							var tuple = SimpleInterpretter.ResolveProperty(prototypeTypeInfo.Prototype, strPropertyName);

							if (null != tuple)
							{
								Prototype protoProp = tuple.Item1;

								fieldTypeInfo = Symbols.GetGlobalScope().GetSymbol(protoProp.PrototypeName) as FieldTypeInfo;
								if (null == fieldTypeInfo)
								{
									this.AddDiagnostic(new Diagnostic($"Resolved property '{strPropertyName}' but could not locate field metadata for prototype '{protoProp.PrototypeName}'."), null, exp);
									return null;
								}

								objCur = new PrototypeFieldReference()
								{
									Left = objCur,
									//TODO: Shouldn't the inferred type be globalStack[index].Type
									Right = new GetGlobalStack() { Index = fieldTypeInfo.Index, InferredType = fieldTypeInfo.FieldInfo, Info = exp.Info },
									InferredType = fieldTypeInfo.FieldInfo,
									FieldInfo = fieldTypeInfo,
									Info = exp.Info
								};

								continue;
							}
						}


						//Try resolving as a method 
						{
							FunctionRuntimeInfo functionRuntimeInfo = MethodCompiler.ResolveMethod2(prototypeTypeInfo.Prototype, strPropertyName, this.Symbols);

							if (null != functionRuntimeInfo)
							{
								PrototypeTypeInfo typeInfoParent = Symbols.GetGlobalScope().GetSymbol(functionRuntimeInfo.ParentPrototype.PrototypeName) as PrototypeTypeInfo;
								objCur = new GetStack() { Scope = typeInfoParent.Scope, Index = functionRuntimeInfo.Index, InferredType = new TypeInfo(typeof(FunctionRuntimeInfo)) };

								continue;
							}
						}
					}

					else if (objCur.InferredType is Namespace)
					{
						throw new ProtoScriptCompilerException(exp.Info, $"Unexpected namespace in member-access chain while resolving '{strPath}' at segment '{strPropertyName}'.");
					}

					//Try as a .NET property 
					{
						System.Reflection.FieldInfo fieldInfo = objCur.InferredType.Type.GetField(strPropertyName);
						if (null != fieldInfo)
						{
							objCur = new DotNetFieldReference()
							{
								Field = fieldInfo,
								Info = exp.Info,
								InferredType = new TypeInfo(fieldInfo.FieldType),
								Object = objCur
							};

							continue;
						}

						System.Reflection.PropertyInfo propertyInfo = objCur.InferredType.Type.GetProperty(strPropertyName);
						if (null != propertyInfo)
						{
							objCur = new DotNetPropertyReference()
							{
								Property = propertyInfo,
								Info = exp.Info,
								InferredType = new TypeInfo(propertyInfo.PropertyType),
								Object = objCur
							};

							continue;
						}
					}

					this.AddDiagnostic(new Diagnostic($"Cannot find field {strPropertyName}"), null, exp);
					return null;
				}

				return objCur;
			}

			this.AddDiagnostic(new Diagnostic(BuildCannotFindIdentifierDiagnostic(strPath, exp)), null, exp);
			return null;
		}

		private static string BuildCannotFindIdentifierDiagnostic(string identifier, Expression exp)
		{
			string fileName = string.IsNullOrWhiteSpace(exp.Info?.File) ? "unknown" : exp.Info.File;
			string offset = exp.Info == null ? "unknown" : exp.Info.StartingOffset.ToString();
			return $"Cannot find identifier {identifier} in {fileName} at offset {offset}";
		}

		private static bool IsPrimitiveTypeAlias(string identifier)
		{
			return identifier == "bool"
				|| identifier == "byte"
				|| identifier == "char"
				|| identifier == "decimal"
				|| identifier == "double"
				|| identifier == "float"
				|| identifier == "int"
				|| identifier == "long"
				|| identifier == "sbyte"
				|| identifier == "short"
				|| identifier == "string"
				|| identifier == "uint"
				|| identifier == "ulong"
				|| identifier == "ushort";
		}

		public Compiled.Expression Compile(Literal literal)
		{
			if (literal is BooleanLiteral)
			{
				return new Compiled.Literal() { Value = literal.Value == "true" ? true : false, InferredType = new TypeInfo(typeof(bool)) };
			}

			if (literal is IntegerLiteral)
			{
				return new Compiled.Literal() { Value = Convert.ToInt32(literal.Value), InferredType = new TypeInfo(typeof(int)) };
			}

			if (literal is CharacterLiteral)
			{
				char cValue = ParseCharacterLiteralValue(literal.Value, literal.Info);
				return new Compiled.Literal() { Value = cValue, InferredType = new TypeInfo(typeof(char)) };
			}

			if (literal is StringLiteral)
			{
				if (literal is PrototypeStringLiteral)
				{
					string strPrototypeName = StringUtil.Between(literal.Value, "\"", "\"");
					return CompileRootIdentifier(strPrototypeName, literal.Info);
				}

				if (literal is AtPrefixedStringLiteral)
				{
					string strVerbatimValue = ParseVerbatimStringLiteral(literal.Value);
					return new Compiled.Literal() { Value = strVerbatimValue, InferredType = new TypeInfo(typeof(string)) };
				}


				//N20250511-01 - Testing converting string literals to Prototypes at compilation. This can be avoided using the AtPrefixed literal 
				string strValue = JsonUtil.FromSafeString(StringUtil.Between(literal.Value, "\"", "\""));
				return new Compiled.Literal() { Value = StringWrapper.ToPrototype(strValue), InferredType = new TypeInfo(typeof(StringWrapper)) };

				//return new Compiled.Literal() { Value = JsonUtil.FromSafeString(StringUtil.Between(literal.Value, "\"", "\"")), InferredType = new TypeInfo(typeof(string)) };
			}

			if (literal is NullLiteral)
			{
				return new Compiled.Literal() { Value = null, InferredType = null };
			}

			if (literal is ArrayLiteral)
			{
				List<Compiled.Expression> lstExpressions = new List<Compiled.Expression>();
				foreach (Expression val in (literal as ArrayLiteral).Values)
				{
					Compiled.Expression exp = Compile(val);
					lstExpressions.Add(exp);
				}

				return new Compiled.ArrayLiteral() { Values = lstExpressions, InferredType = new TypeInfo(typeof(Ontology.Collection)) };
			}

			if (literal is DoubleLiteral)
			{
				return new Compiled.Literal() { Value = Convert.ToDouble(literal.Value), InferredType = new TypeInfo(typeof(double)) };
			}

			return new Compiled.Literal() { Value = literal.Value, InferredType = new TypeInfo(typeof(string)) };
		}

		private static string ParseVerbatimStringLiteral(string rawLiteralValue)
		{
			// rawLiteralValue is expected to include the @"..." delimiters. We preserve backslashes
			// and convert doubled quotes to single quotes, matching C# verbatim-string behavior.
			string strBetweenQuotes = StringUtil.Between(rawLiteralValue, "\"", "\"");
			return strBetweenQuotes.Replace("\"\"", "\"");
		}

		private static char ParseCharacterLiteralValue(string rawLiteralValue, StatementParsingInfo info)
		{
			if (string.IsNullOrEmpty(rawLiteralValue) || rawLiteralValue.Length < 3)
				throw new ProtoScriptCompilerException(info, $"Invalid character literal '{rawLiteralValue}'.");

			string strBetweenQuotes = rawLiteralValue.Substring(1, rawLiteralValue.Length - 2);
			if (strBetweenQuotes.Length == 1)
				return strBetweenQuotes[0];

			if (strBetweenQuotes[0] != '\\')
				throw new ProtoScriptCompilerException(info, $"Invalid character literal '{rawLiteralValue}'.");

			if (strBetweenQuotes.Length < 2)
				throw new ProtoScriptCompilerException(info, $"Invalid character literal '{rawLiteralValue}'.");

			char escape = strBetweenQuotes[1];
			switch (escape)
			{
				case '\'':
					return '\'';
				case '"':
					return '"';
				case '\\':
					return '\\';
				case '0':
					return '\0';
				case 'a':
					return '\a';
				case 'b':
					return '\b';
				case 'f':
					return '\f';
				case 'n':
					return '\n';
				case 'r':
					return '\r';
				case 't':
					return '\t';
				case 'v':
					return '\v';
				case 'u':
					if (strBetweenQuotes.Length == 6)
						return (char)Convert.ToInt32(strBetweenQuotes.Substring(2, 4), 16);
					break;
			}

			throw new ProtoScriptCompilerException(info, $"Invalid character literal '{rawLiteralValue}'.");
		}

		public FunctionRuntimeInfo DeclareFunction(FunctionDefinition funcDef)
		{
			FunctionRuntimeInfo funcInfo = new FunctionRuntimeInfo();

			if (Symbols.GetGlobalScope().TryGetSymbol(funcDef.FunctionName, out object oType))
			{
				this.AddDiagnostic(new Diagnostic($"A function with the same name already exists {funcDef.FunctionName}"), funcDef, null);
				return null;
			}

			funcInfo.FunctionName = funcDef.FunctionName;
			funcInfo.Info = funcDef.Info;

			Symbols.GetGlobalScope().InsertSymbol(funcDef.FunctionName, funcInfo);

			//Insert a generic name (without parameters) so it can be found easy later
			if (funcDef.FunctionName.Contains("<"))    //generic
			{
				string strGenericName = StringUtil.LeftOfFirst(funcInfo.FunctionName, "<") + "<>";
				Symbols.GetGlobalScope().InsertSymbol(strGenericName, funcInfo);
			}


			funcInfo.Index = Symbols.GlobalStack.Add(funcInfo);
			funcInfo.Scope = new Scope(Scope.ScopeTypes.Method);
			funcInfo.Scope.Stack.Add(null);       //return location

			return CompileSignature(funcDef, funcInfo);
		}


		public Compiled.Statement Compile(FunctionDefinition funcDef)
		{
			FunctionRuntimeInfo funcInfo = Symbols.ActiveScope().GetSymbol(funcDef.FunctionName) as FunctionRuntimeInfo;

			if (null == funcInfo)
			{
				this.AddDiagnostic(new Diagnostic("Could not find function: " + funcDef.FunctionName), funcDef, null);
				return null;
			}

			Symbols.EnterScope(funcInfo.Scope);

			try
			{
				Symbols.InsertSymbol("return", funcInfo.ReturnType);
				if (funcDef.ReturnType.TypeName != "void" && !funcDef.IsAbstract)
				{
					if (!StatementScanner.Any(funcDef, x => x is ReturnStatement))
					{
						this.AddDiagnostic(new Diagnostic("Function does not return a value"), funcDef, null);
						return null;
					}
				}


				foreach (Statement statement in funcDef.Statements)
				{
					Compiled.Statement? compiled = Compile(statement);
					if (compiled != null)
						funcInfo.Statements.Add(compiled);
				}


			}
			finally
			{
				Symbols.LeaveScope();
			}

			return null;
		}

		public FunctionRuntimeInfo DeclareMethod(FunctionDefinition funcDef, Prototype prototype)
		{
			FunctionRuntimeInfo funcInfo = new FunctionRuntimeInfo();
			funcInfo.FunctionName = funcDef.FunctionName;
			funcInfo.Info = funcDef.Info;
			funcInfo.ParentPrototype = prototype;
			if (funcDef.FunctionName == "that")
				funcDef.FunctionName = prototype.PrototypeName;

			funcInfo.IsConstructor = (prototype.PrototypeName == funcDef.FunctionName);

			Symbols.ActiveScope().InsertSymbol(funcDef.FunctionName, funcInfo);

			//Insert a generic name (without parameters) so it can be found easy later
			if (funcDef.FunctionName.Contains("<"))    //generic
			{
				string strGenericName = StringUtil.LeftOfFirst(funcInfo.FunctionName, "<") + "<>";
				Symbols.ActiveScope().InsertSymbol(strGenericName, funcInfo);
			}


			funcInfo.Index = Symbols.LocalStack.Add(funcInfo);
			funcInfo.Scope = new Scope(Scope.ScopeTypes.Method);
			funcInfo.Scope.Stack.Add(null);       //return location

			PrototypeTypeInfo infoThis = Symbols.GetGlobalScope().GetSymbol(prototype.PrototypeName) as PrototypeTypeInfo;
			ValueRuntimeInfo infoThisInstance = new ValueRuntimeInfo();
			infoThisInstance.Index = funcInfo.Scope.Stack.Add(infoThisInstance);
			infoThisInstance.OriginalType = infoThis.Clone();
			infoThisInstance.Type = infoThis.Clone();
			funcInfo.Scope.InsertSymbol("this", infoThisInstance);

			return CompileSignature(funcDef, funcInfo);
		}

		private FunctionRuntimeInfo CompileSignature(FunctionDefinition funcDef, FunctionRuntimeInfo funcInfo)
		{
			if (funcDef.ReturnType.TypeName != "void")
			{
				funcInfo.ReturnType = ResolveTypeInfo(funcDef.ReturnType, funcDef, funcDef.ReturnType);
				if (null == funcInfo.ReturnType)
					return funcInfo;
			}
			else
			{
				funcInfo.ReturnType = null;
			}
			foreach (ParameterDeclaration paramDec in funcDef.Parameters)
			{
				TypeInfo paramType = ResolveTypeInfo(paramDec.Type, paramDec, paramDec.Type);
				if (null == paramType)
					return null;

				if (paramType is PrototypeTypeInfo)
				{
					ParameterRuntimeInfo info = new ParameterRuntimeInfo();
					info.Type = paramType as PrototypeTypeInfo;
					info.Index = funcInfo.Scope.Stack.Add(info);
					info.OriginalType = info.Type.Clone();
					info.ParameterName = paramDec.ParameterName;

					funcInfo.Scope.InsertSymbol(paramDec.ParameterName, info);

					funcInfo.Parameters.Add(info);
				}
				else if (paramType is DotNetTypeInfo)
				{
					ParameterRuntimeInfo info = new ParameterRuntimeInfo();
					info.Type = paramType as DotNetTypeInfo;
					info.OriginalType = info.Type.Clone();
					info.Index = funcInfo.Scope.Stack.Add(info);
					info.ParameterName = paramDec.ParameterName;

					funcInfo.Scope.InsertSymbol(paramDec.ParameterName, info);

					funcInfo.Parameters.Add(info);
				}
				else if (paramType is TypeInfo)
				{
					ParameterRuntimeInfo info = new ParameterRuntimeInfo();
					info.Type = paramType as TypeInfo;
					info.OriginalType = info.Type.Clone();
					info.Index = funcInfo.Scope.Stack.Add(info);
					info.ParameterName = paramDec.ParameterName;

					funcInfo.Scope.InsertSymbol(paramDec.ParameterName, info);

					funcInfo.Parameters.Add(info);
				}

				else
					throw new ProtoScriptCompilerException(
						paramDec.Info ?? funcDef.Info,
						$"Unsupported parameter type category for '{paramDec.ParameterName}' in function '{funcDef.FunctionName}': {paramType.GetType().FullName}.");

			}

			return funcInfo;
		}

		public List<Compiled.Statement> CompileFunctionAnnotations(FunctionDefinition funcDef)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			FunctionRuntimeInfo funcInfo = Symbols.ActiveScope().GetSymbol(funcDef.FunctionName) as FunctionRuntimeInfo;

			if (null == funcInfo)
			{
				this.AddDiagnostic(new Diagnostic("Could not find method: " + funcDef.FunctionName), funcDef, null);
				return null;
			}


			foreach (AnnotationExpression annotation in funcDef.Annotations)
			{
				MethodEvaluation method = annotation.GetAnnotationMethodEvaluation();
				if (!annotation.IsExpanded)
				{
					method.Parameters.Insert(0, new Identifier(funcDef.FunctionName));
					annotation.IsExpanded = true;
				}

				Compiled.Expression expression = Compile(annotation);
				if (!(expression is Compiled.FunctionEvaluation))
				{
					this.AddDiagnostic(new Diagnostic("Annotation must compile to a function evaluation."), funcDef, annotation);
					continue;
				}

				Compiled.FunctionEvaluation functionEvaluation = expression as Compiled.FunctionEvaluation;

				lstStatements.Add(new Compiled.PrototypeAnnotation { AnnotationFunction = functionEvaluation, Info = annotation.Info });
			}


			return lstStatements;
		}

		public Compiled.Expression Compile(MethodEvaluation methodEval)
		{
			// Guard malformed method-evaluation nodes so we produce diagnostics instead of runtime null-reference failures.
			if (methodEval == null || string.IsNullOrWhiteSpace(methodEval.MethodName))
			{
				this.AddDiagnostic(new Diagnostic("Invalid method evaluation: method name is missing."), null, methodEval);
				return null;
			}

			if (methodEval.MethodName.Contains("."))
			{
				string strIdentifier = StringUtil.LeftOfLast(methodEval.MethodName, ".");
				Compiled.Expression expression = Compile(strIdentifier, methodEval);
				if (null == expression)
				{
					int separator = strIdentifier.IndexOf('.');
					string rootIdentifier = separator < 0 ? strIdentifier : strIdentifier.Substring(0, separator);
					if (IsPrimitiveTypeAlias(rootIdentifier))
					{
						this.AddDiagnostic(new Diagnostic($"Cannot resolve '{methodEval.MethodName}'. Primitive type aliases are not callable static .NET types in this context."), null, methodEval);
						return null;
					}

					this.AddDiagnostic(new Diagnostic($"Could not find method {methodEval.MethodName}"), null, methodEval);
					return null;
				}
				return CompileMethodEvaluationInternal(methodEval, expression);
			}
			else
			{
				//Simple case, no multi-part identifier
				object obj = Symbols.GetSymbol(methodEval.MethodName);

				if (null == obj)
				{
					if (methodEval.MethodName == "nameof")
					{
						if (methodEval.Parameters.Count == 0)
						{
							this.AddDiagnostic(new Diagnostic("nameof requires one parameter."), null, methodEval);
							return null;
						}

						string strValue = methodEval.Parameters[0].ToString();
						if (strValue == "that")
						{
							PrototypeTypeInfo prototypeTypeInfo = Symbols.GetSymbol(strValue) as PrototypeTypeInfo;
							strValue = prototypeTypeInfo.Prototype.PrototypeName;
						}

						return new Compiled.Literal()
						{
							Value = strValue,
							InferredType = new TypeInfo(typeof(string)),
							Info = methodEval.Parameters[0].Info
						};
					}
					else
					{
						this.AddDiagnostic(new UnknownFunction(methodEval.MethodName), null, methodEval);
						return null;
					}
				}

				List<Compiled.Expression> lstParameters = new List<Compiled.Expression>();

				for (int i = 0; i < methodEval.Parameters.Count; i++)
				{
					Compiled.Expression exp = Compile(methodEval.Parameters[i]);
					if (null == exp)
					{
						this.AddDiagnostic(new Diagnostic("Unknown Parameter"), null, methodEval.Parameters[i]);
						return null;
					}

					exp.Info = methodEval.Parameters[i].Info;

					lstParameters.Add(exp);
				}

				FunctionEvaluation info = new FunctionEvaluation();
				info.Parameters = lstParameters;

				FunctionRuntimeInfo? functionRuntimeInfo = obj as FunctionRuntimeInfo;
				if (functionRuntimeInfo == null)
				{
					string symbolTypeName = obj.GetType().Name;
					this.AddDiagnostic(new Diagnostic($"Cannot call symbol {methodEval.MethodName}: symbol type {symbolTypeName} is not a function."), null, methodEval);
					return null;
				}

				//TODO: allow for function overloading here
				if (info.Parameters.Count != functionRuntimeInfo.Parameters.Count)
				{
					this.AddDiagnostic(new Diagnostic("Incorrect number of parameters"), null, methodEval);
					return null;
				}

				for (int i = 0; i < functionRuntimeInfo.Parameters.Count; i++)
				{
					if (i >= info.Parameters.Count)
					{
						this.AddDiagnostic(new Diagnostic("Not enough parameter supplied"), null, methodEval);
						return null;
					}
					ParameterRuntimeInfo destParam = functionRuntimeInfo.Parameters[i];
					Compiled.Expression exp = info.Parameters[i];

					if (!SimpleInterpretter.IsAssignableFrom(exp.InferredType, destParam.Type))
					{
						this.AddDiagnostic(new CannotConvert(exp.InferredType.ToString(), destParam.Type.ToString()), null, methodEval.Parameters[i]);
						return null;
					}

				}

				info.Function = functionRuntimeInfo;
				info.InferredType = functionRuntimeInfo.ReturnType;

				if (null != functionRuntimeInfo.ParentPrototype)
				{
					PrototypeTypeInfo parentTypeInfo = Symbols.GetGlobalScope().GetSymbol(functionRuntimeInfo.ParentPrototype.PrototypeName) as PrototypeTypeInfo;
					info.Object = new GetLocalStack() { Index = 1, InferredType = parentTypeInfo, Info = functionRuntimeInfo.Info };
				}

				return info;
			}
		}

		private Compiled.Expression CompileMethodEvaluationInternal(MethodEvaluation methodEval, Compiled.Expression expression)
		{
			return MethodCompiler.CompileMethodEvaluationInternal(methodEval, expression, this);
		}
		public Compiled.ForEachStatement Compile(ForEachStatement statement)
		{
			Compiled.ForEachStatement compiled = new Compiled.ForEachStatement();
			compiled.Info = statement.Info;
			compiled.Scope = new Scope(Scope.ScopeTypes.Block);

			TypeInfo infoType = Symbols.GetGlobalScope().GetSymbol(statement.Type.TypeName) as TypeInfo;

			VariableRuntimeInfo variableRuntimeInfo = new VariableRuntimeInfo();
			variableRuntimeInfo.Type = infoType;
			variableRuntimeInfo.Index = compiled.Scope.Stack.Add(variableRuntimeInfo);
			compiled.Scope.InsertSymbol(statement.IteratorName, variableRuntimeInfo);

			Symbols.EnterScope(compiled.Scope);

			compiled.Iterator = variableRuntimeInfo;

			try
			{
				compiled.Expression = Compile(statement.Expression);
				compiled.Statements = Compile(statement.Statements);
			}
			finally
			{
				Symbols.LeaveScope();
			}

			return compiled;
		}

		public Compiled.ReturnStatement Compile(ReturnStatement statement)
		{
			TypeInfo info = null;

			if (!Symbols.TryGetSymbol<TypeInfo>("return", out info))
			{
				this.AddDiagnostic(new Diagnostic("return statement not valid in this context"), statement, null);
				return null;
			}

			Compiled.ReturnStatement compiledReturn = new Compiled.ReturnStatement();
			compiledReturn.Info = statement.Info;

			if (statement.Expression == null)
			{
				if (info == null)
				{
					this.AddDiagnostic(new Diagnostic("return type should be void"), statement, null);
					return null;
				}
			}
			else
			{
				compiledReturn.Expression = Compile(statement.Expression);

				if (null == compiledReturn.Expression)
					return compiledReturn;


				if (!SimpleInterpretter.IsAssignableFrom(compiledReturn.Expression.InferredType, info))
				{
					this.AddDiagnostic(new CannotConvert(compiledReturn.Expression.InferredType.ToString(), info.ToString()), statement, null);
				}
			}

			return compiledReturn;
		}



		public void Compile(ReferenceStatement statement)
		{
			Assembly? assembly = null;
			string loadResolution = string.Empty;
			if (statement.IsFileReference || LooksLikeAssemblyPath(statement.AssemblyName))
			{
				if (!TryResolveReferenceAssemblyPath(statement, out string fullPath, out string? resolveError))
				{
					this.AddDiagnostic(new Diagnostic(resolveError ?? $"Could not resolve reference path {statement.AssemblyName}"), statement, null);
					TrackReferenceAssemblyInfo(statement, null, false, "resolve-path", resolveError);
					return;
				}

				statement.ResolvedAssemblyPath = fullPath;
				if (!StringUtil.EqualNoCase(Path.GetExtension(fullPath), ".dll"))
				{
					this.AddDiagnostic(new Diagnostic($"Reference path must point to a .dll file: {fullPath}"), statement, null);
					TrackReferenceAssemblyInfo(statement, null, false, "validate-extension", $"Reference path must point to a .dll file: {fullPath}");
					return;
				}

				try
				{
					assembly = LoadAssemblyFromResolvedPath(fullPath, out loadResolution);
				}
				catch (BadImageFormatException)
				{
					this.AddDiagnostic(new Diagnostic($"Invalid .dll reference: {fullPath}"), statement, null);
					TrackReferenceAssemblyInfo(statement, null, false, "load-from-path", $"Invalid .dll reference: {fullPath}");
					return;
				}
				catch (Exception err)
				{
					string message = $"Could not load assembly from path {fullPath}: {err.Message}"
						+ BuildAssemblyLoadFailureDetails(fullPath, err);
					this.AddDiagnostic(new Diagnostic(message), statement, null);
					TrackReferenceAssemblyInfo(statement, null, false, "load-from-path", message);
					return;
				}
			}
			else
			{
				try
				{
					assembly = Assembly.Load(statement.AssemblyName);
					loadResolution = "assembly-load";
				}
				catch
				{
					try
					{
						assembly = Assembly.LoadFrom(statement.AssemblyName);
						loadResolution = "assembly-load-from";
					}
					catch
					{
						assembly = null;
					}
				}
			}

			if (null == assembly)
			{
				this.AddDiagnostic(new Diagnostic("Could not load assembly " + statement.AssemblyName), statement, null);
				TrackReferenceAssemblyInfo(statement, null, false, "load-failed", "Could not load assembly " + statement.AssemblyName);
				return;
			}

			if (string.IsNullOrWhiteSpace(statement.Reference))
				statement.Reference = assembly.GetName().Name ?? statement.AssemblyName;

			References[statement.Reference] = assembly;
			TrackReferenceAssemblyInfo(statement, assembly, true, loadResolution, null);
		}

		public IReadOnlyList<ReferenceAssemblyInfo> GetReferenceAssemblyInfos()
		{
			return _referenceAssemblyInfos.Values
				.OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		public string GetReferenceAssemblyReport()
		{
			List<ReferenceAssemblyInfo> infos = GetReferenceAssemblyInfos().ToList();
			if (infos.Count == 0)
				return "No reference assemblies tracked.";

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.AppendLine("Reference assemblies:");
			foreach (ReferenceAssemblyInfo info in infos)
			{
				sb.Append("- alias=").Append(info.Alias);
				sb.Append(", requested=").Append(info.RequestedReference);
				sb.Append(", succeeded=").Append(info.LoadSucceeded ? "true" : "false");
				sb.Append(", resolution=").Append(info.LoadResolution);

				if (!string.IsNullOrWhiteSpace(info.AssemblyVersion))
					sb.Append(", version=").Append(info.AssemblyVersion);

				if (info.LastWriteUtc.HasValue)
					sb.Append(", lastWriteUtc=").Append(info.LastWriteUtc.Value.ToString("O"));

				if (!string.IsNullOrWhiteSpace(info.ResolvedAssemblyPath))
					sb.Append(", path=").Append(info.ResolvedAssemblyPath);

				if (!string.IsNullOrWhiteSpace(info.Error))
					sb.Append(", error=").Append(info.Error);

				sb.AppendLine();
			}

			return sb.ToString().TrimEnd();
		}

		private void TrackReferenceAssemblyInfo(ReferenceStatement statement, Assembly? assembly, bool loadSucceeded, string loadResolution, string? error)
		{
			string alias = !string.IsNullOrWhiteSpace(statement.Reference)
				? statement.Reference
				: statement.AssemblyName;

			string? loadedLocation = null;
			try
			{
				loadedLocation = assembly?.Location;
			}
			catch
			{
				loadedLocation = null;
			}

			string? metadataPath = statement.ResolvedAssemblyPath;
			if (string.IsNullOrWhiteSpace(metadataPath))
				metadataPath = loadedLocation;

			string? fileVersion = null;
			System.DateTime? lastWriteUtc = null;
			if (!string.IsNullOrWhiteSpace(metadataPath))
			{
				try
				{
					FileInfo info = new FileInfo(metadataPath);
					if (info.Exists)
					{
						lastWriteUtc = info.LastWriteTimeUtc;
						FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(metadataPath);
						fileVersion = fileVersionInfo?.FileVersion;
					}
				}
				catch
				{
					// Best-effort metadata only.
				}
			}

			string? informationalVersion = null;
			try
			{
				informationalVersion = assembly?
					.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
					.InformationalVersion;
			}
			catch
			{
				informationalVersion = null;
			}

			AssemblyName? assemblyName = null;
			try
			{
				assemblyName = assembly?.GetName();
			}
			catch
			{
				assemblyName = null;
			}

			_referenceAssemblyInfos[alias] = new ReferenceAssemblyInfo
			{
				Alias = alias,
				RequestedReference = statement.AssemblyName,
				IsFileReference = statement.IsFileReference,
				ResolvedAssemblyPath = statement.ResolvedAssemblyPath,
				AssemblySimpleName = assemblyName?.Name,
				AssemblyFullName = assembly?.FullName,
				AssemblyVersion = assemblyName?.Version?.ToString(),
				FileVersion = fileVersion,
				InformationalVersion = informationalVersion,
				LastWriteUtc = lastWriteUtc,
				LoadedLocation = loadedLocation,
				LoadResolution = string.IsNullOrWhiteSpace(loadResolution) ? "unknown" : loadResolution,
				LoadSucceeded = loadSucceeded,
				Error = error
			};
		}

		private static string BuildAssemblyLoadFailureDetails(string fullPath, Exception err)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append(" | details: path=").Append(fullPath);

			try
			{
				FileInfo info = new FileInfo(fullPath);
				sb.Append(", exists=").Append(info.Exists ? "true" : "false");
				if (info.Exists)
				{
					sb.Append(", length=").Append(info.Length);
					sb.Append(", lastWriteUtc=").Append(info.LastWriteTimeUtc.ToString("O"));
				}
			}
			catch (Exception fileInfoErr)
			{
				sb.Append(", exists=unknown");
				sb.Append(", fileInfoError=").Append(fileInfoErr.GetType().Name).Append(": ").Append(fileInfoErr.Message);
			}

			sb.Append(", exceptionType=").Append(err.GetType().FullName ?? err.GetType().Name);
			sb.Append(", exceptionMessage=").Append(err.Message);

			string innerChain = FormatInnerExceptionChain(err, 3);
			if (!string.IsNullOrWhiteSpace(innerChain))
			{
				sb.Append(", innerExceptions=").Append(innerChain);
			}

			string probeHint = TryGetProbeHint(err);
			if (!string.IsNullOrWhiteSpace(probeHint))
			{
				sb.Append(", probeHint=").Append(probeHint);
			}

			return sb.ToString();
		}

		private static string FormatInnerExceptionChain(Exception err, int maxDepth)
		{
			if (maxDepth <= 0)
				return string.Empty;

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			int depth = 0;
			Exception? current = err.InnerException;
			while (current != null && depth < maxDepth)
			{
				if (depth > 0)
					sb.Append(" -> ");

				sb.Append('[')
					.Append(depth + 1)
					.Append("] ")
					.Append(current.GetType().Name)
					.Append(": ")
					.Append(current.Message);

				current = current.InnerException;
				depth++;
			}

			return sb.ToString();
		}

		private static string TryGetProbeHint(Exception err)
		{
			string? missingDependency = null;
			string? fusionLog = null;

			Exception? current = err;
			int depth = 0;
			while (current != null && depth < 8)
			{
				if (current is FileNotFoundException fnf && !string.IsNullOrWhiteSpace(fnf.FileName))
				{
					missingDependency = fnf.FileName;
				}

				if (string.IsNullOrWhiteSpace(fusionLog))
				{
					try
					{
						PropertyInfo? fusionProp = current.GetType().GetProperty("FusionLog", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (fusionProp != null && fusionProp.PropertyType == typeof(string))
						{
							string? fusionValue = fusionProp.GetValue(current) as string;
							if (!string.IsNullOrWhiteSpace(fusionValue))
								fusionLog = fusionValue;
						}
					}
					catch
					{
						// Ignore reflection issues and continue probing.
					}
				}

				current = current.InnerException;
				depth++;
			}

			if (string.IsNullOrWhiteSpace(missingDependency) && string.IsNullOrWhiteSpace(fusionLog))
				return string.Empty;

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			if (!string.IsNullOrWhiteSpace(missingDependency))
			{
				sb.Append("missingDependency=").Append(missingDependency);
			}

			if (!string.IsNullOrWhiteSpace(fusionLog))
			{
				if (sb.Length > 0)
					sb.Append("; ");

				string normalizedFusion = fusionLog.Replace("\r", " ").Replace("\n", " ").Trim();
				if (normalizedFusion.Length > 500)
					normalizedFusion = normalizedFusion.Substring(0, 500) + "...";
				sb.Append("fusionLog=").Append(normalizedFusion);
			}

			return sb.ToString();
		}

		private static bool LooksLikeAssemblyPath(string assemblyName)
		{
			if (string.IsNullOrWhiteSpace(assemblyName))
				return false;

			return assemblyName.Contains("\\")
				|| assemblyName.Contains("/")
				|| assemblyName.Contains(":")
				|| assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
		}

		private static bool TryResolveReferenceAssemblyPath(ReferenceStatement statement, out string fullPath, out string? error)
		{
			fullPath = string.Empty;
			error = null;

			string rawPath = statement.AssemblyName;
			if (string.IsNullOrWhiteSpace(rawPath))
			{
				error = "Reference path is missing";
				return false;
			}

			string candidatePath;
			if (Path.IsPathRooted(rawPath))
			{
				candidatePath = rawPath;
			}
			else
			{
				string baseDirectory = Environment.CurrentDirectory;
				if (!string.IsNullOrWhiteSpace(statement.Info?.File))
				{
					try
					{
						string? statementDirectory = Path.GetDirectoryName(statement.Info.File);
						if (!string.IsNullOrWhiteSpace(statementDirectory))
							baseDirectory = statementDirectory;
					}
					catch
					{
						// keep current working directory as fallback
					}
				}

				candidatePath = Path.Combine(baseDirectory, rawPath);
			}

			try
			{
				fullPath = Path.GetFullPath(candidatePath);
			}
			catch (Exception err)
			{
				error = $"Invalid reference path {rawPath}: {err.Message}";
				return false;
			}

			if (!System.IO.File.Exists(fullPath))
			{
				error = $"Reference DLL not found: {fullPath}";
				return false;
			}

			return true;
		}

		private static Assembly LoadAssemblyFromResolvedPath(string fullPath, out string loadResolution)
		{
			lock (s_assemblyPathCacheLock)
			{
				return LoadAssemblyFromResolvedPath(fullPath, s_assemblyPathCache, out loadResolution);
			}
		}

		public static void ClearAssemblyReferenceCache()
		{
			lock (s_assemblyPathCacheLock)
			{
				s_assemblyPathCache.Clear();
			}
		}

		private static Assembly LoadAssemblyFromResolvedPath(string path, Dictionary<string, Assembly> loadedAssemblies, out string loadResolution)
		{
			string fullPath = Path.GetFullPath(path);
			string fingerprint = BuildShadowDirectoryKey(fullPath);

			Logs.DebugLog.WriteEvent("AssemblyLoad.RequestedPath", fullPath);

			if (loadedAssemblies.TryGetValue(fingerprint, out Assembly? cached))
			{
				Logs.DebugLog.WriteEvent("AssemblyLoad.CacheHit", "reason=fingerprint");
				loadResolution = "cache-fingerprint";
				return cached;
			}

			if (TryGetLoadedAssemblyByLocation(fullPath, out Assembly? loadedFromLocation))
			{
				loadedAssemblies[fingerprint] = loadedFromLocation;
				Logs.DebugLog.WriteEvent("AssemblyLoad.CacheHit", "reason=exact-location");
				loadResolution = "exact-location";
				return loadedFromLocation;
			}

			if (!System.IO.File.Exists(fullPath))
				throw new FileNotFoundException("Assembly path not found.", fullPath);

			string sourceDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty;
			string shadowDirectory = PrepareShadowCopyDirectory(fullPath);
			string shadowEntryPath = Path.Combine(shadowDirectory, Path.GetFileName(fullPath));
			Logs.DebugLog.WriteEvent("AssemblyLoad.ShadowPath", shadowEntryPath);
			ResolveEventHandler resolver = (_, args) =>
			{
				AssemblyName requestedName = new AssemblyName(args.Name);
				if (string.IsNullOrWhiteSpace(requestedName.Name))
				{
					return null;
				}

				string dependencySourcePath = Path.Combine(sourceDirectory, requestedName.Name + ".dll");
				if (!System.IO.File.Exists(dependencySourcePath))
				{
					return null;
				}

				string dependencyFullPath = Path.GetFullPath(dependencySourcePath);
				string dependencyFingerprint = BuildShadowDirectoryKey(dependencyFullPath);
				if (loadedAssemblies.TryGetValue(dependencyFingerprint, out Assembly? depCached))
				{
					Logs.DebugLog.WriteEvent("AssemblyLoad.CacheHit", "reason=fingerprint");
					return depCached;
				}

				if (TryGetLoadedAssemblyByLocation(dependencyFullPath, out Assembly? loadedDependencyByLocation))
				{
					loadedAssemblies[dependencyFingerprint] = loadedDependencyByLocation;
					Logs.DebugLog.WriteEvent("AssemblyLoad.CacheHit", "reason=exact-location");
					return loadedDependencyByLocation;
				}

				string shadowDependencyPath = Path.Combine(shadowDirectory, requestedName.Name + ".dll");
				if (!System.IO.File.Exists(shadowDependencyPath))
				{
					CopyFileWithRetry(dependencyFullPath, shadowDependencyPath);
				}

				try
				{
					Assembly loadedDependency = Assembly.LoadFile(shadowDependencyPath);
					loadedAssemblies[dependencyFingerprint] = loadedDependency;
					Logs.DebugLog.WriteEvent("AssemblyLoad.LoadedLocation", loadedDependency.Location);
					return loadedDependency;
				}
				catch (FileLoadException fileLoadException)
				{
					if (IsAlreadyLoadedFileLoadException(fileLoadException)
						&& TryGetLoadedAssemblyByIdentity(dependencyFullPath, out Assembly? loadedDependencyAfterLoadFailure))
					{
						loadedAssemblies[dependencyFingerprint] = loadedDependencyAfterLoadFailure;
						Logs.DebugLog.WriteEvent("AssemblyLoad.CacheHit", "reason=assembly-identity-after-fileload");
						return loadedDependencyAfterLoadFailure;
					}

					throw;
				}
			};

			AppDomain.CurrentDomain.AssemblyResolve += resolver;
			try
			{
				try
				{
					Assembly loadedAssembly = Assembly.LoadFile(shadowEntryPath);
					loadedAssemblies[fingerprint] = loadedAssembly;
					Logs.DebugLog.WriteEvent("AssemblyLoad.LoadedLocation", loadedAssembly.Location);
					loadResolution = "load-from-shadow";
					return loadedAssembly;
				}
				catch (FileLoadException fileLoadException)
				{
					if (IsAlreadyLoadedFileLoadException(fileLoadException)
						&& TryGetLoadedAssemblyByIdentity(fullPath, out Assembly? loadedAssemblyAfterLoadFailure))
					{
						loadedAssemblies[fingerprint] = loadedAssemblyAfterLoadFailure;
						Logs.DebugLog.WriteEvent("AssemblyLoad.CacheHit", "reason=assembly-identity-after-fileload");
						loadResolution = "assembly-identity-after-fileload";
						return loadedAssemblyAfterLoadFailure;
					}

					throw;
				}
			}
			finally
			{
				AppDomain.CurrentDomain.AssemblyResolve -= resolver;
			}
		}

		private static string PrepareShadowCopyDirectory(string sourceAssemblyPath)
		{
			string sourceDirectory = Path.GetDirectoryName(sourceAssemblyPath) ?? string.Empty;
			string shadowDirectory = Path.Combine(
				Path.GetTempPath(),
				"ProtoScriptShadow",
				"p" + Environment.ProcessId,
				BuildShadowDirectoryVersionKey(sourceDirectory, sourceAssemblyPath));

			object copyLock = s_shadowCopyLocks.GetOrAdd(shadowDirectory, _ => new object());
			lock (copyLock)
			{
				Directory.CreateDirectory(shadowDirectory);

				if (!string.IsNullOrWhiteSpace(sourceDirectory) && Directory.Exists(sourceDirectory))
				{
					foreach (string sourceDllPath in Directory.GetFiles(sourceDirectory, "*.dll"))
					{
						string destinationDllPath = Path.Combine(shadowDirectory, Path.GetFileName(sourceDllPath));
						CopyFileIfChangedWithRetry(sourceDllPath, destinationDllPath);
					}
				}
				else
				{
					string destinationPath = Path.Combine(shadowDirectory, Path.GetFileName(sourceAssemblyPath));
					CopyFileIfChangedWithRetry(sourceAssemblyPath, destinationPath);
				}
			}

			return shadowDirectory;
		}

		private static string BuildShadowDirectoryKeyForDirectory(string sourceDirectoryPath)
		{
			if (string.IsNullOrWhiteSpace(sourceDirectoryPath))
			{
				return "NO_SOURCE_DIRECTORY";
			}

			string normalizedPath = Path.GetFullPath(sourceDirectoryPath).Trim().ToLowerInvariant();
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(normalizedPath);
			byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
			return Convert.ToHexString(hash);
		}

		private static string BuildShadowDirectoryVersionKey(string sourceDirectoryPath, string sourceAssemblyPath)
		{
			if (string.IsNullOrWhiteSpace(sourceDirectoryPath) || !Directory.Exists(sourceDirectoryPath))
			{
				return BuildShadowDirectoryKey(sourceAssemblyPath);
			}

			System.Text.StringBuilder fingerprint = new System.Text.StringBuilder();
			fingerprint.Append(BuildShadowDirectoryKeyForDirectory(sourceDirectoryPath));

			foreach (string sourceDllPath in Directory.GetFiles(sourceDirectoryPath, "*.dll").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
			{
				FileInfo fileInfo = new FileInfo(sourceDllPath);
				fingerprint.Append('|');
				fingerprint.Append(Path.GetFileName(sourceDllPath).ToLowerInvariant());
				fingerprint.Append('|');
				fingerprint.Append(fileInfo.Exists ? fileInfo.Length : 0L);
				fingerprint.Append('|');
				fingerprint.Append(fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0L);
			}

			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(fingerprint.ToString());
			byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
			return Convert.ToHexString(hash);
		}

		private static void CopyFileIfChangedWithRetry(string sourcePath, string destinationPath)
		{
			FileInfo sourceInfo = new FileInfo(sourcePath);
			FileInfo destinationInfo = new FileInfo(destinationPath);
			if (destinationInfo.Exists
				&& destinationInfo.Length == sourceInfo.Length
				&& destinationInfo.LastWriteTimeUtc >= sourceInfo.LastWriteTimeUtc)
			{
				return;
			}

			CopyFileWithRetry(sourcePath, destinationPath);
		}

		private static void CopyFileWithRetry(string sourcePath, string destinationPath)
		{
			const int maxAttempts = 5;
			for (int attempt = 1; attempt <= maxAttempts; attempt++)
			{
				try
				{
					System.IO.File.Copy(sourcePath, destinationPath, true);
					return;
				}
				catch (IOException) when (attempt < maxAttempts)
				{
					System.Threading.Thread.Sleep(50 * attempt);
				}
				catch (UnauthorizedAccessException) when (attempt < maxAttempts)
				{
					System.Threading.Thread.Sleep(50 * attempt);
				}
			}

			// Final attempt: let the real exception bubble.
			System.IO.File.Copy(sourcePath, destinationPath, true);
		}

		private static string BuildShadowDirectoryKey(string sourceAssemblyPath)
		{
			string normalizedPath = Path.GetFullPath(sourceAssemblyPath).Trim().ToLowerInvariant();
			FileInfo fileInfo = new FileInfo(normalizedPath);
			long length = fileInfo.Exists ? fileInfo.Length : 0L;
			long lastWriteUtcTicks = fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0L;
			string seed = $"{normalizedPath}|{length}|{lastWriteUtcTicks}";
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(seed);
			byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
			return Convert.ToHexString(hash);
		}

		private static bool TryGetLoadedAssemblyByLocation(string requestedFullPath, out Assembly? assembly)
		{
			foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (string.IsNullOrWhiteSpace(loaded.Location))
					continue;

				string loadedPath;
				try
				{
					loadedPath = Path.GetFullPath(loaded.Location);
				}
				catch
				{
					continue;
				}

				if (StringUtil.EqualNoCase(loadedPath, requestedFullPath))
				{
					assembly = loaded;
					return true;
				}
			}

			assembly = null;
			return false;
		}

		private static bool IsAlreadyLoadedFileLoadException(FileLoadException ex)
		{
			if (ex == null)
				return false;

			string message = ex.Message ?? string.Empty;
			if (message.IndexOf("already loaded", StringComparison.OrdinalIgnoreCase) >= 0)
				return true;

			if (message.IndexOf("same name is already loaded", StringComparison.OrdinalIgnoreCase) >= 0)
				return true;

			return false;
		}

		private static bool TryGetLoadedAssemblyByIdentity(string requestedFullPath, out Assembly? assembly)
		{
			try
			{
				AssemblyName requestedName = AssemblyName.GetAssemblyName(requestedFullPath);
				return TryGetLoadedAssemblyByIdentity(requestedName, out assembly);
			}
			catch
			{
				assembly = null;
				return false;
			}
		}

		private static bool TryGetLoadedAssemblyByIdentity(AssemblyName requestedName, out Assembly? assembly)
		{
			foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					AssemblyName loadedName = loaded.GetName();
					if (AssemblyName.ReferenceMatchesDefinition(requestedName, loadedName))
					{
						assembly = loaded;
						return true;
					}
				}
				catch
				{
					// Ignore dynamic or inaccessible assemblies and continue scanning.
				}
			}

			assembly = null;
			return false;
		}

		public void Compile(ImportStatement statement)
		{
			System.Reflection.Assembly ? assembly;

			if (!References.TryGetValue(statement.Reference, out object ? obj))
			{
				this.AddDiagnostic(new Diagnostic("Assembly not referenced: " + statement.Reference), statement, null);
				return;
			}

			assembly = obj as System.Reflection.Assembly;

			if (null == assembly)
			{
				this.AddDiagnostic(new Diagnostic("Assembly not referenced: " + statement.Reference), statement, null);
				return;
			}

			System.Type ? type = assembly.GetType(statement.Type);

			if (null == type)
			{
				this.AddDiagnostic(new Diagnostic($"Type {statement.Type} not found in Assembly: {statement.Reference}"), statement, null);
				return;
			}

			DotNetTypeInfo info = new DotNetTypeInfo(type);
			info.Index = Symbols.GlobalStack.Add(info);

			Symbols.InsertSymbol(statement.Import, info);
		}


		public Compiled.IfStatement Compile(IfStatement statement)
		{
			Compiled.IfStatement compiled = new Compiled.IfStatement();
			compiled.Info = statement.Info;

			compiled.Condition = Compile(statement.Condition);
			compiled.TrueBody = Compile(statement.TrueBody);

			if (statement.ElseBody != null)
				compiled.ElseBody = Compile(statement.ElseBody);

			if (statement.ElseIfConditions.Count > 0)
			{
				foreach (Expression elseIf in statement.ElseIfConditions)
				{
					compiled.ElseIfConditions.Add(Compile(elseIf));
				}
				foreach (CodeBlock codeBlock in statement.ElseIfBodies)
				{
					compiled.ElseIfBodies.Add(Compile(codeBlock));
				}
			}



			return compiled;
		}

		public Compiled.CodeBlock Compile(CodeBlock statements)
		{
			Compiled.CodeBlock compiled = new Compiled.CodeBlock();
			compiled.Scope = new Scope(Scope.ScopeTypes.Block);

			try
			{
				Symbols.EnterScope(compiled.Scope);

				foreach (Statement statement in statements)
				{
					Compiled.Statement? compiledStatement = Compile(statement);
					if (compiledStatement != null)
						compiled.Add(compiledStatement);
				}
			}
			finally
			{
				Symbols.LeaveScope();
			}

			return compiled;
		}

		public Compiled.CodeBlockStatement Compile(CodeBlockStatement statements)
		{
			Compiled.CodeBlock compiled = new Compiled.CodeBlock();
			compiled.Scope = new Scope(Scope.ScopeTypes.Block);

			try
			{
				Symbols.EnterScope(compiled.Scope);

				foreach (Statement statement in statements.Statements)
				{
					Compiled.Statement? compiledStatement = Compile(statement);
					if (compiledStatement != null)
						compiled.Add(compiledStatement);
				}
			}
			finally
			{
				Symbols.LeaveScope();
			}

			return new Compiled.CodeBlockStatement(compiled);
		}
	}
}

