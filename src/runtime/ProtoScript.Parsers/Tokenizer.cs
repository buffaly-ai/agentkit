using System.Runtime.CompilerServices;

namespace ProtoScript.Parsers
{

	[Serializable]
	public class ProtoScriptTokenizingException : Exception
	{
		public override string Message
		{
			get
			{
				string strResult = "ProtoScript Syntax Error. At " + m_iCursor.ToString() + " \r\n";

				if (null != m_strProtoScript && m_strProtoScript.Length >= m_iCursor)
				{
					int iLine = 1;
					for (int i = 0; i < m_iCursor; i++)
					{
						if (m_strProtoScript[i] == '\n')
							iLine++;
					}


					strResult += "Expected: [" + m_strExpected + "] \r\n";
					strResult += "But saw: " + m_strProtoScript.Substring(m_iCursor, Math.Min(255, m_strProtoScript.Length - m_iCursor)) + "\r\n";
					strResult += "Line: " + iLine + "\r\n";
					strResult += "Preceeding: " + m_strProtoScript.Substring(Math.Max(m_iCursor - 255, 0), Math.Min(255, m_strProtoScript.Length));
				}

				return strResult;
			}
		}

		private string m_strProtoScript;
		private int m_iCursor;
		private string m_strExpected;

		public string File;
		public string Explanation;
		public int Cursor
		{
			get
			{
				return m_iCursor;
			}
		}

		public string Expected
		{
			get
			{
				return m_strExpected;
			}
		}

		public ProtoScriptTokenizingException(string strProtoScript, int iCursor, string strExpected) : base(null)
		{
			m_strProtoScript = strProtoScript;
			m_iCursor = iCursor;
			m_strExpected = strExpected;
		}
		public ProtoScriptTokenizingException(string message) : base(message) { }
		public ProtoScriptTokenizingException(string message, Exception inner) : base(message, inner) { }
		protected ProtoScriptTokenizingException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}


	[Serializable]
	public class ProtoScriptParsingException : ProtoScriptTokenizingException
	{
		public ProtoScriptParsingException(string strProtoScript, int iCursor, string strExpected)
			:
			base(strProtoScript, iCursor, strExpected)
		{
					
		}
		public ProtoScriptParsingException(string strProtoScript, int iCursor, string strExpected, string strExplanation)
			:
			base(strProtoScript, iCursor, strExpected)
		{
			Explanation = strExplanation;
		}
		public ProtoScriptParsingException(string message) : base(message) { }
		public ProtoScriptParsingException(string message, Exception inner) : base(message, inner) { }
		protected ProtoScriptParsingException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}


	[Serializable]
	public class ProtoScriptCompilerException : Exception
	{
		public override string Message
		{
			get
			{
				string strResult = "ProtoScript Compiler Error Error. At " + Cursor.ToString() + " \r\n";

				if (null != m_strProtoScript && m_strProtoScript.Length >= Cursor)
				{
					int iLine = 1;
					for (int i = 0; i < Cursor; i++)
					{
						if (m_strProtoScript[i] == '\n')
							iLine++;
					}


					strResult += "Error: [" + Explanation + "] \r\n";
					if (null != Info)
					{
						strResult += "At: " + m_strProtoScript.Substring(Cursor, Math.Min(255, m_strProtoScript.Length - Cursor)) + "\r\n";
						strResult += "Line: " + iLine + "\r\n";
						strResult += "Expression: " + m_strProtoScript.Substring(Info.StartingOffset, Info.Length) + "\r\n";
						strResult += "Preceeding: " + m_strProtoScript.Substring(Math.Max(Cursor - 255, 0), Math.Min(255, m_strProtoScript.Length));
					}
				}

				return strResult;
			}
		}

		public string m_strProtoScript = string.Empty;

		public StatementParsingInfo Info = null;

		public string File;
		public string Explanation;
		public int Cursor
		{
			get
			{
				return Info?.StartingOffset ?? 0;
			}
		}


