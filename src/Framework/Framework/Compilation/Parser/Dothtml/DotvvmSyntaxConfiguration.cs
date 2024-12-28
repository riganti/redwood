using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Configuration;

namespace DotVVM.Framework.Compilation.Parser.Dothtml;

public sealed class DotvvmSyntaxConfiguration
{
    private readonly HashSet<string> rawTextElements;
    public IEnumerable<string> RawTextElements => rawTextElements;

    public bool IsRawTextElement(string elementName) =>
        rawTextElements.Contains(elementName);

    public DotvvmSyntaxConfiguration(IEnumerable<string> rawTextElements)
    {
        this.rawTextElements = rawTextElements.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static DotvvmSyntaxConfiguration FromMarkupConfig(DotvvmMarkupConfiguration markupConfiguration)
    {
        var rawTextElements = markupConfiguration.RawTextElements;

        return new DotvvmSyntaxConfiguration(rawTextElements);
    }

    public static DotvvmSyntaxConfiguration Default { get; } = new DotvvmSyntaxConfiguration(["script", "style", "dot:InlineScript", "dot:HtmlLiteral"]);
}
