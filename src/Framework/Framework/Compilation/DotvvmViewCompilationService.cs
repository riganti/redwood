using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Testing;
using Microsoft.Extensions.Logging;

namespace DotVVM.Framework.Compilation
{
    public class DotvvmViewCompilationService : IDotvvmViewCompilationService
    {
        private readonly IControlBuilderFactory controlBuilderFactory;
        private readonly CompilationTracer tracer;
        private readonly IMarkupFileLoader markupFileLoader;
        private readonly ILogger<DotvvmViewCompilationService>? log;
        private readonly DotvvmConfiguration dotvvmConfiguration;

        public DotvvmViewCompilationService(DotvvmConfiguration dotvvmConfiguration, IControlBuilderFactory controlBuilderFactory, CompilationTracer tracer, IMarkupFileLoader markupFileLoader, ILogger<DotvvmViewCompilationService>? log = null)
        {
            this.dotvvmConfiguration = dotvvmConfiguration;
            this.controlBuilderFactory = controlBuilderFactory;
            this.tracer = tracer;
            this.markupFileLoader = markupFileLoader;
            this.log = log;
            masterPages = new Lazy<ConcurrentDictionary<string, DotHtmlFileInfo>>(InitMasterPagesCollection);
            controls = new Lazy<ImmutableArray<DotHtmlFileInfo>>(InitControls);
            routes = new Lazy<ImmutableArray<DotHtmlFileInfo>>(InitRoutes);
        }

        public ImmutableArray<DotHtmlFileInfo> GetFilesWithFailedCompilation()
        {
            return masterPages.Value.Values.Concat(controls.Value).Concat(routes.Value)
                .Where(t => t.Status == CompilationState.CompilationFailed).ToImmutableArray();
        }

        private readonly Lazy<ConcurrentDictionary<string, DotHtmlFileInfo>> masterPages;
        private ConcurrentDictionary<string, DotHtmlFileInfo> InitMasterPagesCollection()
        {
            return new ConcurrentDictionary<string, DotHtmlFileInfo>();
        }
        public ImmutableArray<DotHtmlFileInfo> GetMasterPages()
        {
            return masterPages.Value.Values.ToImmutableArray();
        }

        private readonly Lazy<ImmutableArray<DotHtmlFileInfo>> controls;
        private ImmutableArray<DotHtmlFileInfo> InitControls()
        {
            return
                dotvvmConfiguration.Markup.Controls.Where(s => !string.IsNullOrWhiteSpace(s.Src))
                    .Select(s => new DotHtmlFileInfo(s.Src!, tagPrefix: s.TagPrefix, tagName: s.TagName,
                        nameSpace: s.Namespace, assembly: s.Assembly)).ToImmutableArray();
        }

        public ImmutableArray<DotHtmlFileInfo> GetControls()
        {
            return controls.Value;
        }

        private readonly Lazy<ImmutableArray<DotHtmlFileInfo>> routes;
        private ImmutableArray<DotHtmlFileInfo> InitRoutes()
        {
            return dotvvmConfiguration.RouteTable.Select(r =>
                new DotHtmlFileInfo(r.VirtualPath,
                    url: r.Url,
                    hasParameters: r.ParameterNames.Any(),
                    defaultValues: r.DefaultValues.Select(s => s.Key + ":" + s.Value).ToImmutableArray(),
                    routeName: r.RouteName)).ToImmutableArray();
        }

        public ImmutableArray<DotHtmlFileInfo> GetRoutes()
        {
            return routes.Value;
        }
        private IEnumerable<DotHtmlFileInfo> GetFilesToBeCompiled()
        {
            return Enumerable.Union(controls.Value, routes.Value).Where(t => t.Status == CompilationState.None);
        }

        static readonly SemaphoreSlim compilationSemaphore = new SemaphoreSlim(1);

