using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Compilation.Parser
{
	public abstract class TokenizerBase<TToken, TTokenType> where TToken : TokenBase<TTokenType>
	{
		public const char NullChar = '\0';
		private string sourceText = "";

		/// <summary>
		/// Gets the type of the text token.
		/// </summary>
		protected TTokenType TextTokenType { get; }

		/// <summary>
		/// Gets the type of the white space token.
		/// </summary>
		protected TTokenType WhiteSpaceTokenType { get; }

		/// <summary>
		/// Gets or sets the current line number.
		/// </summary>
		protected int CurrentLine { get; private set; }

		/// <summary>
		/// Gets or sets the position on current line.
		/// </summary>
		protected int PositionOnLine { get; private set; }

		/// <summary>
		/// Gets the last token position.
		/// </summary>
		protected int LastTokenPosition { get; private set; }

		/// <summary>
		/// Gets the last token.
		/// </summary>
		protected TToken? LastToken { get; private set; }

		private int position;

		/// <summary>
		/// Gets the distance since last token.
		/// </summary>
		protected int DistanceSinceLastToken
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return position - LastTokenPosition; }
		}

		/// <summary>
		/// Occurs when a token is found.
		/// </summary>
		public event Action<TToken>? TokenFound;

		/// <summary>
		/// Gets the list of tokens.
		/// </summary>
		public List<TToken> Tokens { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenizerBase{TToken, TTokenType}"/> class.
        /// </summary>
        public TokenizerBase(TTokenType textTokenType, TTokenType whiteSpaceTokenType)
        {
            Tokens = new List<TToken>();
            TextTokenType = textTokenType;
            WhiteSpaceTokenType = whiteSpaceTokenType;
        }

        /// <summary>
        /// Performs default tokenizer action
        /// </summary>
        public abstract void Tokenize(string sourceText);

		/// <summary>
		/// Tokenizes the input.
		/// </summary>
		protected bool TokenizeInternal(string sourceText, Func<bool> readFunction)
		{
			this.sourceText = sourceText;

			try
			{
				position = 0;
				CurrentLine = 1;
				PositionOnLine = 0;
				LastToken = null;
				LastTokenPosition = 0;
				Tokens.Clear();

				return readFunction();
			}
			catch (Exception ex) when (ex.Message == "Assertion failed!")
			{
				return false;
			}
		}

		/// <summary>
		/// Skips the whitespace.
		/// </summary>
		protected void SkipWhitespace(bool allowEndLine = true)
		{
			var ch = Peek();
			while (char.IsWhiteSpace(ch) & (allowEndLine || (ch != '\r' && ch != '\n')))
			{
				if (Read() == NullChar)
				{
					break;
				}
				ch = Peek();
			}
			if (DistanceSinceLastToken > 0)
			{
				CreateToken(WhiteSpaceTokenType);
			}
		}

		/// <summary>
		/// Skips the until new line or when it hits the specified stop chars.
		/// When the new line is hit, the method automatically consumes it and creates WhiteSpace token.
		/// When the stopchar is hit, it is not consumed.
		/// </summary>
		protected void ReadTextUntilNewLine(TTokenType tokenType, params char[] stopChars)
		{
			int lastNonwhitespaceDistance = 0;

			while (Peek() != '\r' && Peek() != '\n' && !stopChars.Contains(Peek()))
			{
				lastNonwhitespaceDistance = char.IsWhiteSpace(Peek()) ? lastNonwhitespaceDistance + 1 : 0;
				if (Read() == NullChar)
				{
					break;
				}
			}
			if (DistanceSinceLastToken > 0)
			{
				CreateToken(tokenType, lastNonwhitespaceDistance);
			}


			var hasR = Peek() == '\r';
			if (hasR)
			{
				// \r can be followed by \n which is still one new line
				Read();
			}
			var hasN = Peek() == '\n';
			if (hasN)
			{
				Read();
			}

			if (DistanceSinceLastToken > 0)
			{
				if (lastNonwhitespaceDistance > 0)
					CreateToken(WhiteSpaceTokenType);
				else
					CreateToken(WhiteSpaceTokenType, hasR & hasN ? "\r\n" : hasR ? "\r" : "\n");
			}
		}

		protected bool ReadTextUntil(TTokenType tokenType, string stopString, bool stopOnNewLine, string? nestString = null)
		{
			int nestLevel = 0;
			while (Peek() != NullChar)
			{
				if (PeekIsString(stopString)) nestLevel--;
				if (PeekIsString(nestString)) nestLevel++;
				if (nestLevel < 0)
				{
					CreateToken(tokenType);
					for (int i = 0; i < stopString.Length; i++) Read();
					return true;
				}
				Read();
			}
			return false;
		}

		protected bool PeekIsString(string? str)
		{
			if (str is null) return false;
			return PeekSpan(str.Length).SequenceEqual(str.AsSpan());
		}

		protected string? ReadOneOf(params string[] strings)
		{
			int index = 0;
			while (strings.Length > 0 && !strings.Any(s => s.Length <= index))
			{
				var ch = Peek();
				strings = strings.Where(s => s[index] == ch).ToArray();
				if (strings.Length == 0) return null;
				index++;
				Read();
			}
			return strings.FirstOrDefault(s => s.Length == index);
		}

		protected abstract TToken NewToken(string text, TTokenType type, int lineNumber, int columnNumber, int length, int startPosition);

		protected string GetCurrentTokenText(int charsFromEndToSkip = 0)
		{
			var start = LastTokenPosition;
			var end = position - charsFromEndToSkip;
			var length = end - start;
			Debug.Assert(end <= sourceText.Length, "Tokenizer read out of source string.");

			if (length == 1)
			{
				return sourceText[start].DotvvmInternString();
			}
			else if (length < 20)
				return sourceText.AsSpan().Slice(start, length).DotvvmInternString();
			else
				return sourceText.Substring(start, length);
		}

		/// <summary>
		/// Creates the token.
		/// </summary>
		protected TToken CreateToken(TTokenType type, int charsFromEndToSkip = 0, Func<TToken, TokenError>? errorProvider = null)
		{
			var text = GetCurrentTokenText(charsFromEndToSkip);

			var t = NewToken(text,
							 type,
							 lineNumber: CurrentLine,
							 columnNumber: Math.Max(0, PositionOnLine - DistanceSinceLastToken - 1),
							 length: text.Length,
							 startPosition: LastTokenPosition
					);
			Tokens.Add(t);
			if (errorProvider != null)
			{
				t.Error = errorProvider(t);
			}

			LastTokenPosition = position - charsFromEndToSkip;

			OnTokenFound(t);

			return LastToken = t;
		}

		/// <summary> Slightly optimized version of CreateToken when exact string representation of the token is known at compile-time. </summary>
		protected TToken CreateToken(TTokenType type, string text)
		{
			Debug.Assert(GetCurrentTokenText() == text);
			var t = NewToken(text,
							 type,
							 lineNumber: CurrentLine,
							 columnNumber: Math.Max(0, PositionOnLine - DistanceSinceLastToken - 1),
							 length: text.Length,
							 startPosition: LastTokenPosition
					);
			Tokens.Add(t);

			LastTokenPosition = position;

			OnTokenFound(t);

			return LastToken = t;
		}

		protected TokenError CreateTokenError()
		{
			return new NullTokenError<TToken, TTokenType>(this);
		}

		protected TokenError CreateTokenError(TToken lastToken, TTokenType firstTokenType, string errorMessage, bool isCritical = false)
		{
			return new BeginWithLastTokenOfTypeTokenError<TToken, TTokenType>(errorMessage, this, lastToken, firstTokenType, isCritical);
		}

		protected TokenError CreateTokenError(TToken token, string errorMessage)
		{
			return new SimpleTokenError<TToken, TTokenType>(errorMessage, this, token);
		}

		/// <summary>
		/// Called when a token is found.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void OnTokenFound(TToken token)
		{
			TokenFound?.Invoke(token);
		}

		protected ReadOnlySpan<char> PeekSpan(int length) =>
			sourceText.AsSpan(position, Math.Min(length, sourceText.Length - position));

		/// <summary>
		/// Peeks the current char.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected char Peek() => position < sourceText.Length ? sourceText[position] : NullChar;

		protected char Peek(int delta) => position + delta < sourceText.Length ? sourceText[position + delta] : NullChar;

		/// <summary>
		/// Returns the current char and advances to the next one.
		/// </summary>
		protected char Read()
		{
			var ch = Peek();
			if (ch != NullChar)
			{
				position++;
				if (ch == '\n' || (ch == '\r' && Peek() != '\n'))
				{
					CurrentLine++;
					PositionOnLine = 0;
				}
				PositionOnLine++;
			}

			return ch;
		}
	}
}
