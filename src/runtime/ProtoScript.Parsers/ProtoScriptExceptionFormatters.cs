using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BasicUtilities;
using static Logs;

namespace ProtoScript.Parsers
{
	public class ProtoScriptTokenizingExceptionFormatter : IExceptionFormatter
	{
		public virtual string ToJSON(Exception err)
		{
			ProtoScriptTokenizingException ex = (ProtoScriptTokenizingException)err;
			Dictionary<string, object> data = new Dictionary<string, object>();
			data["Type"] = ex.GetType().FullName;
			data["Message"] = ex.Message;
			data["File"] = ex.File;
			data["Explanation"] = ex.Explanation;
			data["Cursor"] = ex.Cursor;
			data["Expected"] = ex.Expected;
			return JsonSerializer.Serialize(data);
		}

		public virtual string ToPretty(Exception err)
		{
			ProtoScriptTokenizingException ex = (ProtoScriptTokenizingException)err;
			System.Type t = typeof(ProtoScriptTokenizingException);
			FieldInfo scriptField = t.GetField("m_strProtoScript", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo cursorField = t.GetField("m_iCursor", BindingFlags.NonPublic | BindingFlags.Instance);
			string script = scriptField == null ? null : scriptField.GetValue(ex) as string;
			int cursor = cursorField == null ? 0 : (int)cursorField.GetValue(ex);
			if (script == null)
			{
				return ex.Message;
			}
			return CreateSnippet(script, cursor, ex.Explanation);
		}

		protected string CreateSnippet(string script, int cursor, string explanation)
		{
			int start = Math.Max(0, cursor - 50);
			int end = Math.Min(script.Length, cursor + 50);
			string snippet = script.Substring(start, end - start);
			int pointer = cursor - start;
			StringBuilder builder = new StringBuilder();
			if (!string.IsNullOrEmpty(explanation))
			{
				builder.AppendLine(explanation);
			}
			builder.AppendLine(snippet);
			builder.AppendLine(new string(' ', pointer) + "^");
			return builder.ToString();
		}
	}

	public class ProtoScriptParsingExceptionFormatter : ProtoScriptTokenizingExceptionFormatter
	{
		public override string ToJSON(Exception err)
		{
			ProtoScriptParsingException ex = (ProtoScriptParsingException)err;
			Dictionary<string, object> data = new Dictionary<string, object>();
			data["Type"] = ex.GetType().FullName;
			data["Message"] = ex.Message;
			data["File"] = ex.File;
			data["Explanation"] = ex.Explanation;
			data["Cursor"] = ex.Cursor;
			data["Expected"] = ex.Expected;
			return JsonSerializer.Serialize(data);
		}

		public override string ToPretty(Exception err)
		{
			return base.ToPretty(err);
		}
	}

	public class ProtoScriptCompilerExceptionFormatter : IExceptionFormatter
	{
		public string ToJSON(Exception err)
		{
			ProtoScriptCompilerException ex = (ProtoScriptCompilerException)err;
			Dictionary<string, object> data = new Dictionary<string, object>();
			data["Type"] = ex.GetType().FullName;
			data["Message"] = ex.Message;
			data["File"] = ex.File;
			data["Explanation"] = ex.Explanation;
			data["Cursor"] = ex.Cursor;
			if (ex.Info != null)
			{
				data["Start"] = ex.Info.StartingOffset;
				data["Length"] = ex.Info.Length;
			}
			return JsonSerializer.Serialize(data);
		}

		public string ToPretty(Exception err)
		{
			ProtoScriptCompilerException ex = (ProtoScriptCompilerException)err;
			if (ex.m_strProtoScript == null)
			{
				return ex.Message;
			}
			return CreateSnippet(ex.m_strProtoScript, ex.Cursor, ex.Explanation);
		}

		private string CreateSnippet(string script, int cursor, string explanation)
		{
			int start = Math.Max(0, cursor - 50);
			int end = Math.Min(script.Length, cursor + 50);
			string snippet = script.Substring(start, end - start);
			int pointer = cursor - start;
			StringBuilder builder = new StringBuilder();
			if (!string.IsNullOrEmpty(explanation))
			{
				builder.AppendLine(explanation);
			}
			builder.AppendLine(snippet);
			builder.AppendLine(new string(' ', pointer) + "^");
			return builder.ToString();
		}
	}
}
