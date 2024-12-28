using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Compilation.Parser;
using DotVVM.Framework.Compilation.Parser.Dothtml.Tokenizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotVVM.Framework.Tests.Parser.Dothtml
{
    [TestClass]
    public class DothtmlTokenizerElementsTests : DothtmlTokenizerTestsBase
    {

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Valid_OpenClose_NoAttributes()
        {
            var input = @"<html>aaa</html>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Valid_SelfClosing_NoAttributes_TextOnEnd()
        {
            var input = @"<html/>aaa";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Valid_SelfClosing_NonQuotedAttribute_TextOnBegin()
        {
            var input = @" tr <html xmlns=hello />";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Valid_NestedTags_MultipleAttributes()
        {
            var input = @"<html><body lang=cs><h1 class=alert size=big>Test</h1>  </body></html>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Valid_SelfClosing_SingleQuotedAttribute()
        {
            var input = @" tr <html xmlns='hello dolly' />";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Valid_SelfClosing_DoubleQuotedAttribute()
        {
            var input = @" tr <html xmlns=""hello dolly"" />";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Valid_SelfClosing_EmptyAttribute()
        {
            var input = @" tr <html xmlns="""" />";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }


        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Valid_AttributeWithoutValue()
        {
            var input = @" tr <input type=""checkbox"" checked />";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_Invalid_OpenBraceInText()
        {
            var input = "inline < script";
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);

            Assert.IsTrue(tokenizer.Tokens.All(t => t.Type == DothtmlTokenType.Text));
            Assert.IsTrue(tokenizer.Tokens.All(t => !t.HasError));
            Assert.AreEqual(string.Concat(tokenizer.Tokens.Select(t => t.Text)), input);
        }

        [TestMethod]
        public void DothtmlTokenizer_Invalid_OpenBraceInTextWithoutSpace()
        {
            var input = "inline <script";
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);

            Assert.IsTrue(tokenizer.Tokens.Any(t => t.HasError));
            Assert.AreEqual(string.Concat(tokenizer.Tokens.Select(t => t.Text)), input);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_InvalidTagName_AnotherTag()
        {
            var input = @"<'<a/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag()
        {
            var input = @"<html";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AnotherTag()
        {
            var input = @"<html<a/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_WhiteSpace()
        {
            var input = @"<html ";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_WhiteSpace_AnotherTag()
        {
            var input = @"<html <a/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AttributeName()
        {
            var input = @"<html attr";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AttributeName_AnotherTag()
        {
            var input = @"<html attr<a/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AttributeName_Equals()
        {
            var input = @"<html attr=";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AttributeName_Equals_AnotherTag()
        {
            var input = @"<html attr=<a/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AttributeName_Equals_Quote()
        {
            var input = @"<html attr='";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AttributeName_Equals_Quote_AnotherTag()
        {
            var input = @"<html attr='<a/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AttributeName_Equals_Quote_Quote()
        {
            var input = @"<html attr=''";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Incomplete_OpenTag_AttributeName_Equals_Quote_Quote_AnotherTag()
        {
            var input = @"<html attr=''<a/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Invalid_ElementName_MissingTagPrefix()
        {
            var input = @"<:name/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Colon, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Invalid_ElementName_MissingTagName()
        {
            var input = @"<prefix:/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Colon, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Invalid_AttributeName_MissingTagPrefix()
        {
            var input = @"<a :name=''/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Colon, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_AttributeNameStartWithSquareBracket()
        {
            var input = "<a [text]=\"'This is a attribute with special characters ' + (show ? 'true' : 'false')\"/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_AttributeNameStartWithRoundBracket()
        {
            var input = "<a (text)=\"'This is a attribute with special characters ' + (show ? 'true' : 'false')\"/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.DoubleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Invalid_AttributeName_MissingTagName()
        {
            var input = @"<a prefix:=''/>";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Colon, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(0, tokenizer.Tokens[i].Length);
            Assert.IsTrue(tokenizer.Tokens[i].HasError);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Equals, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.SingleQuote, tokenizer.Tokens[i++].Type);

            Assert.AreEqual(DothtmlTokenType.Slash, tokenizer.Tokens[i++].Type);
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokenizer.Tokens[i++].Type);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Invalid_OpenTag_OpenTag()
        {
            var input = @"<<";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(tokenizer.Tokens[i].HasError);
                Assert.AreEqual("<", tokenizer.Tokens[i].Text);
                Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
            }
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Invalid_OpenTag_Equals()
        {
            var input = @"<=";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[0].Type);
            Assert.IsTrue(tokenizer.Tokens[0].HasError);
            Assert.AreEqual("<", tokenizer.Tokens[0].Text);

            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[1].Type);
            Assert.AreEqual("=", tokenizer.Tokens[1].Text);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Invalid_CloseTag_TreatedAsText()
        {
            var input = @">";

            // parse
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(input);
            CheckForErrors(tokenizer, input.Length);

            var i = 0;
            Assert.AreEqual(DothtmlTokenType.Text, tokenizer.Tokens[i++].Type);
        }


        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_Script()
        {
            var input = """
                <body>
                <script>
                    const a = '<a href="#" title="<test>">Test</a>'
                    const b = 1<4 && 4>1
                    const c = '</'+'script>'
                    const d = '<style></style>'
                </SCRIPT  >
                <script />
                </body>
                """;

            var tokens = Tokenize(input);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[0].Type);
            Assert.AreEqual("<", tokens[0].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[1].Type);
            Assert.AreEqual("body", tokens[1].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[2].Type);
            Assert.AreEqual(">", tokens[2].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[3].Type);
            Assert.AreEqual("\n", tokens[3].Text);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[4].Type);
            Assert.AreEqual("<", tokens[4].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[5].Type);
            Assert.AreEqual("script", tokens[5].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[6].Type);
            Assert.AreEqual(">", tokens[6].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[7].Type);
            Assert.AreEqual("\n    const a = '<a href=\"#\" title=\"<test>\">Test</a>'\n    const b = 1<4 && 4>1\n    const c = '</'+'script>'\n    const d = '<style></style>'\n", tokens[7].Text);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[8].Type);
            Assert.AreEqual("<", tokens[8].Text);
            
            Assert.AreEqual(DothtmlTokenType.Slash, tokens[9].Type);
            Assert.AreEqual("/", tokens[9].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[10].Type);
            Assert.AreEqual("SCRIPT", tokens[10].Text);
            
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokens[11].Type);
            Assert.AreEqual("  ", tokens[11].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[12].Type);
            Assert.AreEqual(">", tokens[12].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[13].Type);
            Assert.AreEqual("\n", tokens[13].Text);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[14].Type);
            Assert.AreEqual("<", tokens[14].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[15].Type);
            Assert.AreEqual("script", tokens[15].Text);
            
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokens[16].Type);
            Assert.AreEqual(" ", tokens[16].Text);
            
            Assert.AreEqual(DothtmlTokenType.Slash, tokens[17].Type);
            Assert.AreEqual("/", tokens[17].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[18].Type);
            Assert.AreEqual(">", tokens[18].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[19].Type);
            Assert.AreEqual("\n", tokens[19].Text);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[20].Type);
            Assert.AreEqual("<", tokens[20].Text);
            
            Assert.AreEqual(DothtmlTokenType.Slash, tokens[21].Type);
            Assert.AreEqual("/", tokens[21].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[22].Type);
            Assert.AreEqual("body", tokens[22].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[23].Type);
            Assert.AreEqual(">", tokens[23].Text);
        }

        [TestMethod]
        public void DothtmlTokenizer_ElementParsing_HtmlLiteral()
        {
            var input = """
                <body>
                <dot:HtmlLiteral>
                    const a = '<a href="#" title="<test>">Test</a>'
                    const b = 1<4 && 4>1
                    const c = ''
                    const d = '</script> </HtmlLiteral> </cc:HtmlLiteral> </dot:HtmlLiteralOrNo>'
                </doT:htmlliteral  >
                <script />
                </body>
                """;

            var tokens = Tokenize(input);

            // Console.WriteLine(CreateTest(tokens));
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[0].Type);
            Assert.AreEqual("<", tokens[0].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[1].Type);
            Assert.AreEqual("body", tokens[1].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[2].Type);
            Assert.AreEqual(">", tokens[2].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[3].Type);
            Assert.AreEqual("\n", tokens[3].Text);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[4].Type);
            Assert.AreEqual("<", tokens[4].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[5].Type);
            Assert.AreEqual("dot", tokens[5].Text);
            
            Assert.AreEqual(DothtmlTokenType.Colon, tokens[6].Type);
            Assert.AreEqual(":", tokens[6].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[7].Type);
            Assert.AreEqual("HtmlLiteral", tokens[7].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[8].Type);
            Assert.AreEqual(">", tokens[8].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[9].Type);
            Assert.AreEqual("\n    const a = '<a href=\"#\" title=\"<test>\">Test</a>'\n    const b = 1<4 && 4>1\n    const c = ''\n    const d = '</script> </HtmlLiteral> </cc:HtmlLiteral> </dot:HtmlLiteralOrNo>'\n", tokens[9].Text);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[10].Type);
            Assert.AreEqual("<", tokens[10].Text);
            
            Assert.AreEqual(DothtmlTokenType.Slash, tokens[11].Type);
            Assert.AreEqual("/", tokens[11].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[12].Type);
            Assert.AreEqual("doT", tokens[12].Text);
            
            Assert.AreEqual(DothtmlTokenType.Colon, tokens[13].Type);
            Assert.AreEqual(":", tokens[13].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[14].Type);
            Assert.AreEqual("htmlliteral", tokens[14].Text);
            
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokens[15].Type);
            Assert.AreEqual("  ", tokens[15].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[16].Type);
            Assert.AreEqual(">", tokens[16].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[17].Type);
            Assert.AreEqual("\n", tokens[17].Text);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[18].Type);
            Assert.AreEqual("<", tokens[18].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[19].Type);
            Assert.AreEqual("script", tokens[19].Text);
            
            Assert.AreEqual(DothtmlTokenType.WhiteSpace, tokens[20].Type);
            Assert.AreEqual(" ", tokens[20].Text);
            
            Assert.AreEqual(DothtmlTokenType.Slash, tokens[21].Type);
            Assert.AreEqual("/", tokens[21].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[22].Type);
            Assert.AreEqual(">", tokens[22].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[23].Type);
            Assert.AreEqual("\n", tokens[23].Text);
            
            Assert.AreEqual(DothtmlTokenType.OpenTag, tokens[24].Type);
            Assert.AreEqual("<", tokens[24].Text);
            
            Assert.AreEqual(DothtmlTokenType.Slash, tokens[25].Type);
            Assert.AreEqual("/", tokens[25].Text);
            
            Assert.AreEqual(DothtmlTokenType.Text, tokens[26].Type);
            Assert.AreEqual("body", tokens[26].Text);
            
            Assert.AreEqual(DothtmlTokenType.CloseTag, tokens[27].Type);
            Assert.AreEqual(">", tokens[27].Text);
        }
        
    }
}
