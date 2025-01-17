using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DotVVM.Framework.Binding.Properties;

namespace DotVVM.Framework.Compilation
{
    public sealed class DotHtmlFileInfo
    {
        public CompilationState Status { get; internal set; }
        public string? Exception { get; internal set; }

        public ImmutableArray<CompilationDiagnosticViewModel> Errors { get; internal set; } = ImmutableArray<CompilationDiagnosticViewModel>.Empty;
        public ImmutableArray<CompilationDiagnosticViewModel> Warnings { get; internal set; } = ImmutableArray<CompilationDiagnosticViewModel>.Empty;

        /// <summary>Gets or sets the virtual path to the view.</summary>
        public string? VirtualPath { get; }

        public string? TagName { get; }
        public string? Namespace { get; }
        public string? Assembly { get; }
        public string? TagPrefix { get; }
        public string? Url { get; }
        public string? RouteName { get; }
        public ImmutableArray<string>? DefaultValues { get; }
        public bool? HasParameters { get; }

        public DotHtmlFileInfo(string? virtualPath, string? tagName = null, string? nameSpace = null, string? assembly = null, string? tagPrefix = null, string? url = null, string? routeName = null, ImmutableArray<string>? defaultValues = null, bool? hasParameters = null)
        {
            VirtualPath = virtualPath;
            Status = IsDothtmlFile(virtualPath) ? CompilationState.None : CompilationState.NonCompilable;

            TagName = tagName;
            Namespace = nameSpace;
            Assembly = assembly;
            TagPrefix = tagPrefix;
            Url = url;
            RouteName = routeName;
            DefaultValues = defaultValues;
            HasParameters = hasParameters;
        }

        private static bool IsDothtmlFile(string? virtualPath)
        {
            return !string.IsNullOrWhiteSpace(virtualPath) &&
                (
                virtualPath.IndexOf(".dothtml", StringComparison.OrdinalIgnoreCase) > -1 ||
                virtualPath.IndexOf(".dotmaster", StringComparison.OrdinalIgnoreCase) > -1 ||
                virtualPath.IndexOf(".dotcontrol", StringComparison.OrdinalIgnoreCase) > -1 ||
                virtualPath.IndexOf(".dotlayout", StringComparison.OrdinalIgnoreCase) > -1
                );
        }

        public sealed record CompilationDiagnosticViewModel(
            DiagnosticSeverity Severity,
            string Message,
            string? FileName,
            int? LineNumber,
            int? ColumnNumber,
            string? SourceLine,
            int? HighlightLength
        )
        {
            public string? SourceLine { get; set; } = SourceLine;
            public string? SourceLinePrefix => SourceLine?.Substring(0, ColumnNumber ?? 0);
            public string? SourceLineHighlight =>
                HighlightLength is {} len ? SourceLine?.Substring(ColumnNumber ?? 0, len)
                                          : SourceLine?.Substring(ColumnNumber ?? 0);
            public string? SourceLineSuffix =>
                (ColumnNumber + HighlightLength) is int startIndex ? SourceLine?.Substring(startIndex) : null;


            public CompilationDiagnosticViewModel(DotvvmCompilationDiagnostic diagnostic, string? contextLine)
                : this(
                    diagnostic.Severity,
                    diagnostic.Message,
                    diagnostic.Location.FileName,
                    diagnostic.Location.LineNumber,
                    diagnostic.Location.ColumnNumber,
                    contextLine,
                    diagnostic.Location.LineErrorLength
                )
            {
            }
        }
    }
}
