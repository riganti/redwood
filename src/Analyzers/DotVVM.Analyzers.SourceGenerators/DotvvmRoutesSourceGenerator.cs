using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotVVM.Framework.Compilation.Parser.Dothtml.Tokenizer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotVVM.Analyzers.SourceGenerators
{
    [Generator]
    public class DotvvmRoutesSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDirectory))
            {
                throw new Exception("Unable to find project directory.");
            }
            projectDirectory = Path.GetFullPath(projectDirectory);

            if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.rootnamespace", out var rootNamespace))
            {
                throw new Exception("Unable to find root namespace");
            }

            var routes = new List<(string routeName, string url, string virtualPath)>();

            var potentialMarkupFiles = context.AdditionalFiles.Where(f => f.Path.EndsWith(".dothtml", StringComparison.OrdinalIgnoreCase));
            foreach (var file in potentialMarkupFiles)
            {
                try
                {
                    var content = file.GetText(context.CancellationToken);
                    if (TryExtractRouteDirective(content) is { } url)
                    {
                        var virtualPath = GetRelativePath(projectDirectory, file.Path).Replace("\\", "/");

                        var routeName = virtualPath.Replace("/", "_");
                        routeName = routeName.Substring(0, routeName.LastIndexOf("."));

                        if (routeName.StartsWith("Views_", StringComparison.OrdinalIgnoreCase))
                        {
                            routeName = routeName.Substring("Views_".Length);
                        }

                        routes.Add((routeName, url, virtualPath));
                    }
                }
                catch (Exception ex)
                {
                    context.ReportDiagnostic(Diagnostic.Create("DG0001", "DotVVM routing", ex.Message, DiagnosticSeverity.Warning, DiagnosticSeverity.Warning, true, 1));
                }
            }

            var result = $$"""
                using DotVVM.Framework.Routing;

                namespace {{rootNamespace}}
                {
                    public static class DotvvmRoutes
                    {
                        {{ string.Join("\n    ", routes.Select(r => $"public const string {r.routeName} = nameof({r.routeName});")) }}

                        public static void RegisterRoutes(DotvvmRouteTable routes) {
                            {{ string.Join("\n        ", routes.Select(r => $"routes.Add(\"{r.routeName}\", \"{r.url}\", \"{r.virtualPath}\");")) }}
                        }
                    }
                }
                """;
            context.AddSource("DotvvmRoutes.cs", result);
        }

        private string GetRelativePath(string projectDirectory, string path)
        {
            path = Path.GetFullPath(path);
            if (!path.StartsWith(projectDirectory))
            {
                throw new Exception($"File {path} is outside the project directory!");
            }
            return path.Substring(projectDirectory.Length).TrimStart('/', '\\');
        }

        private static string? TryExtractRouteDirective(SourceText content)
        {
            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(content.ToString());

            for (var i = 0; i < tokenizer.Tokens.Count - 3; i++)
            {
                var token = tokenizer.Tokens[i];
                if (token.Type == DothtmlTokenType.DirectiveStart)
                {
                    i++;
                    token = tokenizer.Tokens[i];

                    if (token is { Type: DothtmlTokenType.DirectiveName, Text: "route" })
                    {
                        i += 2;
                        token = tokenizer.Tokens[i];

                        return token.Text;
                    }
                }
            }

            return null;
        }
    }
}

namespace DotVVM.Framework.Utils
{
    public static class StringUtils
    {
        public static string DotvvmInternString(this string s) => s;
        public static string DotvvmInternString(this char s) => s.ToString();
        public static string DotvvmInternString(this ReadOnlySpan<char> s) => s.ToString();
    }
}