        public async Task<bool> CompileAll(bool buildInParallel = true, bool forceRecompile = false)
        {
            var exclusiveMode = false;
            try
            {
                IEnumerable<DotHtmlFileInfo>? filesToCompile = null;
                if (forceRecompile)
                {
                    filesToCompile = controls.Value.Union(routes.Value);
                }
                else
                {
                    filesToCompile = GetFilesToBeCompiled();
                    if (filesToCompile.Any())
                    {
                        await compilationSemaphore.WaitAsync(); //LOCK

                        exclusiveMode = true;
                        filesToCompile = GetFilesToBeCompiled();
                        if (!filesToCompile.Any())
                            return GetFilesWithFailedCompilation().Any();
                    }
                }
                var discoveredMasterPages = new ConcurrentDictionary<string, DotHtmlFileInfo>();
                var maxParallelism = buildInParallel ? Environment.ProcessorCount : 1;
                if (!dotvvmConfiguration.Debug && dotvvmConfiguration.Markup.ViewCompilation.Mode != ViewCompilationMode.DuringApplicationStart)
                {
                    // in production when compiling after application start, only use half of the CPUs to leave room for handling requests
                    maxParallelism = (int)Math.Ceiling(maxParallelism * 0.5);
                }
                var sw = ValueStopwatch.StartNew();

                var compilationTaskFactory = (DotHtmlFileInfo t) => () => {
                    BuildView(t, forceRecompile, out var masterPage);
                    if (masterPage != null && masterPage.Status == CompilationState.None)
                        discoveredMasterPages.TryAdd(masterPage.VirtualPath!, masterPage);
                };

                var compileTasks = filesToCompile.Select(compilationTaskFactory).ToArray();
                var totalCompiledFiles = compileTasks.Length;
                await ExecuteCompileTasks(compileTasks, maxParallelism);

                while (discoveredMasterPages.Any())
                {
                    compileTasks = discoveredMasterPages.Values.Select(compilationTaskFactory).ToArray();
                    totalCompiledFiles += compileTasks.Length;
                    discoveredMasterPages = new ConcurrentDictionary<string, DotHtmlFileInfo>();

                    await ExecuteCompileTasks(compileTasks, maxParallelism);
                }

                log?.LogInformation("Compiled {0} DotHTML files on {1} threads in {2} s", totalCompiledFiles, maxParallelism, sw.ElapsedSeconds);
            }
            finally
            {
                if (exclusiveMode) compilationSemaphore.Release(); //UNLOCK
            }

            return !GetFilesWithFailedCompilation().Any();
        }

