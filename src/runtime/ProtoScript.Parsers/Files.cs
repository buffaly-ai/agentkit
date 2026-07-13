using BasicUtilities;

namespace ProtoScript.Parsers
{
	public class Files
	{

		[Serializable]
		public class FileDoesNotExistException : Exception
		{
			public FileDoesNotExistException() { }
			public FileDoesNotExistException(string message) : base("File does not exist: " + message) { }
			public FileDoesNotExistException(string message, Exception inner) : base(message, inner) { }
			protected FileDoesNotExistException(
			  System.Runtime.Serialization.SerializationInfo info,
			  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
		}

		static public string CurrentFile = null;
		static public ProtoScript.File Parse(string strFileName)
		{
			try
			{
				if (Logs.DebugLog.DebugLevel == DebugLog.debug_level_t.VERBOSE)
				{
					Logs.DebugLog.CreateTimer("ProtoScript.Parsers.Files.Parse");
					Logs.DebugLog.WriteEvent("Parsing File", strFileName);
				}

				if (!System.IO.File.Exists(strFileName))
					throw new FileDoesNotExistException(strFileName);

				System.IO.FileInfo info = new System.IO.FileInfo(strFileName);

				string strContents = FileUtil.ReadFile(strFileName);
				Files.CurrentFile = info.FullName;		//use this instead of the strFileName so it resolves relative paths

				File file = ParseFileContents(strContents);

				Files.CurrentFile = null;
				file.Info = info;

				if (Logs.DebugLog.DebugLevel == DebugLog.debug_level_t.VERBOSE)
					Logs.DebugLog.WriteTimer("ProtoScript.Parsers.Files.Parse");

				return file;
			}

			catch (ProtoScriptParsingException err)
			{
				err.File = strFileName;
				throw;
			}
			catch (ProtoScriptTokenizingException err)
			{
				err.File = strFileName;
				throw;
			}			
			catch (Exception)
			{
				//Don't log the error here, let the caller decide
				Logs.DebugLog.WriteEvent("File Parsing Failed", strFileName);
				throw;
			}
		}

		static public ProtoScript.File ParseFileContents(string strContents)
		{
			Tokenizer tok = new Tokenizer(strContents);
			File file = Parse(tok);
			file.RawCode = strContents;
			return file;
		}

		static public ProtoScript.File Parse(Tokenizer tok)
		{
			ProtoScript.File result = new File();
			List<AnnotationExpression> lstAnnotations = new List<AnnotationExpression>();
			

			while (tok.hasMoreTokens())
			{
				string strToken = tok.peekNextToken();

				switch (strToken)
				{
					case "include":
						{
							result.Includes.Add(ProtoScript.Parsers.IncludeStatements.Parse(tok));
							break;
						}

					case "reference":
						{
							result.References.Add(ProtoScript.Parsers.ReferenceStatements.Parse(tok));
							break;
						}

					case "import":
						{
							int saveCursor = tok.getCursor();
							if (ProtoScript.Parsers.IncludeStatements.TryParseImportAsInclude(tok, out ProtoScript.IncludeStatement includeStatement))
							{
								// File-path imports are intentionally rejected to keep import/include contracts explicit.
								throw new ProtoScriptParsingException(
									tok.getString(),
									saveCursor,
									"assembly alias",
									$"Import statements cannot target files. Use include \"{includeStatement.FileName}\"; instead.");
							}
							else
							{
								tok.setCursor(saveCursor);
								result.Imports.Add(ProtoScript.Parsers.ImportStatements.Parse(tok));
							}
							break;
						}

					case "using":
						{
							result.Usings.Add(ProtoScript.Parsers.UsingStatements.Parse(tok));
							break;
						}

					case "namespace":
						{
							NamespaceDefinition nsDef = ProtoScript.Parsers.NamespaceDefinitions.Parse(tok);
							result.Namespaces.Add(nsDef);
							result.Statements.AddRange(nsDef.Statements);
							break;
						}
					case "partial":
					case "prototype":
						{
							PrototypeDefinition protoDef = ProtoScript.Parsers.PrototypeDefinitions.Parse(tok);
							if (lstAnnotations.Count > 0)
							{
								protoDef.Annotations = lstAnnotations;
								lstAnnotations = new List<AnnotationExpression>();
							}
							result.PrototypeDefinitions.Add(protoDef);

							break;
						}
					case "extern":
						{
							if (IsPrototypeDefinitionAhead(tok))
							{
								PrototypeDefinition protoDef = ProtoScript.Parsers.PrototypeDefinitions.Parse(tok);
								if (lstAnnotations.Count > 0)
								{
									protoDef.Annotations = lstAnnotations;
									lstAnnotations = new List<AnnotationExpression>();
								}
								result.PrototypeDefinitions.Add(protoDef);
								break;
							}

							Statement statement = Statements.Parse(tok);
							if (statement is FunctionDefinition && lstAnnotations.Count > 0)
							{
								FunctionDefinition functionDefinition = statement as FunctionDefinition;
								functionDefinition.Annotations = lstAnnotations;
								lstAnnotations = new List<AnnotationExpression>();
							}

							if (statement != null)
							{
								result.Statements.Add(statement);
							}

							break;
						}

					case "[":
						{
							lstAnnotations.Add(ProtoScript.Parsers.AnnotationExpressions.Parse(tok));
							break;
						}
					case "":
						{
							//Comment as the last piece of a file
							tok.getNextToken();
							break;
						}
					default:
						{
							Statement statement = Statements.Parse(tok);
							if (statement is FunctionDefinition && lstAnnotations.Count > 0)
							{
								FunctionDefinition functionDefinition = statement as FunctionDefinition;
								functionDefinition.Annotations = lstAnnotations;
								lstAnnotations = new List<AnnotationExpression>();								
							}

							if (null == statement)
							{

							}
							result.Statements.Add(statement);
							break;
						}
				}
			}

			return result;
		}

		private static bool IsPrototypeDefinitionAhead(Tokenizer tok)
		{
			int saveCursor = tok.getCursor();
			try
			{
				ProtoScript.Parsers.Modifiers.Parse(tok);
				return tok.peekNextToken() == "prototype";
			}
			finally
			{
				tok.setCursor(saveCursor);
			}
		}
	}
}


