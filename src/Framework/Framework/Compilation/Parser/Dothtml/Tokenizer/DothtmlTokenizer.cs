using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DotVVM.Framework.Compilation.Parser.Binding.Tokenizer;
using DotVVM.Framework.Resources;

namespace DotVVM.Framework.Compilation.Parser.Dothtml.Tokenizer
{
    /// <summary>
    /// Reads the Dothtml content and returns tokens.
    /// </summary>
    public class DothtmlTokenizer : TokenizerBase<DothtmlToken, DothtmlTokenType>
    {
        private readonly DotvvmSyntaxConfiguration config;

        public DothtmlTokenizer(DotvvmSyntaxConfiguration? config = null) : base(DothtmlTokenType.Text, DothtmlTokenType.WhiteSpace)
        {
            this.config = config ?? DotvvmSyntaxConfiguration.Default;
        }

        private static bool IsAllowedAttributeFirstChar(char ch)
        {
            return ch == '_' | ch == '[' | ch == '(';
        }

        private static bool IsAllowedAttributeChar(char ch)
        {
            // codegolfing at this point... nvm, this is a fast way to check if small (< 64) integer is in a set
            // * create a bit mask for the target set
            //     - when needed, shift the integer by an offset to make it fit into the 64 bits
            // ...would be nice if C# did this optimization automatically, but it doesn't
            const int shift = '('; // 40
            Debug.Assert(shift - '_' < 64);
            const ulong magicBitMask = (1L << ':' - shift | 1L << '_' - shift | 1L << '-' - shift | 1L << '.' - shift | 1L << '[' - shift | 1L << ']' - shift | 1L << '(' - shift | 1L << ')' - shift);
            uint c = (uint)ch - shift;
            return c < 63 & 0 != ((1UL << (int)c) & magicBitMask);
        }

        private static bool IsAllowedIdentifierChar(char ch)
        {
            const int shift = '('; // 40
            Debug.Assert(shift - '_' < 64);
            const ulong magicBitMask = (1L << ':' - shift | 1L << '_' - shift | 1L << '-' - shift | 1 << '.' - shift);
            uint c = (uint)ch - shift;
            return c < 63 & 0 != ((1UL << (int)c) & magicBitMask);
        }


        public override void Tokenize(string sourceText)
        {
            TokenizeInternal(sourceText, () => { ReadDocument(); return true; });
        }

        public bool TokenizeBinding(string sourceText, bool usesDoubleBraces = false)
        {
            return TokenizeInternal(sourceText, () => {
                ReadBinding(usesDoubleBraces);
                //Finished?
                Assert(Peek() == NullChar);
                //Properly opened/closed
                Assert((Tokens.FirstOrDefault()?.Length ?? 0) > 0);
                Assert((Tokens.LastOrDefault()?.Length ?? 0) > 0);
                return true;
            });
        }

        /// <summary>
        /// Tokenizes whole dothtml document from start to end.
        /// </summary>
        private void ReadDocument()
        {
            var directivesAllowed = true;

            // read the body
            while (Peek() != NullChar)
            {
                if (Peek() == '@' && directivesAllowed)
                {
                    ReadDirective();
                }
                else if (Peek() == '<')
                {
                    if (DistanceSinceLastToken > 0)
                    {
                        CreateToken(DothtmlTokenType.Text);
                    }

                    // HTML element
                    var elementType = ReadElement();
                    if (elementType != ReadElementType.Comment && elementType != ReadElementType.ServerComment)
                    {
                        directivesAllowed = false;
                    }
                }
                else if (Peek() == '{')
                {
                    // possible binding
                    Read();
                    if (Peek() == '{')
                    {
                        if (DistanceSinceLastToken > 1)
                        {
                            CreateToken(DothtmlTokenType.Text, 1);
                        }

                        // it really is a binding
                        ReadBinding(true);
                    }

                    directivesAllowed = false;
                }
                else
                {
                    if (!char.IsWhiteSpace(Peek()))
                    {
                        directivesAllowed = false;
                    }

                    // text content
                    Read();
                }
            }

            // treat remaining content as text
            if (DistanceSinceLastToken > 0)
            {
                CreateToken(DothtmlTokenType.Text);
            }
        }

