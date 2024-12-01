using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using DotVVM.Framework.Binding;
using FastExpressionCompiler;

namespace DotVVM.Framework.Utils
{
    public static class StringUtils
    {
        public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

        public static string Utf8Decode(byte[] bytes) =>
            Utf8.GetString(bytes);
        public static string Utf8Decode(ReadOnlySpan<byte> bytes)
        {
#if DotNetCore
            return Utf8.GetString(bytes);
#else
            unsafe
            {
                fixed (byte* pBytes = bytes)
                {
                    return Utf8.GetString(pBytes, bytes.Length);
                }
            }
#endif
        }
        public static int Utf8Encode(ReadOnlySpan<char> str, Span<byte> bytes)
        {
#if DotNetCore
            return Utf8.GetBytes(str, bytes);
#else
            unsafe
            {
                fixed (byte* pBytes = bytes)
                {
                    fixed (char* pStr = str)
                    {
                        return Utf8.GetBytes(pStr, str.Length, pBytes, bytes.Length);
                    }
                }
            }
#endif
        }
        public static string LimitLength(this string source, int length, string ending = "...")
        {
            if (length < source.Length)
            {
                return source.Substring(0, length - ending.Length) + ending;
            }
            else
            {
                return source;
            }
        }

        public static string CreateString<T>(int length, T state, SpanAction<char, T> action)
        {
#if DotNetCore
            return string.Create(length, state, action);
#else
            var buffer = ArrayPool<char>.Shared.Rent(length);
            try
            {
                action(buffer.AsSpan(0, length), state);
                return new string(buffer, 0, length);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
#endif
        }

        // /// <summary> Appends <paramref name="combiner"/> to each grapheme cluster of the source string. </summary>
        // internal static string AddCombinerToGraphemes(string source, string combiner, bool includeLast = true)
        // {
        //     if (source.Length == 0)
        //         return "";

        //     var buffer = ArrayPool<char>.Shared.Rent(source.Length * (1 + combiner.Length));
        //     var bufferIndex = 0;

        //     var enumerator = StringInfo.GetTextElementEnumerator(source);
        //     // enumerator.ElementIndex points to the element start, but we also need the end index, so the loop is "one element behind" the enumerator
        //     Debug.Assert(enumerator.MoveNext());
        //     var lastIndex = enumerator.ElementIndex;
        //     while (enumerator.MoveNext())
        //     {
        //         var length = enumerator.ElementIndex - lastIndex;
        //         source.AsSpan(lastIndex, length).CopyTo(buffer.AsSpan(bufferIndex));

        //         combiner.CopyTo(0, buffer, bufferIndex + length, combiner.Length);
        //         lastIndex = enumerator.ElementIndex;
        //         bufferIndex += length + combiner.Length;
        //     }

        //     // last element
        //     source.AsSpan(lastIndex).CopyTo(buffer.AsSpan(bufferIndex));
        //     bufferIndex += source.Length - lastIndex;
        //     if (includeLast)
        //     {
        //         combiner.CopyTo(0, buffer, bufferIndex, combiner.Length);
        //         bufferIndex += combiner.Length;
        //     }

        //     var result = new string(buffer, 0, bufferIndex);
        //     ArrayPool<char>.Shared.Return(buffer);
        //     return result;
        // }

        public static string UnicodeUnderline(string source)
        {
            var result = new StringBuilder();
            char zwSpace = '\u200B';
            char lowLine = '\u0332';
            char doubleMacron = '\u035F';
            char macron = '\u0331';

            var enumerator = StringInfo.GetTextElementEnumerator(source);
            bool isFirst = true;
            // result.Append(zwSpace);
            // result.Append(macron);
            while (enumerator.MoveNext())
            {
                var element = enumerator.GetTextElement();
                result.Append(element);

                // we want s͟t͟r͟i͟n͟g not s͟t͟r͟i͟n͟g͟
                var sticksDown = element switch {
                    "q" or "y" or "g" or "p" or "j" => true,
                    _ => false
                };
                if (sticksDown)
                {
                    // result.Append(zwSpace);
                    // result.Append(macron);
                    // ignore, next time put lowLine again
                    isFirst = true;
                }
                else if (isFirst)
                {
                    // result.Append(lowLine);
                    result.Append(doubleMacron);
                    isFirst = false;
                }
                else
                {
                    result.Append(doubleMacron);
                }
            }
            return result.ToString();
        }

        public static string UnicodeBold(string source)
        {
            var decomposed = source.Normalize(NormalizationForm.FormD);
            var result = new StringBuilder();

            foreach (var ch in decomposed)
            {
                if (ch >= 'A' && ch <= 'Z')
                {
                    result.Append("𝗔"[0]); // surrogate pair
                    result.Append((char)("𝗔"[1] + (ch - 'a')));
                }
                else if (ch >= 'a' && ch <= 'z')
                {
                    result.Append("𝗮"[0]); // surrogate pair
                    result.Append((char)("𝗮"[1] + (ch - 'a')));
                }
                else if (ch >= '0' && ch <= '9')
                {
                    result.Append("𝟬"[0]); // surrogate pair
                    result.Append((char)("𝟬"[1] + (ch - 'a')));
                }
                else
                {
                    result.Append(ch);
                }
            }
            return result.ToString();
        }

        public static string PadCenter(string str, int length)
        {
            var charBoundaries = StringInfo.ParseCombiningCharacters(str);
            if (charBoundaries.Length >= length)
                return str;

            return CreateString(str.Length + length - charBoundaries.Length, (str, length, charBoundaries), (span, state) =>
            {
                var (str, length, charBoundaries) = state;
                var start = (length - charBoundaries.Length) / 2;
                span.Slice(0, start).Fill(' ');
                str.AsSpan().CopyTo(span.Slice(start));
                span.Slice(start + str.Length).Fill(' ');
            });
        }

        internal static string DotvvmInternString(this char ch, string? str = null) =>
            ch switch {
                ' ' => " ",
                '!' => "!",
                '"' => "\"",
                '#' => "#",
                '$' => "$",
                '%' => "%",
                '&' => "&",
                '\'' => "'",
                '(' => "(",
                ')' => ")",
                '*' => "*",
                '+' => "+",
                ',' => ",",
                '-' => "-",
                '.' => ".",
                '/' => "/",
                '0' => "0",
                '1' => "1",
                '2' => "2",
                '3' => "3",
                '4' => "4",
                '5' => "5",
                '6' => "6",
                '7' => "7",
                '8' => "8",
                '9' => "9",
                ':' => ":",
                ';' => ";",
                '<' => "<",
                '=' => "=",
                '>' => ">",
                '?' => "?",
                '@' => "@",
                'A' => "A",
                'B' => "B",
                'C' => "C",
                'D' => "D",
                'E' => "E",
                'F' => "F",
                'G' => "G",
                'H' => "H",
                'I' => "I",
                'J' => "J",
                'K' => "K",
                'L' => "L",
                'M' => "M",
                'N' => "N",
                'O' => "O",
                'P' => "P",
                'Q' => "Q",
                'R' => "R",
                'S' => "S",
                'T' => "T",
                'U' => "U",
                'V' => "V",
                'W' => "W",
                'X' => "X",
                'Y' => "Y",
                'Z' => "Z",
                '[' => "[",
                '\\' => "\\",
                ']' => "]",
                '^' => "^",
                '_' => "_",
                '`' => "`",
                'a' => "a",
                'b' => "b",
                'c' => "c",
                'd' => "d",
                'e' => "e",
                'f' => "f",
                'g' => "g",
                'h' => "h",
                'i' => "i",
                'j' => "j",
                'k' => "k",
                'l' => "l",
                'm' => "m",
                'n' => "n",
                'o' => "o",
                'p' => "p",
                'q' => "q",
                'r' => "r",
                's' => "s",
                't' => "t",
                'u' => "u",
                'v' => "v",
                'w' => "w",
                'x' => "x",
                'y' => "y",
                'z' => "z",
                '{' => "{",
                '|' => "|",
                '}' => "}",
                '~' => "~",
                '\n' => "\n",
                // although .NET intern is quite slow, there is just no reason to have multiple instances of the same character
                _ => string.Intern(str ?? ch.ToString())
            };

        internal static string DotvvmInternString(this string str, bool trySystemIntern = false)
        {
            var x = str.AsSpan().DotvvmTryInternString();
            if (x is {})
                return x;
            if (trySystemIntern)
                return string.IsInterned(str) ?? str;
            else
                return str;
        }

        public static void GenerateCode()
        {
            // just run dotnet dump and then `dumpheap -stat -strings` to see what is there suspiciously often...
            var strings = new List<string> {
                // common tag prefixes
                "dot", "bp", "bs", "dc", "cc",

                // some JS fragment occuring often
                "Items()?.length", ".Items()?.length", ".$index()", "$index()", "value", "dotvvm.postBack(", "ko.pureComputed(()=>", "viewModel", "dotvvm.evaluator.wrapObservable(()=>", "$type", "$data", ".$data", "(()=>{let vm=", ",[],", "dotvvm.globalize.bindingNumberToString(", ".Name", ".Id", "(i)=>ko.unwrap(i).Id()", "(i)=>ko.unwrap(i).Text()", "(i)=>ko.unwrap(i).Name()", "(async ()=>{let vm=", ".Visible", ".Enabled", "ko.contextFor(", "import", "service",

                // bindings
                "value", "resource", "command", "controlCommand", "staticCommand", "staticCommand", "{{", "}}",

                // html attributes
                "accept", "accept-charset", "accesskey", "action", "align", "allow", "alt", "async", "autocapitalize", "autocomplete", "autofocus", "autoplay", "background", "bgcolor", "border", "buffered", "capture", "challenge", "charset", "checked", "cite", "class", "code", "codebase", "color", "cols", "colspan", "content", "contenteditable", "contextmenu", "controls", "coords", "crossorigin", "csp", "data", "datetime", "decoding", "default", "defer", "dir", "dirname", "disabled", "download", "draggable", "enctype", "enterkeyhint", "for", "form", "formaction", "formenctype", "formmethod", "formnovalidate", "formtarget", "headers", "height", "hidden", "high", "href", "hreflang", "http-equiv", "icon", "id", "importance", "integrity", "intrinsicsize", "inputmode", "ismap", "itemprop", "keytype", "kind", "label", "lang", "language", "loading", "list", "loop", "low", "manifest", "max", "maxlength", "minlength", "media", "method", "min", "multiple", "muted", "name", "novalidate", "open", "optimum", "pattern", "ping", "placeholder", "poster", "preload", "radiogroup", "readonly", "referrerpolicy", "rel", "required", "reversed", "rows", "rowspan", "sandbox", "scope", "scoped", "selected", "shape", "size", "sizes", "slot", "span", "spellcheck", "src", "srcdoc", "srclang", "srcset", "start", "step", "style", "summary", "tabindex", "target", "title", "translate", "type", "usemap", "value", "width", "wrap",

                "data-ui", "data-bind",
                "a", "abbr", "acronym", "address", "applet", "area", "article", "aside", "audio", "b", "base", "basefont", "bdi", "bdo", "big", "blockquote", "body", "br", "button", "canvas", "caption", "center", "cite", "code", "col", "colgroup", "data", "datalist", "dd", "del", "details", "dfn", "dialog", "dir", "div", "dl", "dt", "em", "embed", "fieldset", "figcaption", "figure", "font", "footer", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hr", "html", "i", "iframe", "img", "input", "ins", "kbd", "label", "legend", "li", "link", "main", "map", "mark", "meta", "meter", "nav", "noframes", "noscript", "object", "ol", "optgroup", "option", "output", "p", "param", "picture", "pre", "progress", "q", "rp", "rt", "ruby", "s", "samp", "script", "section", "select", "small", "source", "span", "strike", "strong", "style", "sub", "summary", "sup", "svg", "table", "tbody", "td", "template", "textarea", "tfoot", "th", "thead", "time", "title", "tr", "track", "tt", "u", "ul", "var", "video", "wbr",

                // random fragments

                "http://www.w3.org/1999/xhtml", "utf-8", "viewport", "description", "name", "url",

                "Load", "Init", "PreRender", "ToString", "Equals", "Context", "DotVVM", "en",
                "_this", "_parent", "_control", "_root",
                "PostBack.Handlers",

                "true", "false",
                "  ", "\n\n", "\r\n", "\r\n    ", "\n   ", "\r\n        ", "\n       ", "\r\n            ", "\n           ",
            };

            strings.AddRange(Enumerable.Range(0, 20).Select(x => "c" + x));

            strings.AddRange(DotvvmProperty.AllProperties.Select(x => x.Name));
            strings.AddRange(DotvvmProperty.AllProperties.Select(x => x.DeclaringType.Name));

            var lenGroups = strings.Where(l => l.Length > 1).Distinct().GroupBy(x => x.Length).OrderBy(k => k.Key);
            Console.WriteLine("switch (span.Length)\n{");
            const int manualSequenceEqualThreshold = 5;
            foreach (var lenGroup in lenGroups)
            {
                Console.WriteLine($"    case {lenGroup.Key}: {{");
                if (lenGroup.Key <= manualSequenceEqualThreshold)
                {
                    for (int i = 1; i < lenGroup.Key; i++)
                        Console.WriteLine($"        char ch{i} = span[{i}];");
                }
                Console.WriteLine( "        switch (ch)");
                Console.WriteLine( "        {");
                var gs = lenGroup.GroupBy(x => x[0]).OrderBy(k => k.Key);
                foreach (var g in gs)
                {
                    var ch = formatChar(g.Key);
                    Console.WriteLine($"            case '{ch}':");
                    if (lenGroup.Key <= manualSequenceEqualThreshold)
                    {
                        foreach (var str in g)
                        {
                            var charCmp = str.Skip(1).Select((c, i) => $"ch{i+1} == '{formatChar(c)}'").StringJoin(" & ");
                            Console.WriteLine($"                if ({charCmp})");
                            Console.WriteLine($"                    return {str.ToCode()};");

                        }
                        Console.WriteLine( "                break;");
                    }
                    else
                    {
                        var expr = g.Select(c => $"SpanEq(span, {c.ToCode()})").StringJoin(" ??\n                    ");
                        Console.WriteLine($"                return {expr};");
                    }

                }
                Console.WriteLine( "        }");
                Console.WriteLine( "        break;");
                Console.WriteLine( "    }");
            }
            Console.WriteLine("}");

            string formatChar(char ch) =>
                ch switch {
                    '\r' => "\\r",
                    '\n' => "\\n",
                    '\t' => "\\t",
                    '\\' => "\\\\",
                    '\'' => "\\'",
                    _ => ch.ToString()
                };
        }
        internal static string DotvvmInternString(this ReadOnlySpan<char> span, string? str, bool trySystemIntern = false)
        {
            var x = span.DotvvmTryInternString();
            if (x is {}) return x;
            x = str ?? SpanToString(span);
            return trySystemIntern ? string.IsInterned(x) ?? x : x;
        }
        internal static string DotvvmInternString(this ReadOnlySpan<char> span, string? str = null)
        {
            return DotvvmTryInternString(span) ?? str ?? SpanToString(span);
        }

        static string SpanToString(ReadOnlySpan<char> span) =>
#if NoSpan
            span.ToString();
#else
            new string(span);
#endif

        static string? SpanEq(ReadOnlySpan<char> ch, String s)
        {
            if (ch.SequenceEqual(s.AsSpan()))
                return s;
            return null;
        }

        internal static string? DotvvmTryInternString(this ReadOnlySpan<char> span)
        {
            if (span.Length == 0)
                return "";

            var ch = span[0];

            if (span.Length == 1)
                return ch.DotvvmInternString();
switch (span.Length)
{
    case 2: {
        char ch1 = span[1];
        switch (ch)
        {
            case '\n':
                if (ch1 == '\n')
                    return "\n\n";
                break;
            case '\r':
                if (ch1 == '\n')
                    return "\r\n";
                break;
            case ' ':
                if (ch1 == ' ')
                    return "  ";
                break;
            case 'I':
                if (ch1 == 'D')
                    return "ID";
                break;
            case 'O':
                if (ch1 == 'k')
                    return "Ok";
                break;
            case 'b':
                if (ch1 == 'p')
                    return "bp";
                if (ch1 == 's')
                    return "bs";
                if (ch1 == 'r')
                    return "br";
                break;
            case 'c':
                if (ch1 == 'c')
                    return "cc";
                if (ch1 == '0')
                    return "c0";
                if (ch1 == '1')
                    return "c1";
                if (ch1 == '2')
                    return "c2";
                if (ch1 == '3')
                    return "c3";
                if (ch1 == '4')
                    return "c4";
                if (ch1 == '5')
                    return "c5";
                if (ch1 == '6')
                    return "c6";
                if (ch1 == '7')
                    return "c7";
                if (ch1 == '8')
                    return "c8";
                if (ch1 == '9')
                    return "c9";
                break;
            case 'd':
                if (ch1 == 'c')
                    return "dc";
                if (ch1 == 'd')
                    return "dd";
                if (ch1 == 'l')
                    return "dl";
                if (ch1 == 't')
                    return "dt";
                break;
            case 'e':
                if (ch1 == 'm')
                    return "em";
                if (ch1 == 'n')
                    return "en";
                break;
            case 'h':
                if (ch1 == '1')
                    return "h1";
                if (ch1 == '2')
                    return "h2";
                if (ch1 == '3')
                    return "h3";
                if (ch1 == '4')
                    return "h4";
                if (ch1 == '5')
                    return "h5";
                if (ch1 == '6')
                    return "h6";
                if (ch1 == 'r')
                    return "hr";
                break;
            case 'i':
                if (ch1 == 'd')
                    return "id";
                break;
            case 'l':
                if (ch1 == 'i')
                    return "li";
                break;
            case 'o':
                if (ch1 == 'l')
                    return "ol";
                break;
            case 'r':
                if (ch1 == 'p')
                    return "rp";
                if (ch1 == 't')
                    return "rt";
                break;
            case 't':
                if (ch1 == 'd')
                    return "td";
                if (ch1 == 'h')
                    return "th";
                if (ch1 == 'r')
                    return "tr";
                if (ch1 == 't')
                    return "tt";
                break;
            case 'u':
                if (ch1 == 'l')
                    return "ul";
                break;
            case '{':
                if (ch1 == '{')
                    return "{{";
                break;
            case '}':
                if (ch1 == '}')
                    return "}}";
                break;
        }
        break;
    }
    case 3: {
        char ch1 = span[1];
        char ch2 = span[2];
        switch (ch)
        {
            case '.':
                if (ch1 == 'I' & ch2 == 'd')
                    return ".Id";
                break;
            case 'R':
                if (ch1 == 'o' & ch2 == 'w')
                    return "Row";
                break;
            case 'T':
                if (ch1 == 'a' & ch2 == 'g')
                    return "Tag";
                break;
            case 'a':
                if (ch1 == 'l' & ch2 == 't')
                    return "alt";
                break;
            case 'b':
                if (ch1 == 'd' & ch2 == 'i')
                    return "bdi";
                if (ch1 == 'd' & ch2 == 'o')
                    return "bdo";
                if (ch1 == 'i' & ch2 == 'g')
                    return "big";
                break;
            case 'c':
                if (ch1 == 's' & ch2 == 'p')
                    return "csp";
                if (ch1 == 'o' & ch2 == 'l')
                    return "col";
                if (ch1 == '1' & ch2 == '0')
                    return "c10";
                if (ch1 == '1' & ch2 == '1')
                    return "c11";
                if (ch1 == '1' & ch2 == '2')
                    return "c12";
                if (ch1 == '1' & ch2 == '3')
                    return "c13";
                if (ch1 == '1' & ch2 == '4')
                    return "c14";
                if (ch1 == '1' & ch2 == '5')
                    return "c15";
                if (ch1 == '1' & ch2 == '6')
                    return "c16";
                if (ch1 == '1' & ch2 == '7')
                    return "c17";
                if (ch1 == '1' & ch2 == '8')
                    return "c18";
                if (ch1 == '1' & ch2 == '9')
                    return "c19";
                break;
            case 'd':
                if (ch1 == 'o' & ch2 == 't')
                    return "dot";
                if (ch1 == 'i' & ch2 == 'r')
                    return "dir";
                if (ch1 == 'e' & ch2 == 'l')
                    return "del";
                if (ch1 == 'f' & ch2 == 'n')
                    return "dfn";
                if (ch1 == 'i' & ch2 == 'v')
                    return "div";
                break;
            case 'f':
                if (ch1 == 'o' & ch2 == 'r')
                    return "for";
                break;
            case 'i':
                if (ch1 == 'm' & ch2 == 'g')
                    return "img";
                if (ch1 == 'n' & ch2 == 's')
                    return "ins";
                break;
            case 'k':
                if (ch1 == 'b' & ch2 == 'd')
                    return "kbd";
                break;
            case 'l':
                if (ch1 == 'o' & ch2 == 'w')
                    return "low";
                break;
            case 'm':
                if (ch1 == 'a' & ch2 == 'x')
                    return "max";
                if (ch1 == 'i' & ch2 == 'n')
                    return "min";
                if (ch1 == 'a' & ch2 == 'p')
                    return "map";
                break;
            case 'n':
                if (ch1 == 'a' & ch2 == 'v')
                    return "nav";
                break;
            case 'p':
                if (ch1 == 'r' & ch2 == 'e')
                    return "pre";
                break;
            case 'r':
                if (ch1 == 'e' & ch2 == 'l')
                    return "rel";
                break;
            case 's':
                if (ch1 == 'r' & ch2 == 'c')
                    return "src";
                if (ch1 == 'u' & ch2 == 'b')
                    return "sub";
                if (ch1 == 'u' & ch2 == 'p')
                    return "sup";
                if (ch1 == 'v' & ch2 == 'g')
                    return "svg";
                break;
            case 'u':
                if (ch1 == 'r' & ch2 == 'l')
                    return "url";
                break;
            case 'v':
                if (ch1 == 'a' & ch2 == 'r')
                    return "var";
                break;
            case 'w':
                if (ch1 == 'b' & ch2 == 'r')
                    return "wbr";
                break;
        }
        break;
    }
    case 4: {
        char ch1 = span[1];
        char ch2 = span[2];
        char ch3 = span[3];
        switch (ch)
        {
            case '\n':
                if (ch1 == ' ' & ch2 == ' ' & ch3 == ' ')
                    return "\n   ";
                break;
            case ',':
                if (ch1 == '[' & ch2 == ']' & ch3 == ',')
                    return ",[],";
                break;
            case 'D':
                if (ch1 == 'a' & ch2 == 't' & ch3 == 'a')
                    return "Data";
                if (ch1 == 'a' & ch2 == 't' & ch3 == 'e')
                    return "Date";
                break;
            case 'E':
                if (ch1 == 'd' & ch2 == 'i' & ch3 == 't')
                    return "Edit";
                break;
            case 'H':
                if (ch1 == 't' & ch2 == 'm' & ch3 == 'l')
                    return "Html";
                break;
            case 'I':
                if (ch1 == 'n' & ch2 == 'i' & ch3 == 't')
                    return "Init";
                if (ch1 == 't' & ch2 == 'e' & ch3 == 'm')
                    return "Item";
                break;
            case 'L':
                if (ch1 == 'o' & ch2 == 'a' & ch3 == 'd')
                    return "Load";
                break;
            case 'M':
                if (ch1 == 'o' & ch2 == 'd' & ch3 == 'e')
                    return "Mode";
                break;
            case 'N':
                if (ch1 == 'a' & ch2 == 'm' & ch3 == 'e')
                    return "Name";
                break;
            case 'R':
                if (ch1 == 'o' & ch2 == 'w' & ch3 == '2')
                    return "Row2";
                break;
            case 'S':
                if (ch1 == 'i' & ch2 == 'z' & ch3 == 'e')
                    return "Size";
                break;
            case 'T':
                if (ch1 == 'e' & ch2 == 'x' & ch3 == 't')
                    return "Text";
                if (ch1 == 'r' & ch2 == 'a' & ch3 == 'p')
                    return "Trap";
                if (ch1 == 'y' & ch2 == 'p' & ch3 == 'e')
                    return "Type";
                break;
            case 'a':
                if (ch1 == 'b' & ch2 == 'b' & ch3 == 'r')
                    return "abbr";
                if (ch1 == 'r' & ch2 == 'e' & ch3 == 'a')
                    return "area";
                break;
            case 'b':
                if (ch1 == 'a' & ch2 == 's' & ch3 == 'e')
                    return "base";
                if (ch1 == 'o' & ch2 == 'd' & ch3 == 'y')
                    return "body";
                break;
            case 'c':
                if (ch1 == 'i' & ch2 == 't' & ch3 == 'e')
                    return "cite";
                if (ch1 == 'o' & ch2 == 'd' & ch3 == 'e')
                    return "code";
                if (ch1 == 'o' & ch2 == 'l' & ch3 == 's')
                    return "cols";
                break;
            case 'd':
                if (ch1 == 'a' & ch2 == 't' & ch3 == 'a')
                    return "data";
                break;
            case 'f':
                if (ch1 == 'o' & ch2 == 'r' & ch3 == 'm')
                    return "form";
                if (ch1 == 'o' & ch2 == 'n' & ch3 == 't')
                    return "font";
                break;
            case 'h':
                if (ch1 == 'i' & ch2 == 'g' & ch3 == 'h')
                    return "high";
                if (ch1 == 'r' & ch2 == 'e' & ch3 == 'f')
                    return "href";
                if (ch1 == 'e' & ch2 == 'a' & ch3 == 'd')
                    return "head";
                if (ch1 == 't' & ch2 == 'm' & ch3 == 'l')
                    return "html";
                break;
            case 'i':
                if (ch1 == 'c' & ch2 == 'o' & ch3 == 'n')
                    return "icon";
                break;
            case 'k':
                if (ch1 == 'i' & ch2 == 'n' & ch3 == 'd')
                    return "kind";
                break;
            case 'l':
                if (ch1 == 'a' & ch2 == 'n' & ch3 == 'g')
                    return "lang";
                if (ch1 == 'i' & ch2 == 's' & ch3 == 't')
                    return "list";
                if (ch1 == 'o' & ch2 == 'o' & ch3 == 'p')
                    return "loop";
                if (ch1 == 'i' & ch2 == 'n' & ch3 == 'k')
                    return "link";
                break;
            case 'm':
                if (ch1 == 'a' & ch2 == 'i' & ch3 == 'n')
                    return "main";
                if (ch1 == 'a' & ch2 == 'r' & ch3 == 'k')
                    return "mark";
                if (ch1 == 'e' & ch2 == 't' & ch3 == 'a')
                    return "meta";
                break;
            case 'n':
                if (ch1 == 'a' & ch2 == 'm' & ch3 == 'e')
                    return "name";
                break;
            case 'o':
                if (ch1 == 'p' & ch2 == 'e' & ch3 == 'n')
                    return "open";
                break;
            case 'p':
                if (ch1 == 'i' & ch2 == 'n' & ch3 == 'g')
                    return "ping";
                break;
            case 'r':
                if (ch1 == 'o' & ch2 == 'w' & ch3 == 's')
                    return "rows";
                if (ch1 == 'u' & ch2 == 'b' & ch3 == 'y')
                    return "ruby";
                break;
            case 's':
                if (ch1 == 'i' & ch2 == 'z' & ch3 == 'e')
                    return "size";
                if (ch1 == 'l' & ch2 == 'o' & ch3 == 't')
                    return "slot";
                if (ch1 == 'p' & ch2 == 'a' & ch3 == 'n')
                    return "span";
                if (ch1 == 't' & ch2 == 'e' & ch3 == 'p')
                    return "step";
                if (ch1 == 'a' & ch2 == 'm' & ch3 == 'p')
                    return "samp";
                break;
            case 't':
                if (ch1 == 'y' & ch2 == 'p' & ch3 == 'e')
                    return "type";
                if (ch1 == 'i' & ch2 == 'm' & ch3 == 'e')
                    return "time";
                if (ch1 == 'r' & ch2 == 'u' & ch3 == 'e')
                    return "true";
                break;
            case 'w':
                if (ch1 == 'r' & ch2 == 'a' & ch3 == 'p')
                    return "wrap";
                break;
        }
        break;
    }
    case 5: {
        char ch1 = span[1];
        char ch2 = span[2];
        char ch3 = span[3];
        char ch4 = span[4];
        switch (ch)
        {
            case '$':
                if (ch1 == 't' & ch2 == 'y' & ch3 == 'p' & ch4 == 'e')
                    return "$type";
                if (ch1 == 'd' & ch2 == 'a' & ch3 == 't' & ch4 == 'a')
                    return "$data";
                break;
            case '.':
                if (ch1 == 'N' & ch2 == 'a' & ch3 == 'm' & ch4 == 'e')
                    return ".Name";
                break;
            case 'A':
                if (ch1 == 'd' & ch2 == 'd' & ch3 == 'e' & ch4 == 'd')
                    return "Added";
                break;
            case 'C':
                if (ch1 == 'l' & ch2 == 'a' & ch3 == 'i' & ch4 == 'm')
                    return "Claim";
                if (ch1 == 'l' & ch2 == 'i' & ch3 == 'c' & ch4 == 'k')
                    return "Click";
                break;
            case 'D':
                if (ch1 == 'e' & ch2 == 'l' & ch3 == 'a' & ch4 == 'y')
                    return "Delay";
                break;
            case 'F':
                if (ch1 == 'i' & ch2 == 'l' & ch3 == 'e' & ch4 == 's')
                    return "Files";
                break;
            case 'L':
                if (ch1 == 'a' & ch2 == 'b' & ch3 == 'e' & ch4 == 'l')
                    return "Label";
                break;
            case 'M':
                if (ch1 == 'o' & ch2 == 'd' & ch3 == 'e' & ch4 == 'l')
                    return "Model";
                break;
            case 'R':
                if (ch1 == 'o' & ch2 == 'l' & ch3 == 'e' & ch4 == 's')
                    return "Roles";
                break;
            case 'V':
                if (ch1 == 'a' & ch2 == 'l' & ch3 == 'u' & ch4 == 'e')
                    return "Value";
                break;
            case 'W':
                if (ch1 == 'i' & ch2 == 'd' & ch3 == 't' & ch4 == 'h')
                    return "Width";
                break;
            case '_':
                if (ch1 == 't' & ch2 == 'h' & ch3 == 'i' & ch4 == 's')
                    return "_this";
                if (ch1 == 'r' & ch2 == 'o' & ch3 == 'o' & ch4 == 't')
                    return "_root";
                break;
            case 'a':
                if (ch1 == 'l' & ch2 == 'i' & ch3 == 'g' & ch4 == 'n')
                    return "align";
                if (ch1 == 'l' & ch2 == 'l' & ch3 == 'o' & ch4 == 'w')
                    return "allow";
                if (ch1 == 's' & ch2 == 'y' & ch3 == 'n' & ch4 == 'c')
                    return "async";
                if (ch1 == 's' & ch2 == 'i' & ch3 == 'd' & ch4 == 'e')
                    return "aside";
                if (ch1 == 'u' & ch2 == 'd' & ch3 == 'i' & ch4 == 'o')
                    return "audio";
                break;
            case 'c':
                if (ch1 == 'l' & ch2 == 'a' & ch3 == 's' & ch4 == 's')
                    return "class";
                if (ch1 == 'o' & ch2 == 'l' & ch3 == 'o' & ch4 == 'r')
                    return "color";
                break;
            case 'd':
                if (ch1 == 'e' & ch2 == 'f' & ch3 == 'e' & ch4 == 'r')
                    return "defer";
                break;
            case 'e':
                if (ch1 == 'm' & ch2 == 'b' & ch3 == 'e' & ch4 == 'd')
                    return "embed";
                break;
            case 'f':
                if (ch1 == 'r' & ch2 == 'a' & ch3 == 'm' & ch4 == 'e')
                    return "frame";
                if (ch1 == 'a' & ch2 == 'l' & ch3 == 's' & ch4 == 'e')
                    return "false";
                break;
            case 'i':
                if (ch1 == 's' & ch2 == 'm' & ch3 == 'a' & ch4 == 'p')
                    return "ismap";
                if (ch1 == 'n' & ch2 == 'p' & ch3 == 'u' & ch4 == 't')
                    return "input";
                break;
            case 'l':
                if (ch1 == 'a' & ch2 == 'b' & ch3 == 'e' & ch4 == 'l')
                    return "label";
                break;
            case 'm':
                if (ch1 == 'e' & ch2 == 'd' & ch3 == 'i' & ch4 == 'a')
                    return "media";
                if (ch1 == 'u' & ch2 == 't' & ch3 == 'e' & ch4 == 'd')
                    return "muted";
                if (ch1 == 'e' & ch2 == 't' & ch3 == 'e' & ch4 == 'r')
                    return "meter";
                break;
            case 'p':
                if (ch1 == 'a' & ch2 == 'r' & ch3 == 'a' & ch4 == 'm')
                    return "param";
                break;
            case 's':
                if (ch1 == 'c' & ch2 == 'o' & ch3 == 'p' & ch4 == 'e')
                    return "scope";
                if (ch1 == 'h' & ch2 == 'a' & ch3 == 'p' & ch4 == 'e')
                    return "shape";
                if (ch1 == 'i' & ch2 == 'z' & ch3 == 'e' & ch4 == 's')
                    return "sizes";
                if (ch1 == 't' & ch2 == 'a' & ch3 == 'r' & ch4 == 't')
                    return "start";
                if (ch1 == 't' & ch2 == 'y' & ch3 == 'l' & ch4 == 'e')
                    return "style";
                if (ch1 == 'm' & ch2 == 'a' & ch3 == 'l' & ch4 == 'l')
                    return "small";
                break;
            case 't':
                if (ch1 == 'i' & ch2 == 't' & ch3 == 'l' & ch4 == 'e')
                    return "title";
                if (ch1 == 'a' & ch2 == 'b' & ch3 == 'l' & ch4 == 'e')
                    return "table";
                if (ch1 == 'b' & ch2 == 'o' & ch3 == 'd' & ch4 == 'y')
                    return "tbody";
                if (ch1 == 'f' & ch2 == 'o' & ch3 == 'o' & ch4 == 't')
                    return "tfoot";
                if (ch1 == 'h' & ch2 == 'e' & ch3 == 'a' & ch4 == 'd')
                    return "thead";
                if (ch1 == 'r' & ch2 == 'a' & ch3 == 'c' & ch4 == 'k')
                    return "track";
                break;
            case 'u':
                if (ch1 == 't' & ch2 == 'f' & ch3 == '-' & ch4 == '8')
                    return "utf-8";
                break;
            case 'v':
                if (ch1 == 'a' & ch2 == 'l' & ch3 == 'u' & ch4 == 'e')
                    return "value";
                if (ch1 == 'i' & ch2 == 'd' & ch3 == 'e' & ch4 == 'o')
                    return "video";
                break;
            case 'w':
                if (ch1 == 'i' & ch2 == 'd' & ch3 == 't' & ch4 == 'h')
                    return "width";
                break;
        }
        break;
    }
    case 6: {
        switch (ch)
        {
            case '\r':
                return SpanEq(span, "\r\n    ");
            case '.':
                return SpanEq(span, ".$data");
            case 'A':
                return SpanEq(span, "Append");
            case 'B':
                return SpanEq(span, "Button");
            case 'C':
                return SpanEq(span, "Cancel") ??
                    SpanEq(span, "Custom") ??
                    SpanEq(span, "Column");
            case 'D':
                return SpanEq(span, "DotVVM") ??
                    SpanEq(span, "Device") ??
                    SpanEq(span, "Dialog");
            case 'E':
                return SpanEq(span, "Equals") ??
                    SpanEq(span, "Events");
            case 'G':
                return SpanEq(span, "Global");
            case 'L':
                return SpanEq(span, "Loader");
            case 'R':
                return SpanEq(span, "Remove");
            case 'S':
                return SpanEq(span, "Script") ??
                    SpanEq(span, "Styles");
            case 'T':
                return SpanEq(span, "Target");
            case 'U':
                return SpanEq(span, "Update");
            case 'V':
                return SpanEq(span, "Values");
            case 'a':
                return SpanEq(span, "accept") ??
                    SpanEq(span, "action") ??
                    SpanEq(span, "applet");
            case 'b':
                return SpanEq(span, "border") ??
                    SpanEq(span, "button");
            case 'c':
                return SpanEq(span, "coords") ??
                    SpanEq(span, "canvas") ??
                    SpanEq(span, "center");
            case 'd':
                return SpanEq(span, "dialog");
            case 'f':
                return SpanEq(span, "figure") ??
                    SpanEq(span, "footer");
            case 'h':
                return SpanEq(span, "height") ??
                    SpanEq(span, "hidden") ??
                    SpanEq(span, "header");
            case 'i':
                return SpanEq(span, "import") ??
                    SpanEq(span, "iframe");
            case 'l':
                return SpanEq(span, "legend");
            case 'm':
                return SpanEq(span, "method");
            case 'o':
                return SpanEq(span, "object") ??
                    SpanEq(span, "option") ??
                    SpanEq(span, "output");
            case 'p':
                return SpanEq(span, "poster");
            case 's':
                return SpanEq(span, "scoped") ??
                    SpanEq(span, "srcdoc") ??
                    SpanEq(span, "srcset") ??
                    SpanEq(span, "script") ??
                    SpanEq(span, "select") ??
                    SpanEq(span, "source") ??
                    SpanEq(span, "strike") ??
                    SpanEq(span, "strong");
            case 't':
                return SpanEq(span, "target");
            case 'u':
                return SpanEq(span, "usemap");
        }
        break;
    }
    case 7: {
        switch (ch)
        {
            case 'C':
                return SpanEq(span, "Context") ??
                    SpanEq(span, "Changed") ??
                    SpanEq(span, "Column2") ??
                    SpanEq(span, "Checked") ??
                    SpanEq(span, "Columns") ??
                    SpanEq(span, "Command") ??
                    SpanEq(span, "Content");
            case 'D':
                return SpanEq(span, "DataSet");
            case 'E':
                return SpanEq(span, "Enabled") ??
                    SpanEq(span, "Exclude");
            case 'L':
                return SpanEq(span, "Literal") ??
                    SpanEq(span, "ListBox");
            case 'M':
                return SpanEq(span, "Message");
            case 'P':
                return SpanEq(span, "Prepend");
            case 'T':
                return SpanEq(span, "TextBox");
            case 'U':
                return SpanEq(span, "UITests");
            case 'V':
                return SpanEq(span, "Visible");
            case '_':
                return SpanEq(span, "_parent");
            case 'a':
                return SpanEq(span, "acronym") ??
                    SpanEq(span, "address") ??
                    SpanEq(span, "article");
            case 'b':
                return SpanEq(span, "bgcolor");
            case 'c':
                return SpanEq(span, "command") ??
                    SpanEq(span, "capture") ??
                    SpanEq(span, "charset") ??
                    SpanEq(span, "checked") ??
                    SpanEq(span, "colspan") ??
                    SpanEq(span, "content") ??
                    SpanEq(span, "caption");
            case 'd':
                return SpanEq(span, "default") ??
                    SpanEq(span, "dirname") ??
                    SpanEq(span, "data-ui") ??
                    SpanEq(span, "details");
            case 'e':
                return SpanEq(span, "enctype");
            case 'h':
                return SpanEq(span, "headers");
            case 'k':
                return SpanEq(span, "keytype");
            case 'l':
                return SpanEq(span, "loading");
            case 'o':
                return SpanEq(span, "optimum");
            case 'p':
                return SpanEq(span, "pattern") ??
                    SpanEq(span, "preload") ??
                    SpanEq(span, "picture");
            case 'r':
                return SpanEq(span, "rowspan");
            case 's':
                return SpanEq(span, "service") ??
                    SpanEq(span, "sandbox") ??
                    SpanEq(span, "srclang") ??
                    SpanEq(span, "summary") ??
                    SpanEq(span, "section");
        }
        break;
    }
    case 8: {
        switch (ch)
        {
            case '\n':
                return SpanEq(span, "\n       ");
            case '$':
                return SpanEq(span, "$index()");
            case '.':
                return SpanEq(span, ".Visible") ??
                    SpanEq(span, ".Enabled");
            case 'C':
                return SpanEq(span, "ClientID") ??
                    SpanEq(span, "CssClass") ??
                    SpanEq(span, "CheckBox") ??
                    SpanEq(span, "Control1") ??
                    SpanEq(span, "Control2") ??
                    SpanEq(span, "ComboBox");
            case 'D':
                return SpanEq(span, "DoAction");
            case 'G':
                return SpanEq(span, "GridView");
            case 'H':
                return SpanEq(span, "Handlers");
            case 'I':
                return SpanEq(span, "Internal");
            case 'M':
                return SpanEq(span, "MyButton");
            case 'N':
                return SpanEq(span, "NewTitle");
            case 'P':
                return SpanEq(span, "PostBack");
            case 'R':
                return SpanEq(span, "ResultId") ??
                    SpanEq(span, "Repeater") ??
                    SpanEq(span, "RoleView");
            case 'S':
                return SpanEq(span, "Suppress") ??
                    SpanEq(span, "Selector");
            case 'T':
                return SpanEq(span, "ToString") ??
                    SpanEq(span, "Template");
            case 'U':
                return SpanEq(span, "UniqueID");
            case 'W':
                return SpanEq(span, "Wrappers");
            case '_':
                return SpanEq(span, "_control");
            case 'a':
                return SpanEq(span, "autoplay");
            case 'b':
                return SpanEq(span, "buffered") ??
                    SpanEq(span, "basefont");
            case 'c':
                return SpanEq(span, "codebase") ??
                    SpanEq(span, "controls") ??
                    SpanEq(span, "colgroup");
            case 'd':
                return SpanEq(span, "datetime") ??
                    SpanEq(span, "decoding") ??
                    SpanEq(span, "disabled") ??
                    SpanEq(span, "download") ??
                    SpanEq(span, "datalist");
            case 'f':
                return SpanEq(span, "fieldset") ??
                    SpanEq(span, "frameset");
            case 'h':
                return SpanEq(span, "hreflang");
            case 'i':
                return SpanEq(span, "itemprop");
            case 'l':
                return SpanEq(span, "language");
            case 'm':
                return SpanEq(span, "manifest") ??
                    SpanEq(span, "multiple");
            case 'n':
                return SpanEq(span, "noframes") ??
                    SpanEq(span, "noscript");
            case 'o':
                return SpanEq(span, "optgroup");
            case 'p':
                return SpanEq(span, "progress");
            case 'r':
                return SpanEq(span, "resource") ??
                    SpanEq(span, "readonly") ??
                    SpanEq(span, "required") ??
                    SpanEq(span, "reversed");
            case 's':
                return SpanEq(span, "selected");
            case 't':
                return SpanEq(span, "tabindex") ??
                    SpanEq(span, "template") ??
                    SpanEq(span, "textarea");
            case 'v':
                return SpanEq(span, "viewport");
        }
        break;
    }
    case 9: {
        switch (ch)
        {
            case '.':
                return SpanEq(span, ".$index()");
            case 'C':
                return SpanEq(span, "ClaimView");
            case 'D':
                return SpanEq(span, "DataPager");
            case 'E':
                return SpanEq(span, "EventName") ??
                    SpanEq(span, "EditClick") ??
                    SpanEq(span, "EmptyData");
            case 'G':
                return SpanEq(span, "GroupName");
            case 'I':
                return SpanEq(span, "IsSpaPage") ??
                    SpanEq(span, "InnerText");
            case 'O':
                return SpanEq(span, "OnCommand");
            case 'P':
                return SpanEq(span, "PreRender") ??
                    SpanEq(span, "Parameter");
            case 'R':
                return SpanEq(span, "RouteName") ??
                    SpanEq(span, "RouteLink");
            case 'T':
                return SpanEq(span, "TextInput");
            case 'U':
                return SpanEq(span, "UrlSuffix");
            case 'V':
                return SpanEq(span, "Validator");
            case 'a':
                return SpanEq(span, "accesskey") ??
                    SpanEq(span, "autofocus");
            case 'c':
                return SpanEq(span, "challenge");
            case 'd':
                return SpanEq(span, "draggable") ??
                    SpanEq(span, "data-bind");
            case 'i':
                return SpanEq(span, "integrity") ??
                    SpanEq(span, "inputmode");
            case 'm':
                return SpanEq(span, "maxlength") ??
                    SpanEq(span, "minlength");
            case 't':
                return SpanEq(span, "translate");
            case 'v':
                return SpanEq(span, "viewModel");
        }
        break;
    }
    case 10: {
        switch (ch)
        {
            case '\r':
                return SpanEq(span, "\r\n        ");
            case 'B':
                return SpanEq(span, "ButtonBase");
            case 'D':
                return SpanEq(span, "DataSource") ??
                    SpanEq(span, "Directives") ??
                    SpanEq(span, "DeviceList") ??
                    SpanEq(span, "DotvvmView");
            case 'F':
                return SpanEq(span, "FilesCount") ??
                    SpanEq(span, "FileUpload");
            case 'H':
                return SpanEq(span, "HeaderText");
            case 'I':
                return SpanEq(span, "IsEditable");
            case 'M':
                return SpanEq(span, "MyProperty");
            case 'O':
                return SpanEq(span, "OffCommand");
            case 'P':
                return SpanEq(span, "PrefixText");
            case 'T':
                return SpanEq(span, "TableUtils");
            case 'V':
                return SpanEq(span, "Validation");
            case 'b':
                return SpanEq(span, "background") ??
                    SpanEq(span, "blockquote");
            case 'f':
                return SpanEq(span, "formaction") ??
                    SpanEq(span, "formmethod") ??
                    SpanEq(span, "formtarget") ??
                    SpanEq(span, "figcaption");
            case 'h':
                return SpanEq(span, "http-equiv");
            case 'i':
                return SpanEq(span, "importance");
            case 'n':
                return SpanEq(span, "novalidate");
            case 'r':
                return SpanEq(span, "radiogroup");
            case 's':
                return SpanEq(span, "spellcheck");
        }
        break;
    }
    case 11: {
        switch (ch)
        {
            case 'A':
                return SpanEq(span, "ArticleBase");
            case 'C':
                return SpanEq(span, "CheckedItem") ??
                    SpanEq(span, "Concurrency");
            case 'D':
                return SpanEq(span, "DataContext") ??
                    SpanEq(span, "DoubleClick");
            case 'H':
                return SpanEq(span, "HtmlLiteral");
            case 'J':
                return SpanEq(span, "JsComponent");
            case 'M':
                return SpanEq(span, "MaxFileSize");
            case 'R':
                return SpanEq(span, "ReplaceWith") ??
                    SpanEq(span, "RadioButton");
            case 'S':
                return SpanEq(span, "SortChanged");
            case 'c':
                return SpanEq(span, "contextmenu") ??
                    SpanEq(span, "crossorigin");
            case 'd':
                return SpanEq(span, "description");
            case 'f':
                return SpanEq(span, "formenctype");
            case 'p':
                return SpanEq(span, "placeholder");
        }
        break;
    }
    case 12: {
        switch (ch)
        {
            case '\n':
                return SpanEq(span, "\n           ");
            case 'A':
                return SpanEq(span, "AllowSorting");
            case 'C':
                return SpanEq(span, "ClientIDMode") ??
                    SpanEq(span, "CheckedItems") ??
                    SpanEq(span, "CheckedValue");
            case 'D':
                return SpanEq(span, "Dependencies");
            case 'E':
                return SpanEq(span, "EditTemplate") ??
                    SpanEq(span, "Environments");
            case 'F':
                return SpanEq(span, "FormatString") ??
                    SpanEq(span, "FormControls");
            case 'G':
                return SpanEq(span, "GenerateStub");
            case 'I':
                return SpanEq(span, "ItemTemplate") ??
                    SpanEq(span, "InlineScript") ??
                    SpanEq(span, "ItemsControl") ??
                    SpanEq(span, "InnerWrapper");
            case 'N':
                return SpanEq(span, "NamedCommand");
            case 'O':
                return SpanEq(span, "OnCreateItem") ??
                    SpanEq(span, "OuterWrapper");
            case 'P':
                return SpanEq(span, "PathFragment") ??
                    SpanEq(span, "PromptButton");
            case 'R':
                return SpanEq(span, "ResourceName");
            case 'S':
                return SpanEq(span, "SelectorBase") ??
                    SpanEq(span, "SelectorItem");
            case 'T':
                return SpanEq(span, "TitleBinding") ??
                    SpanEq(span, "TemplateHost") ??
                    SpanEq(span, "TextRepeater");
            case 'V':
                return SpanEq(span, "ValueBinding");
            case 'a':
                return SpanEq(span, "autocomplete");
            case 'e':
                return SpanEq(span, "enterkeyhint");
        }
        break;
    }
    case 13: {
        switch (ch)
        {
            case '(':
                return SpanEq(span, "(()=>{let vm=");
            case 'A':
                return SpanEq(span, "ArticleEditor") ??
                    SpanEq(span, "ArticleDetail");
            case 'B':
                return SpanEq(span, "ButtonTagName") ??
                    SpanEq(span, "ButtonWrapper");
            case 'C':
                return SpanEq(span, "ColumnVisible");
            case 'D':
                return SpanEq(span, "DotvvmControl");
            case 'E':
                return SpanEq(span, "EmptyItemText");
            case 'H':
                return SpanEq(span, "HideWhenValid");
            case 'I':
                return SpanEq(span, "InlineEditing") ??
                    SpanEq(span, "IncludeInPage");
            case 'M':
                return SpanEq(span, "MultiSelector");
            case 'N':
                return SpanEq(span, "NumberBinding");
            case 'R':
                return SpanEq(span, "RowDecorators");
            case 'S':
                return SpanEq(span, "SelectedValue");
            case 'U':
                return SpanEq(span, "UseHistoryApi") ??
                    SpanEq(span, "UploadedFiles");
            case 'i':
                return SpanEq(span, "intrinsicsize");
            case 's':
                return SpanEq(span, "staticCommand");
        }
        break;
    }
    case 14: {
        switch (ch)
        {
            case '\r':
                return SpanEq(span, "\r\n            ");
            case 'C':
                return SpanEq(span, "CellDecorators") ??
                    SpanEq(span, "ChangedBinding");
            case 'E':
                return SpanEq(span, "ExcludedQueues");
            case 'F':
                return SpanEq(span, "FilterTemplate");
            case 'G':
                return SpanEq(span, "GridViewColumn");
            case 'H':
                return SpanEq(span, "HeaderTemplate") ??
                    SpanEq(span, "HtmlCapability") ??
                    SpanEq(span, "HeaderCssClass");
            case 'I':
                return SpanEq(span, "ItemKeyBinding") ??
                    SpanEq(span, "IncludedQueues") ??
                    SpanEq(span, "InnerViewModel") ??
                    SpanEq(span, "IsSubmitButton");
            case 'M':
                return SpanEq(span, "MarkupFileName");
            case 'R':
                return SpanEq(span, "RequestContext") ??
                    SpanEq(span, "RenderSettings");
            case 'S':
                return SpanEq(span, "SelectedValues") ??
                    SpanEq(span, "SortExpression") ??
                    SpanEq(span, "SetToolTipText");
            case 'U':
                return SpanEq(span, "UpdateProgress");
            case 'W':
                return SpanEq(span, "WrapperTagName");
            case 'a':
                return SpanEq(span, "accept-charset") ??
                    SpanEq(span, "autocapitalize");
            case 'c':
                return SpanEq(span, "controlCommand");
            case 'f':
                return SpanEq(span, "formnovalidate");
            case 'k':
                return SpanEq(span, "ko.contextFor(");
            case 'r':
                return SpanEq(span, "referrerpolicy");
        }
        break;
    }
    case 15: {
        switch (ch)
        {
            case 'C':
                return SpanEq(span, "ControlProperty") ??
                    SpanEq(span, "ContentTemplate");
            case 'D':
                return SpanEq(span, "DataContextType");
            case 'E':
                return SpanEq(span, "EnvironmentView");
            case 'F':
                return SpanEq(span, "FilterPlacement");
            case 'I':
                return SpanEq(span, "Items()?.length") ??
                    SpanEq(span, "ItemTextBinding") ??
                    SpanEq(span, "InvalidCssClass");
            case 'O':
                return SpanEq(span, "OriginalMessage");
            case 'P':
                return SpanEq(span, "PrefixRouteName") ??
                    SpanEq(span, "PostBackHandler");
            case 'U':
                return SpanEq(span, "UploadCompleted");
            case 'c':
                return SpanEq(span, "contenteditable");
        }
        break;
    }
    case 16: {
        switch (ch)
        {
            case '.':
                return SpanEq(span, ".Items()?.length");
            case 'A':
                return SpanEq(span, "AllowedFileTypes");
            case 'C':
                return SpanEq(span, "ClientIDFragment") ??
                    SpanEq(span, "ConcurrencyQueue");
            case 'D':
                return SpanEq(span, "DefaultRouteName");
            case 'G':
                return SpanEq(span, "GoToDetailAction");
            case 'H':
                return SpanEq(span, "HasClaimTemplate") ??
                    SpanEq(span, "HierarchyControl");
            case 'I':
                return SpanEq(span, "ItemTitleBinding") ??
                    SpanEq(span, "ItemValueBinding") ??
                    SpanEq(span, "Inner-li:Visible") ??
                    SpanEq(span, "IsMemberTemplate");
            case 'L':
                return SpanEq(span, "LastPageTemplate");
            case 'M':
                return SpanEq(span, "MarkupLineNumber");
            case 'N':
                return SpanEq(span, "NextPageTemplate");
            case 'R':
                return SpanEq(span, "RenderWrapperTag") ??
                    SpanEq(span, "RequiredResource");
            case 'S':
                return SpanEq(span, "SelectAllOnFocus") ??
                    SpanEq(span, "SelectionChanged") ??
                    SpanEq(span, "SanitizedMessage");
            case 'U':
                return SpanEq(span, "UploadButtonText");
            case 'd':
                return SpanEq(span, "dotvvm.postBack(");
        }
        break;
    }
    case 17: {
        switch (ch)
        {
            case 'A':
                return SpanEq(span, "AuthenticatedView");
            case 'C':
                return SpanEq(span, "ControlWithButton");
            case 'E':
                return SpanEq(span, "EmptyDataTemplate") ??
                    SpanEq(span, "EditRowDecorators");
            case 'F':
                return SpanEq(span, "FirstPageTemplate") ??
                    SpanEq(span, "FileUploadWrapper");
            case 'I':
                return SpanEq(span, "IsNamingContainer");
            case 'P':
                return SpanEq(span, "PostBack.Handlers");
            case 'R':
                return SpanEq(span, "RenderSpanElement") ??
                    SpanEq(span, "RequiredResources") ??
                    SpanEq(span, "RefreshTextButton");
            case 'S':
                return SpanEq(span, "SeparatorTemplate");
            case 'T':
                return SpanEq(span, "TextEditorControl");
            case 'U':
                return SpanEq(span, "UpdateTextOnInput");
            case 'V':
                return SpanEq(span, "ValidationSummary");
        }
        break;
    }
    case 18: {
        switch (ch)
        {
            case 'A':
                return SpanEq(span, "AllowMultipleFiles");
            case 'C':
                return SpanEq(span, "ControlWithButton2");
            case 'E':
                return SpanEq(span, "EditCellDecorators");
            case 'G':
                return SpanEq(span, "GridViewTextColumn");
            case 'H':
                return SpanEq(span, "HtmlGenericControl");
            case 'P':
                return SpanEq(span, "ParametrizedButton");
            case 'S':
                return SpanEq(span, "SuccessMessageText");
            case 'U':
                return SpanEq(span, "UsedPropertiesInfo");
            case 'V':
                return SpanEq(span, "ValidatorPlacement");
        }
        break;
    }
    case 19: {
        switch (ch)
        {
            case '(':
                return SpanEq(span, "(async ()=>{let vm=");
            case 'C':
                return SpanEq(span, "CurrentIndexBinding");
            case 'H':
                return SpanEq(span, "HasNotClaimTemplate") ??
                    SpanEq(span, "HideWhenOnlyOnePage") ??
                    SpanEq(span, "HeaderRowDecorators");
            case 'I':
                return SpanEq(span, "IsNotMemberTemplate");
            case 'S':
                return SpanEq(span, "ServerRenderedLabel");
        }
        break;
    }
    case 20: {
        switch (ch)
        {
            case 'C':
                return SpanEq(span, "ContentPlaceHolderID") ??
                    SpanEq(span, "CheckableControlBase");
            case 'D':
                return SpanEq(span, "DotvvmBindableObject");
            case 'H':
                return SpanEq(span, "HeaderCellDecorators");
            case 'P':
                return SpanEq(span, "PreviousPageTemplate");
            case 'S':
                return SpanEq(span, "ShowHeaderWhenNoData") ??
                    SpanEq(span, "ShowErrorMessageText");
            case 'T':
                return SpanEq(span, "TemplatedListControl");
            case 'k':
                return SpanEq(span, "ko.pureComputed(()=>");
        }
        break;
    }
    case 21: {
        switch (ch)
        {
            case 'A':
                return SpanEq(span, "AuthenticatedTemplate");
            case 'C':
                return SpanEq(span, "ControlCommandBinding");
            case 'H':
                return SpanEq(span, "HideForAnonymousUsers");
            case 'I':
                return SpanEq(span, "IsEnvironmentTemplate");
            case 'R':
                return SpanEq(span, "RenderAsNamedTemplate") ??
                    SpanEq(span, "RecursiveTextRepeater");
            case 'S':
                return SpanEq(span, "SpaContentPlaceHolder");
        }
        break;
    }
    case 22: {
        switch (ch)
        {
            case '(':
                return SpanEq(span, "(i)=>ko.unwrap(i).Id()");
            case 'C':
                return SpanEq(span, "CompositeControlSample") ??
                    SpanEq(span, "ConfirmPostBackHandler");
            case 'G':
                return SpanEq(span, "GridViewCheckBoxColumn") ??
                    SpanEq(span, "GridViewTemplateColumn");
            case 'I':
                return SpanEq(span, "IsControlBindingTarget");
            case 'T':
                return SpanEq(span, "TemplatedMarkupControl");
            case 'U':
                return SpanEq(span, "UploadErrorMessageText");
        }
        break;
    }
    case 23: {
        switch (ch)
        {
            case 'C':
                return SpanEq(span, "ControlPropertyUpdating") ??
                    SpanEq(span, "ConfigurableHtmlControl") ??
                    SpanEq(span, "ConcurrencyQueueSetting");
            case 'I':
                return SpanEq(span, "IncludeErrorsFromTarget");
            case 'R':
                return SpanEq(span, "ResourceRequiringButton");
            case 'S':
                return SpanEq(span, "ServerSideStylesControl") ??
                    SpanEq(span, "SuppressPostBackHandler");
            case 'T':
                return SpanEq(span, "TextOrContentCapability");
            case 'i':
                return SpanEq(span, "inner-li:HtmlCapability");
        }
        break;
    }
    case 24: {
        switch (ch)
        {
            case '(':
                return SpanEq(span, "(i)=>ko.unwrap(i).Text()") ??
                    SpanEq(span, "(i)=>ko.unwrap(i).Name()");
            case 'C':
                return SpanEq(span, "ConcurrencyQueueSettings");
            case 'I':
                return SpanEq(span, "IsNotEnvironmentTemplate");
            case 'N':
                return SpanEq(span, "NotAuthenticatedTemplate");
            case 'R':
                return SpanEq(span, "RenderLinkForCurrentPage") ??
                    SpanEq(span, "ReferencedViewModuleInfo");
            case 'S':
                return SpanEq(span, "StopwatchPostbackHandler");
        }
        break;
    }
    case 25: {
        switch (ch)
        {
            case 'C':
                return SpanEq(span, "ControlPropertyValidation");
            case 'E':
                return SpanEq(span, "ErrorCountPostbackHandler");
            case 'I':
                return SpanEq(span, "IncludeErrorsFromChildren");
        }
        break;
    }
    case 26: {
        switch (ch)
        {
            case 'N':
                return SpanEq(span, "NumberOfFilesIndicatorText");
        }
        break;
    }
    case 27: {
        switch (ch)
        {
            case 'C':
                return SpanEq(span, "CheckedItemsRepeaterWrapper");
            case 'S':
                return SpanEq(span, "SortAscendingHeaderCssClass");
        }
        break;
    }
    case 28: {
        switch (ch)
        {
            case 'S':
                return SpanEq(span, "SortDescendingHeaderCssClass");
            case 'h':
                return SpanEq(span, "http://www.w3.org/1999/xhtml");
        }
        break;
    }
    case 36: {
        switch (ch)
        {
            case 'd':
                return SpanEq(span, "dotvvm.evaluator.wrapObservable(()=>");
        }
        break;
    }
    case 39: {
        switch (ch)
        {
            case 'd':
                return SpanEq(span, "dotvvm.globalize.bindingNumberToString(");
        }
        break;
    }
}

            return null;
        }
    }
}