        private static async Task ExecuteCompileTasks(Action[] compileTasks, int maxParallelism)
        {
            if (maxParallelism > 1)
            {
                var semaphore = new SemaphoreSlim(maxParallelism);
                await Task.WhenAll(compileTasks.Select(async t => {
                    await semaphore.WaitAsync();
                    try
                    {
                        await Task.Run(t);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
            else
            {
                foreach (var task in compileTasks)
                {
                    task();
                }
            }
        }

        public bool BuildView(DotHtmlFileInfo file, out DotHtmlFileInfo? masterPage) =>
            BuildView(file, false, out masterPage);
        public bool BuildView(DotHtmlFileInfo file, bool forceRecompile, out DotHtmlFileInfo? masterPage)
        {
            masterPage = null;
            if (file.Status != CompilationState.NonCompilable)
            {
                try
                {
                    if (forceRecompile)
                        // TODO: next major version - add method to interface
                        (controlBuilderFactory as DefaultControlBuilderFactory)?.InvalidateCache(file.VirtualPath!);

                    var pageBuilder = controlBuilderFactory.GetControlBuilder(file.VirtualPath!);

                    using var scopedServices = dotvvmConfiguration.ServiceProvider.CreateScope(); // dependencies that are configured as scoped cannot be resolved from root service provider
                    scopedServices.ServiceProvider.GetRequiredService<DotvvmRequestContextStorage>().Context = new ViewCompilationFakeRequestContext(scopedServices.ServiceProvider);
                    var compiledControl = pageBuilder.builder.Value.BuildControl(controlBuilderFactory, scopedServices.ServiceProvider);

                    if (pageBuilder.descriptor.MasterPage is { FileName: {} masterPagePath })
                    {
                        masterPage = masterPages.Value.GetOrAdd(masterPagePath, path => new DotHtmlFileInfo(path));
                    }

                    file.Status = CompilationState.CompletedSuccessfully;
                    file.Exception = null;
                }
                catch (Exception e)
                {
                    file.Status = CompilationState.CompilationFailed;
                    file.Exception = e.Message;
                    return false;
                }
            }
            return true;
        }

        /// <summary> Callback from the compiler which adds the view compilation result to the status page. </summary>
        public void RegisterCompiledView(string file, ViewCompiler.ControlBuilderDescriptor? descriptor, Exception? exception)
        {
            var fileInfo =
                routes.Value.FirstOrDefault(t => t.VirtualPath == file) ??
                controls.Value.FirstOrDefault(t => t.VirtualPath == file) ??
                masterPages.Value.GetOrAdd(file, path => new DotHtmlFileInfo(path));

            var tracerData = this.tracer.CompiledViews.GetValueOrDefault(file);
            
            fileInfo.Exception = null;

            var diagnostics = tracerData?.Diagnostics ?? Enumerable.Empty<DotHtmlFileInfo.CompilationDiagnosticViewModel>();

            if (exception is null)
            {
                fileInfo.Status = CompilationState.CompletedSuccessfully;
            }
            else
            {
                fileInfo.Status = CompilationState.CompilationFailed;
                fileInfo.Exception = exception.Message;

                if (exception is DotvvmCompilationException compilationException)
                {
                    // overwrite the tracer diagnostics to avoid presenting duplicates
                    diagnostics = compilationException.AllDiagnostics.Select(x => new DotHtmlFileInfo.CompilationDiagnosticViewModel(x, null)).ToArray();

                    AddSourceLines(diagnostics, compilationException.AllDiagnostics);
                }
            }

            fileInfo.Errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
            fileInfo.Warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToImmutableArray();

            if (descriptor?.MasterPage is { FileName: {} masterPagePath })
            {
                masterPages.Value.GetOrAdd(masterPagePath, path => new DotHtmlFileInfo(path));
            }
        }

        /// <summary> Loads the error markup file(s), adds the source line information to <paramref name="viewModels" /> </summary>
        private void AddSourceLines(IEnumerable<DotHtmlFileInfo.CompilationDiagnosticViewModel> viewModels, IEnumerable<DotvvmCompilationDiagnostic> originalDiagnostics)
        {
            var markupFiles = new Dictionary<string, MarkupFile?>();
            foreach (var d in originalDiagnostics)
            {
                if (d.Location.FileName is null)
                    continue;
                markupFiles[d.Location.FileName] = markupFiles.GetValueOrDefault(d.Location.FileName) ?? d.Location.MarkupFile;
            }
            var sourceCodes = new Dictionary<string, string[]?>();
            foreach (var fileName in viewModels.Where(vm => vm.FileName is {} && vm.LineNumber is {} && vm.SourceLine is null).Select(vm => vm.FileName).Distinct())
            {
                var markupFile = markupFiles.GetValueOrDefault(fileName!) ?? markupFileLoader.GetMarkup(this.dotvvmConfiguration, fileName!);
                var sourceCode = markupFile?.ReadContent();
                if (sourceCode is {})
                    sourceCodes.Add(fileName!, sourceCode.Split('\n'));
            }
            foreach (var d in viewModels)
            {
                if (d.FileName is null || d.LineNumber is not > 0 || d.SourceLine is {})
                    continue;
                var source = sourceCodes.GetValueOrDefault(d.FileName);
                
                if (source is null || d.LineNumber!.Value > source.Length)
                    continue;

                d.SourceLine = source[d.LineNumber.Value - 1];
            }
        }

        public class CompilationTracer : IDiagnosticsCompilationTracer
        {
            internal readonly ConcurrentDictionary<string, Handle> CompiledViews = new ConcurrentDictionary<string, Handle>();
            public IDiagnosticsCompilationTracer.Handle CompilationStarted(string file, string sourceCode)
            {
                return new Handle(this, file);
            }

            internal sealed class Handle : IDiagnosticsCompilationTracer.Handle, IDisposable
            {
                private readonly CompilationTracer compilationTracer;
                public string File { get; }
                public DateTime CompiledAt { get; } = DateTime.UtcNow;
                public List<DotHtmlFileInfo.CompilationDiagnosticViewModel> Diagnostics = new();

                public Handle(CompilationTracer compilationTracer, string file)
                {
                    this.compilationTracer = compilationTracer;
                    this.File = file;
                }

                public override void CompilationDiagnostic(DotvvmCompilationDiagnostic diagnostic, string? contextLine)
                {
                    Diagnostics.Add(new (diagnostic, contextLine));
                }

                public void Dispose()
                {
                    compilationTracer.CompiledViews[this.File] = this;
                }
            }
        }
    }
}