		public ProtoScriptCompilerException(StatementParsingInfo info, string strExplanation)
			:		base("ProtoScript Compiler Exception")
		{
			Info = info;
			Explanation = strExplanation;
		}
		public ProtoScriptCompilerException(string message) : base(message) { }
		public ProtoScriptCompilerException(string message, Exception inner) : base(message, inner) { }
		protected ProtoScriptCompilerException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}

	[Serializable]
	public class LookAheadException : Exception
	{
		public LookAheadException() { }
		public LookAheadException(string message) : base(message) { }
		public LookAheadException(string message, Exception inner) : base(message, inner) { }
		protected LookAheadException(
			System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}

       // This is the C#-specific Tokenizer that inherits from BasicUtilities.Tokenizer
       public class Tokenizer : BasicUtilities.Tokenizer
	{
		private readonly HashSet<string> m_setOperators;
		private readonly HashSet<string> m_setUnary;
		private readonly HashSet<string> m_setBinary;

		public Tokenizer(string source) : base(source)
		{
			clearQuotes();  // We'll handle quotes as symbols and parse literals manually
			clearSymbols(); // Clear any base symbols to define our own set

			// Define C#-like operators
			m_setOperators = new HashSet<string>(new[]
			{
				".","!","!=","!==","%","%=","&","&&","&=","(",")", // Corrected "*" to ")"
                "*=","+","++", "+=",",","-","--","-=","/","/=","<","<=",
				"<<","<<=",">",">=",">>", ">>=","?","::",":","^","^=","=","==",
				"|","|=","||","~","=>","??","?.", "->"
			});

			m_setUnary = new HashSet<string>(new[] { "!", "~", "+", "-", "++", "--", "out", "ref", "await" });
			m_setBinary = new HashSet<string>(new[]
			{
				".","!=","!==","%","%=","&","&&","&=","*","*=","+","+=","-","-=","/","/=",
				"<","<=","<<","<<=",">",">=",">>",">>=","^","^=","=","==","|","|=","||",
				"=>","is","as","??","?.","typeof","cast"
			});

			// Add every character from operators as a symbol for the base tokenizer
			foreach (string op in m_setOperators)
			{
				foreach (char c in op)
				{
					insertSymbol(c);
				}
			}

			// Add quotes as symbols so GetNextTokenSpan() returns them individually
			insertSymbol('"');
			insertSymbol('\'');

			// Add other critical single-character tokens
			insertSymbol('@');  // For verbatim strings @"..."
			insertSymbol('$');  // For interpolated strings $"..."
			//insertSymbol('#'); # is not a symbol in ProtoScript, it can be used within an identifier
			insertSymbol('{');
			insertSymbol('}');
			insertSymbol('[');
			insertSymbol(']');
			insertSymbol(';');
			// Note: '(', ')', ',' are already covered as they appear in m_setOperators.
		}

		// Helper method to robustly skip to the end of the current line
		private void SkipToEndOfLine()
		{
			// Assumes m_szCursor is currently positioned *after* the introducer (e.g., after "//" or "#")
			while (hasMoreChars())
			{
				char currentChar = m_memTarget.Span[m_szCursor]; // Peek before consuming
				if (currentChar == '\n')
				{
					m_szCursor++; // Consume '\n'
					break;
				}
				if (currentChar == '\r')
				{
					m_szCursor++; // Consume '\r'
					if (hasMoreChars() && m_memTarget.Span[m_szCursor] == '\n')
					{
						m_szCursor++; // Consume '\n' if it's part of \r\n
					}
					break;
				}
				m_szCursor++; // Consume other characters on the line
			}
		}

		new public string peekNextToken()
		{
			int originalCursor = getCursor();    // Get current cursor from base
			string tokenResult = this.getNextToken(); // Call THIS class's getNextToken(), which handles comments
			setCursor(originalCursor);           // Restore cursor using base method
			return tokenResult;
		}

