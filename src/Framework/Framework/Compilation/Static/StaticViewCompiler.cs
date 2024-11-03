
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ViewCompiler;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Security;
using DotVVM.Framework.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotVVM.Framework.Compilation.Static
{
    internal static class StaticViewCompiler
    {
        public static ImmutableArray<DotvvmCompilationDiagnostic> CompileAll(
            Assembly dotvvmProjectAssembly,
            string dotvvmProjectDir)
        {
            var configuration = ConfigurationInitializer.GetConfiguration(dotvvmProjectAssembly, dotvvmProjectDir);
            var diagnostics = ImmutableArray.CreateBuilder<DotvvmCompilationDiagnostic>();

            var markupControls = configuration.Markup.Controls.Select(c => c.Src)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToImmutableArray();
            foreach (var markupControl in markupControls)
            {
                diagnostics.AddRange(CompileNoThrow(configuration, markupControl!));
            }

            var views = configuration.RouteTable.Select(r => r.VirtualPath).WhereNotNull().ToImmutableArray();
            foreach(var view in views)
            {
                diagnostics.AddRange(CompileNoThrow(configuration, view));
            }

            return diagnostics.Distinct().ToImmutableArray();
        }

        private static ImmutableArray<DotvvmCompilationDiagnostic> CompileNoThrow(
            DotvvmConfiguration configuration,
            string viewPath)
        {
            var fileLoader = configuration.ServiceProvider.GetRequiredService<IMarkupFileLoader>();
            var file = fileLoader.GetMarkup(configuration, viewPath);
            if (file is null)
            {
                return ImmutableArray.Create<DotvvmCompilationDiagnostic>();
            }

            var sourceCode = file.ReadContent();

            try
            {
                var compiler = configuration.ServiceProvider.GetRequiredService<IViewCompiler>();
                var (_, builderFactory) = compiler.CompileView(
                    sourceCode: sourceCode,
                    fileName: viewPath);
                _ = builderFactory();
                // TODO: get warnings from compilation tracer
                return ImmutableArray.Create<DotvvmCompilationDiagnostic>();
            }
            catch(DotvvmCompilationException e)
            {
                return e.AllDiagnostics.ToImmutableArray();
            }
        }
    }
}