        private void ReadDirective()
        {
            if (DistanceSinceLastToken > 0)
            {
                CreateToken(DothtmlTokenType.WhiteSpace);
            }

            // the directive started
            Read();
            CreateToken(DothtmlTokenType.DirectiveStart, "@");

            // identifier
            if (!ReadIdentifier(DothtmlTokenType.DirectiveName))
            {
                CreateToken(DothtmlTokenType.DirectiveName, errorProvider: t => CreateTokenError(t, DothtmlTokenType.DirectiveStart, DothtmlTokenizerErrors.DirectiveNameExpected));
            }
            SkipWhitespace(false);

            if (Peek() is '\r' or '\n' or NullChar)
            {
                // empty value
                CreateToken(DothtmlTokenType.DirectiveValue);
                SkipWhitespace();
                return;
            }

            // whitespace
            if (LastToken!.Type != DothtmlTokenType.WhiteSpace)
            {
                CreateToken(DothtmlTokenType.WhiteSpace, errorProvider: t => CreateTokenError(t, DothtmlTokenType.DirectiveStart, DothtmlTokenizerErrors.DirectiveValueExpected));
            }

            // directive value
            ReadTextUntilNewLine(DothtmlTokenType.DirectiveValue);
        }

        /// <summary>
        /// Reads the identifier.
        /// </summary>
        private bool ReadIdentifier(DothtmlTokenType tokenType, char stopChar = '\0')
        {
            var ch = Peek();
            // read first character
            if ((!char.IsLetter(ch) && ch != '_') | stopChar == ch)
                return false;

            // read identifier
            while ((Char.IsLetterOrDigit(ch) | IsAllowedIdentifierChar(ch)) & stopChar != ch)
            {
                Read();
                ch = Peek();
            }
            CreateToken(tokenType);
            return true;
        }

        /// <summary>
        /// Reads the attribute name.
        /// </summary>
        private bool ReadAttributeName(DothtmlTokenType tokenType, char stopChar)
        {
            var ch = Peek();
            // read first character
            if ((!char.IsLetter(ch) && !IsAllowedAttributeFirstChar(ch)) | stopChar == ch)
                return false;

            // read identifier
            while ((Char.IsLetterOrDigit(ch) | IsAllowedAttributeChar(ch)) & stopChar != ch)
            {
                Read();
                ch = Peek();
            }
            CreateToken(tokenType);
            return true;
        }

        /// <summary>
        /// Reads the element.
        /// </summary>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private ReadElementType ReadElement(bool wasOpenBraceRead = false)
        {
            var isClosingTag = false;

            if (!wasOpenBraceRead)
            {
                // open tag brace
                Assert(Peek() == '<');
                Read();
            }

            var firstChar = Peek();

            if (firstChar == '!' | firstChar == '?' | firstChar == '%')
            {
                return ReadHtmlSpecial(true);
            }

            if (!char.IsLetterOrDigit(firstChar) & firstChar != '/' & firstChar != ':')
            {
                if (char.IsWhiteSpace(firstChar))
                {
                    CreateToken(DothtmlTokenType.Text);
                }
                else
                {
                    CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, "'<' char is not allowed in normal text"));
                }
                return ReadElementType.Error;
            }

            CreateToken(DothtmlTokenType.OpenTag, "<");

            if (firstChar == '/')
            {
                // it is a closing tag
                Read();
                CreateToken(DothtmlTokenType.Slash, "/");
                isClosingTag = true;
            }