		new public string getNextToken()
		{
ReadOnlySpan<char> spanTok = GetNextTokenSpan(); // Get basic token from base Tokenizer
			if (spanTok.IsEmpty) return string.Empty;

			char firstCharOfSpan = spanTok[0];
			int startPositionOfToken = getCursor() - spanTok.Length; // Cursor is now after spanTok

			// Verbatim string: @"..."
			if (firstCharOfSpan == '@' && spanTok.Length == 1) // Ensure '@' was a standalone token
			{
ReadOnlySpan<char> nextTokenAfterAt = PeekNextTokenSpan(); // Base PeekNextTokenSpan (doesn't skip C# comments)
				if (!nextTokenAfterAt.IsEmpty && nextTokenAfterAt.Length == 1 && nextTokenAfterAt[0] == '"')
				{
					GetNextTokenSpan(); // Consume the '"' token from base
					ParseVerbatimStringLiteral(); // m_szCursor will be after the closing quote
					return m_strTarget.Substring(startPositionOfToken, getCursor() - startPositionOfToken);
				}
			}

			// Interpolated string: $"..." or $$"..." (basic handling)
			if (firstCharOfSpan == '$' && spanTok.Length == 1) // Ensure '$' was a standalone token
			{
				if (peekNextChar() == '$') // peekNextChar from base
				{
					discardNextChar(); // discardNextChar from base
				}

ReadOnlySpan<char> nextTokenAfterDollar = PeekNextTokenSpan(); // Base PeekNextTokenSpan
				if (!nextTokenAfterDollar.IsEmpty && nextTokenAfterDollar.Length == 1 && nextTokenAfterDollar[0] == '"')
				{
					GetNextTokenSpan(); // Consume the '"' token from base
					ParseStringLiteral();
					return m_strTarget.Substring(startPositionOfToken, getCursor() - startPositionOfToken);
				}
			}

			// Regular string literal: "..."
			if (firstCharOfSpan == '"' && spanTok.Length == 1) // Ensure '"' was a standalone token
			{
				ParseStringLiteral();
				return m_strTarget.Substring(startPositionOfToken, getCursor() - startPositionOfToken);
			}

			// Character literal: '...'
			if (firstCharOfSpan == '\'' && spanTok.Length == 1) // Ensure '\'' was a standalone token
			{
				ParseCharacterLiteral();
				return m_strTarget.Substring(startPositionOfToken, getCursor() - startPositionOfToken);
			}

			string sTok = spanTok.ToString();

			if (m_setOperators.Contains(sTok))
			{
				if (hasMoreChars())
				{
					char nextOpChar = peekNextChar();
					if (m_setOperators.Contains(sTok + nextOpChar))
					{
						sTok += getNextChar();
						if (hasMoreChars())
						{
							char thirdOpChar = peekNextChar();
							if (m_setOperators.Contains(sTok + thirdOpChar))
							{
								sTok += getNextChar();
							}
						}
					}
				}
			}

			// Comment and Preprocessor Handling
			if (sTok == "/")
			{
				char nextChar = peekNextChar();
				if (nextChar == '/')
				{
					discardNextChar(); // Consume the second '/', m_szCursor is now after "//"
					SkipToEndOfLine(); // Use robust line skipper
					return getNextToken(); // Recursively get the next actual token
				}
				if (nextChar == '*')
				{
					discardNextChar(); // Consume the '*', m_szCursor is now after "/*"
movePast("*/");   // Base Tokenizer.movePast for fixed delimiter
					return getNextToken();
				}
			}

			if (sTok == "#") // m_szCursor is already after "#" because GetNextTokenSpan consumed it
			{
				SkipToEndOfLine(); // Use robust line skipper
				return getNextToken();
			}

			return sTok;
		}

		public void MovePastToken(string tok)
		{
			while (hasMoreTokens())
			{
				if (getNextToken() == tok) return;
			}
		}

		new public void movePastWhitespace()
		{
			ReadOnlySpan<char> src = m_memTarget.Span;
			int len = src.Length;

			while (m_szCursor < len)
			{
				char c = src[m_szCursor];
				if (isSpace(c)) { m_szCursor++; continue; }

				if (c == '/')
				{
					if (m_szCursor + 1 < len)
					{
						char nxt = src[m_szCursor + 1];
						if (nxt == '/')
						{
							m_szCursor += 2; // Consume "//"
							SkipToEndOfLine(); // Use robust line skipper
							continue;
						}
						if (nxt == '*')
						{
							m_szCursor += 2; // Consume "/*"
movePast("*/");  // Base Tokenizer.movePast for fixed delimiter
							continue;
						}
					}
				}
				if (c == '#')
				{
					m_szCursor++;   // Consume "#"
					SkipToEndOfLine(); // Use robust line skipper
					continue;
				}
				break;
			}
		}

