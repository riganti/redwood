using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotVVM.Framework.Compilation.Parser;
using DotVVM.Framework.Compilation.Parser.Dothtml.Tokenizer;
using DotVVM.Framework.Controls;

namespace DotVVM.Framework.Tests.Parser.Dothtml
{
    public abstract class DothtmlTokenizerTestsBase
    {
        protected IList<DothtmlToken> Tokenize(string markup)
        {
            var t = new DothtmlTokenizer();
            t.Tokenize(markup);
            return t.Tokens;
        }

        public string CreateTest<TTokenType>(IEnumerable<TokenBase<TTokenType>> tokens)
        {
            var test = new StringBuilder();

            int i = 0;
            foreach (var token in tokens)
            {
                test.AppendLine($"Assert.AreEqual({ typeof(TTokenType).Name }.{ token.Type.ToString() }, tokens[{ i }].Type);");
                if(token.HasError)
                {
                    test.AppendLine($"Assert.IsTrue(tokens[{i}].HasError)");
                }
                test.AppendLine($"Assert.AreEqual({ literal(token.Text) }, tokens[{i}].Text);");
                test.AppendLine();
                i++;
            }
            return test.ToString();

            string literal(string a) => KnockoutHelper.MakeStringLiteral(a, htmlSafe: false);
        }

        protected void CheckForErrors(DothtmlTokenizer tokenizer, int inputLength)
        {
            // check for parsing errors
            if (tokenizer.Tokens.Any(t => t.Length != t.Text.Length))
            {
                throw new Exception("The length of the token does not match with its text content length!");
            }

            // check that the token sequence is complete
            var position = 0;
            foreach (var token in tokenizer.Tokens)
            {
                if (token.StartPosition != position)
                {
                    throw new Exception("The token sequence is not complete!");
                }
                position += token.Length;
            }

            if (position != inputLength)
            {
                throw new Exception("The parser did not finished the file!");
            }
        }
    }
}