            // read tag name
            if (!ReadTagOrAttributeName(isAttributeName: false, out var tagPrefix, out var tagName))
            {
                CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.TagNameExpected));
                CreateToken(DothtmlTokenType.CloseTag, errorProvider: t => CreateTokenError());
                return ReadElementType.Error;
            }

            var tagFullName = tagPrefix is null ? tagName ?? "" : tagPrefix + ":" + tagName;

            // read tag attributes
            SkipWhitespace();
            if (!isClosingTag)
            {
                while (Peek() != '/' && Peek() != '>')
                {
                    if (Peek() == '<')
                    {
                        // comment inside element
                        Read();
                        if (Peek() == '!' || Peek() == '%')
                        {
                            var c = ReadOneOf("!--", "%--");
                            if (c == null) CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, ""));
                            else if (c == "!--") ReadComment();
                            else if (c == "%--") ReadServerComment();
                            else throw new Exception();
                            SkipWhitespace();
                        }
                        else
                        {
                            CreateToken(DothtmlTokenType.CloseTag, charsFromEndToSkip: 1, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.InvalidCharactersInTag, isCritical: true));
                            ReadElement(wasOpenBraceRead: true);
                            return ReadElementType.Error;
                        }
                    }
                    else
                    if (!ReadAttribute())
                    {
                        CreateToken(DothtmlTokenType.CloseTag, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.InvalidCharactersInTag, isCritical: true));
                        return ReadElementType.Error;
                    }
                }
            }

            bool isSelfClosing = false;

            if (Peek() == '/' && !isClosingTag)
            {
                // self closing tag
                Read();
                CreateToken(DothtmlTokenType.Slash, "/");
                isSelfClosing = true;
            }
            if (Peek() != '>')
            {
                // tag is not closed
                CreateToken(DothtmlTokenType.CloseTag, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.TagNotClosed));
                return ReadElementType.Error;
            }

            Read();
            CreateToken(DothtmlTokenType.CloseTag, ">");

            if (!isClosingTag && !isSelfClosing && config.IsRawTextElement(tagFullName))
            {
                // HTML <script>, <style> tags: read content until we find the closing the, i.e. the `</script` sequence
                ReadRawTextTag(tagFullName);
                return ReadElementType.RawTextTag;
            }

            return ReadElementType.ValidTag;
        }

        public enum ReadElementType
        {
            Error,
            ValidTag,
            RawTextTag,
            CData,
            Comment,
            Doctype,
            XmlProcessingInstruction,
            ServerComment
        }

        public void ReadRawTextTag(string name)
        {
            // Read everything as raw text until the matching end tag
            // used to parsing <script>, <style>, <dot:InlineScript>, <dot:HtmlLiteral>
            while (Peek() != NullChar)
            {
                if (PeekIsString("</") &&
                    PeekSpan(name.Length + 2).Slice(2).Equals(name.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
                    !char.IsLetterOrDigit(Peek(name.Length + 2)))
                {
                    CreateToken(DothtmlTokenType.Text);
                    Debug.Assert(Peek() == '<');
                    Read();
                    CreateToken(DothtmlTokenType.OpenTag);

                    Debug.Assert(Peek() == '/');
                    Read();
                    CreateToken(DothtmlTokenType.Slash);

                    if (!ReadTagOrAttributeName(isAttributeName: false, out _, out _))
                    {
                        CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.TagNameExpected));
                    }

                    SkipWhitespace();

                    if (Read() != '>')
                    {
                        CreateToken(DothtmlTokenType.CloseTag, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.TagNotClosed));
                    }
                    else
                    {
                        CreateToken(DothtmlTokenType.CloseTag);
                    }

                    return;
                }
                Read();
            }

            // not terminated

            CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.TagNotClosed));
        }

        public ReadElementType ReadHtmlSpecial(bool openBraceConsumed = false)
        {
            var s = ReadOneOf("![CDATA[", "!--", "!DOCTYPE", "?", "%--");
            if (s == "![CDATA[")
            {
                return ReadCData();
            }
            else if (s == "!--")
            {
                return ReadComment();
            }
            else if (s == "%--")
            {
                return ReadServerComment();
            }
            else if (s == "!DOCTYPE")
            {
                return ReadDoctype();
            }
            else if (s == "?")
            {
                return ReadXmlPI();
            }
            return ReadElementType.Error;
        }

        private ReadElementType ReadCData()
        {
            CreateToken(DothtmlTokenType.OpenCData);
            if (ReadTextUntil(DothtmlTokenType.CDataBody, "]]>", false))
            {
                CreateToken(DothtmlTokenType.CloseCData);
                return ReadElementType.CData;
            }
            else
            {
                CreateToken(DothtmlTokenType.CDataBody, errorProvider: t => CreateTokenError());
                CreateToken(DothtmlTokenType.CloseCData, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenCData, DothtmlTokenizerErrors.CDataNotClosed));
                return ReadElementType.Error;
            }
        }

        private ReadElementType ReadComment()
        {
            CreateToken(DothtmlTokenType.OpenComment, "<!--");
            if (ReadTextUntil(DothtmlTokenType.CommentBody, "-->", false))
            {
                CreateToken(DothtmlTokenType.CloseComment, "-->");
                return ReadElementType.Comment;
            }
            else
            {
                CreateToken(DothtmlTokenType.CommentBody);
                CreateToken(DothtmlTokenType.CloseComment, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenComment, DothtmlTokenizerErrors.CommentNotClosed));
                return ReadElementType.Error;
            }
        }

        private ReadElementType ReadServerComment()
        {
            CreateToken(DothtmlTokenType.OpenServerComment, "<%--");
            if (ReadTextUntil(DothtmlTokenType.CommentBody, "--%>", false, nestString: "<%--"))
            {
                CreateToken(DothtmlTokenType.CloseComment, "--%>");
                return ReadElementType.ServerComment;
            }
            else
            {
                CreateToken(DothtmlTokenType.CommentBody);
                CreateToken(DothtmlTokenType.CloseComment, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenComment, DothtmlTokenizerErrors.CommentNotClosed));
                return ReadElementType.Error;
            }
        }

        private ReadElementType ReadDoctype()
        {
            CreateToken(DothtmlTokenType.OpenDoctype);
            if (ReadTextUntil(DothtmlTokenType.DoctypeBody, ">", true))
            {
                CreateToken(DothtmlTokenType.CloseDoctype);
                return ReadElementType.Doctype;
            }
            else
            {
                CreateToken(DothtmlTokenType.DoctypeBody, errorProvider: t => CreateTokenError());
                CreateToken(DothtmlTokenType.CloseDoctype, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenDoctype, DothtmlTokenizerErrors.DoctypeNotClosed));
                return ReadElementType.Error;
            }
        }

        private ReadElementType ReadXmlPI()
        {
            CreateToken(DothtmlTokenType.OpenXmlProcessingInstruction);
            if (ReadTextUntil(DothtmlTokenType.XmlProcessingInstructionBody, "?>", true))
            {
                CreateToken(DothtmlTokenType.CloseXmlProcessingInstruction);
                return ReadElementType.XmlProcessingInstruction;
            }
            else
            {
                CreateToken(DothtmlTokenType.XmlProcessingInstructionBody, errorProvider: t => CreateTokenError());
                CreateToken(DothtmlTokenType.CloseXmlProcessingInstruction, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenXmlProcessingInstruction, DothtmlTokenizerErrors.XmlProcessingInstructionNotClosed));
                return ReadElementType.Error;
            }
        }

        private void Assert(bool expression)
        {
            if (!expression)
            {
                throw new Exception("Assertion failed!");
            }
        }

        /// <summary>
        /// Reads the name of the tag or attribute.
        /// </summary>
        private bool ReadTagOrAttributeName(bool isAttributeName, out string? prefix, out string? name)
        {
            var readIdentifierFunc = isAttributeName ? (Func<DothtmlTokenType, char, bool>)ReadAttributeName : (Func<DothtmlTokenType, char, bool>)ReadIdentifier;

            if (Peek() != ':')
            {
                // read the identifier
                if (!readIdentifierFunc(DothtmlTokenType.Text, ':'))
                {
                    prefix = name = null;
                    return false;
                }
            }
            else
            {
                // missing tag prefix
                CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.MissingTagPrefix));
            }

            if (Peek() == ':')
            {
                prefix = Tokens[^1].Text;

                Read();
                CreateToken(DothtmlTokenType.Colon, ":");

                if (!readIdentifierFunc(DothtmlTokenType.Text, '\0'))
                {
                    CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenTag, DothtmlTokenizerErrors.MissingTagName));
                    name = null;
                    return true;
                }
                name = Tokens[^1].Text;
            }
            else
            {
                prefix = null;
                name = Tokens[^1].Text;
            }

            SkipWhitespace();
            return true;
        }

        /// <summary>
        /// Reads the attribute of a tag.
        /// </summary>
        private bool ReadAttribute()
        {
            // attribute name
            if (!ReadTagOrAttributeName(isAttributeName: true, out _, out _))
            {
                return false;
            }

            if (Peek() == '=')
            {
                // equals sign
                Read();
                CreateToken(DothtmlTokenType.Equals, "=");
                SkipWhitespace();

                // attribute value
                if (Peek() == '\'' || Peek() == '"')
                {
                    // single or double quotes
                    if (!ReadQuotedAttributeValue())
                    {
                        return false;
                    }
                }
                else
                {
                    // unquoted value
                    if (!ReadUnquotedAttributeValue())
                        return false;
                    SkipWhitespace();
                }
            }

            return true;
        }

        public bool ReadUnquotedAttributeValue()
        {
            if (Peek() == '{')
            {
                ReadBinding(false);
            }
            else
            {
                while (!char.IsWhiteSpace(Peek()) && Peek() != '/' && Peek() != '>')
                {
                    if (Peek() == NullChar || Peek() == '<')
                    {
                        CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError());
                        return false;
                    }
                    Read();
                }
                if (DistanceSinceLastToken == 0)
                {
                    CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, DothtmlTokenType.Text, DothtmlTokenizerErrors.MissingAttributeValue));
                    return false;
                }
                CreateToken(DothtmlTokenType.Text);
            }
            return true;
        }

        /// <summary>
        /// Reads the quoted attribute value.
        /// </summary>
        private bool ReadQuotedAttributeValue()
        {
            // read the beginning quotes
            var quotes = Peek();
            var quotesToken = quotes == '\'' ? DothtmlTokenType.SingleQuote : DothtmlTokenType.DoubleQuote;
            Read();
            CreateToken(quotesToken, quotesToken == DothtmlTokenType.SingleQuote ? "'" : "\"");

            // read value
            if (Peek() == '{')
            {
                // binding
                ReadBinding(false);
            }
            else
            {
                // read text
                while (Peek() != quotes)
                {
                    var ch = Peek();
                    if (ch == NullChar || ch == '<' || ch == '>')
                    {
                        CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError());
                        CreateToken(quotesToken, errorProvider: t => CreateTokenError(t, quotesToken, DothtmlTokenizerErrors.AttributeValueNotClosed));
                        return false;
                    }
                    Read();
                }
                CreateToken(DothtmlTokenType.Text);
            }

            // read the ending quotes
            if (Peek() != quotes)
            {
                CreateToken(quotesToken, errorProvider: t => CreateTokenError(t, quotesToken, DothtmlTokenizerErrors.AttributeValueNotClosed));
            }
            else
            {
                Read();
                CreateToken(quotesToken, quotesToken == DothtmlTokenType.SingleQuote ? "'" : "\"");
                SkipWhitespace();
            }
            return true;
        }

        /// <summary>
        /// Reads the binding.
        /// </summary>
        private void ReadBinding(bool doubleCloseBrace)
        {
            // read open brace
            Assert(Peek() == '{');
            Read();
            if (!doubleCloseBrace && Peek() == '{')
            {
                doubleCloseBrace = true;
                Read();
            }
            CreateToken(DothtmlTokenType.OpenBinding, doubleCloseBrace ? "{{" : "{");
            SkipWhitespace();

            // read binding name
            if (!ReadIdentifier(DothtmlTokenType.Text, ':'))
            {
                CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenBinding, DothtmlTokenizerErrors.BindingInvalidFormat));
            }
            else
            {
                SkipWhitespace();
            }

            // colon
            if (Peek() != ':')
            {
                CreateToken(DothtmlTokenType.Colon, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenBinding, DothtmlTokenizerErrors.BindingInvalidFormat));
            }
            else
            {
                Read();
                CreateToken(DothtmlTokenType.Colon);
                SkipWhitespace();
            }

            // binding value
            if (Peek() == '}')
            {
                CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenBinding, DothtmlTokenizerErrors.BindingInvalidFormat));
            }
            else
            {
                char current;
                while ((current = Peek()) != '}')
                {
                    char next = Peek(delta: 1);
                    if (current == '$' && (next == '"' || next == '\''))
                    {
                        // interpolated string
                        // -- we may need to ignore some curly brackets
                        // -- additionally also some quotes might be ignored
                        BindingTokenizer.ReadInterpolatedString(Peek, Read, out _);
                    }
                    else if (current == '\'' || current == '"')
                    {
                        // string literal - ignore curly braces inside
                        BindingTokenizer.ReadStringLiteral(Peek, Read, out _);
                    }
                    else if (current == NullChar)
                    {
                        CreateToken(DothtmlTokenType.Text, errorProvider: t => CreateTokenError());
                        CreateToken(DothtmlTokenType.CloseBinding, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenBinding, DothtmlTokenizerErrors.BindingNotClosed));
                        return;
                    }
                    else
                    {
                        Read();
                    }
                }
                CreateToken(DothtmlTokenType.Text);
            }

            // close brace
            if (Peek() != '}')
            {
                CreateToken(DothtmlTokenType.CloseBinding, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenBinding, DothtmlTokenizerErrors.BindingNotClosed));
                return;
            }
            Read();

            if (doubleCloseBrace)
            {
                if (Peek() != '}')
                {
                    CreateToken(DothtmlTokenType.CloseBinding, errorProvider: t => CreateTokenError(t, DothtmlTokenType.OpenBinding, DothtmlTokenizerErrors.DoubleBraceBindingNotClosed));
                    return;
                }
                Read();
            }
            CreateToken(DothtmlTokenType.CloseBinding, doubleCloseBrace ? "}}" : "}");
        }

        protected override DothtmlToken NewToken(string text, DothtmlTokenType type, int lineNumber, int columnNumber, int length, int startPosition)
        {
            return new DothtmlToken(text, type, lineNumber, columnNumber, length, startPosition);
        }
    }
}