		private void ParseStringLiteral()
		{
			while (hasMoreChars())
			{
				char c = m_memTarget.Span[m_szCursor];
				if (c == '\\')
				{
					m_szCursor++;
					if (hasMoreChars()) { m_szCursor++; }
					else { throw new ProtoScriptTokenizingException(m_strTarget, getCursor(), "incomplete escape sequence in string literal"); }
				}
				else if (c == '"')
				{
					m_szCursor++;
					return;
				}
				else { m_szCursor++; }
			}
			throw new ProtoScriptTokenizingException(m_strTarget, getCursor(), "unterminated string literal (missing closing '\"')");
		}

		private void ParseVerbatimStringLiteral()
		{
			while (hasMoreChars())
			{
				char c = m_memTarget.Span[m_szCursor];
				if (c == '"')
				{
					m_szCursor++;
					if (hasMoreChars() && m_memTarget.Span[m_szCursor] == '"') { m_szCursor++; }
					else { return; }
				}
				else { m_szCursor++; }
			}
			throw new ProtoScriptTokenizingException(m_strTarget, getCursor(), "unterminated verbatim string literal (missing closing '\"')");
		}

		private void ParseCharacterLiteral()
		{
			if (!hasMoreChars()) { throw new ProtoScriptTokenizingException(m_strTarget, getCursor(), "empty character literal"); }
			char charContentOrEscape = m_memTarget.Span[m_szCursor];
			m_szCursor++;

			if (charContentOrEscape == '\\')
			{
				if (!hasMoreChars()) { throw new ProtoScriptTokenizingException(m_strTarget, getCursor(), "incomplete escape sequence in char literal"); }
				m_szCursor++;
			}

			if (!hasMoreChars() || m_memTarget.Span[m_szCursor] != '\'')
			{
				throw new ProtoScriptTokenizingException(m_strTarget, getCursor(), "unterminated character literal (missing closing \"'\") or too many characters");
			}
			m_szCursor++;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsUnaryOperator(string tok) => m_setUnary.Contains(tok);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsBinaryOperator(string tok) => m_setBinary.Contains(tok);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsOperator(string tok)
		{
			// Comprehensive check for any type of defined operator
			return m_setOperators.Contains(tok) || m_setUnary.Contains(tok) || m_setBinary.Contains(tok);
		}

		public bool CouldBeNext(string strTok)
		{
			return TryConsume(strTok);
		}

		public bool IsNext(string strTok)
		{
			return peekNextToken() == strTok;
		}

		public bool TryConsume(string strTok)
		{
			int saveCursor = getCursor();
			if (getNextToken() == strTok)
			{
				return true;
			}

			setCursor(saveCursor);
			return false;
		}

		public bool TryConsumeIdentifier()
		{
			int saveCursor = getCursor();
			string token = getNextToken();
			if (string.IsNullOrEmpty(token))
			{
				setCursor(saveCursor);
				return false;
			}

			if (IsOperator(token))
			{
				setCursor(saveCursor);
				return false;
			}

			if (token.Length == 1 && isSymbol(token[0]))
			{
				setCursor(saveCursor);
				return false;
			}

			return true;
		}

		public bool MustBeNext(string strTok)
		{
			int saveCursor = getCursor();
			if (!TryConsume(strTok)) { throw new ProtoScriptTokenizingException(getString(), saveCursor, strTok); }
			return true;
		}

		public bool MustBeNextChar(char c)
		{
			int saveCursor = getCursor();
			if (getNextChar() != c) { throw new ProtoScriptTokenizingException(getString(), saveCursor, c.ToString()); }
			return true;
		}
	}
}

