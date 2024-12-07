using System;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Directives;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Styles;
using DotVVM.Framework.Compilation.Validation;
using DotVVM.Framework.Compilation.ViewCompiler;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Diagnostics;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Hosting.ErrorPages;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.Runtime.Caching;
using DotVVM.Framework.Runtime.Tracing;
using DotVVM.Framework.ViewModel;
using DotVVM.Framework.ViewModel.Serialization;
using DotVVM.Framework.ViewModel.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DotvvmServiceCollectionExtensions
    {
        /// <summary>
        /// Adds essential DotVVM services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        public static IServiceCollection RegisterDotVVMServices(IServiceCollection services)
        {
            // init dotvvm controls in the background, they will be needed no matter what
            DefaultControlResolver.InvokeStaticConstructorsOnDotvvmControls();

            services.AddOptions();

            services.TryAddSingleton<CompiledAssemblyCache>();
            services.TryAddSingleton<ExtensionMethodsCache>();
            services.TryAddSingleton<IDotvvmViewBuilder, DefaultDotvvmViewBuilder>();
            services.TryAddSingleton<IViewModelSerializer, DefaultViewModelSerializer>();
            services.TryAddSingleton<IViewModelLoader, DefaultViewModelLoader>();
            services.TryAddSingleton<IViewModelServerCache, DefaultViewModelServerCache>();
            services.TryAddSingleton<IViewModelServerStore, InMemoryViewModelServerStore>();
#pragma warning disable CS0618
            services.TryAddSingleton<IStaticCommandServiceLoader, DefaultStaticCommandServiceLoader>();
#pragma warning restore CS0618
            services.TryAddSingleton<IViewModelValidationMetadataProvider, AttributeViewModelValidationMetadataProvider>();
            services.TryAddSingleton<IViewModelTypeMetadataSerializer, ViewModelTypeMetadataSerializer>();
            services.TryAddSingleton<IValidationRuleTranslator, ViewModelValidationRuleTranslator>();
            services.TryAddSingleton<IPropertySerialization, DefaultPropertySerialization>();
            services.TryAddSingleton<UserColumnMappingCache>();
            services.TryAddSingleton<IValidationErrorPathExpander, ValidationErrorPathExpander>();
            services.TryAddSingleton<IViewModelValidator, ViewModelValidator>();
            services.TryAddSingleton<IStaticCommandArgumentValidator, StaticCommandArgumentValidator>();
            services.TryAddSingleton<IDotvvmJsonOptionsProvider, DotvvmJsonOptionsProvider>();
            services.TryAddSingleton<IViewModelSerializationMapper, ViewModelSerializationMapper>();
            services.TryAddSingleton<ViewModelJsonConverter>();
            services.TryAddSingleton<IViewModelParameterBinder, AttributeViewModelParameterBinder>();
            services.TryAddSingleton<IOutputRenderer, DefaultOutputRenderer>();
            services.TryAddSingleton<StaticCommandExecutor>();
            services.TryAddSingleton<IDotvvmPresenter, DotvvmPresenter>();
            services.TryAddSingleton<IMarkupFileLoader, AggregateMarkupFileLoader>();
            services.TryAddSingleton<IControlBuilderFactory, DefaultControlBuilderFactory>();
            services.TryAddSingleton<IControlResolver, DefaultControlResolver>();
            services.TryAddSingleton<IControlTreeResolver, DefaultControlTreeResolver>();
            services.TryAddSingleton<IMarkupDirectiveCompilerPipeline, MarkupDirectiveCompilerPipeline>();
            services.TryAddSingleton<IAbstractTreeBuilder, ResolvedTreeBuilder>();
            services.TryAddSingleton<Func<ControlUsageValidationVisitor>>(s => () => ActivatorUtilities.CreateInstance<ControlUsageValidationVisitor>(s));
            services.TryAddSingleton<IViewCompiler, DefaultViewCompiler>();
            services.AddSingleton<IDiagnosticsCompilationTracer>(s => s.GetRequiredService<DotvvmViewCompilationService.CompilationTracer>());
            services.TryAddSingleton<CompositeDiagnosticsCompilationTracer>();
            services.TryAddSingleton<IBindingCompiler, BindingCompiler>();
            services.TryAddSingleton<IBindingExpressionBuilder, BindingExpressionBuilder>();
            services.TryAddSingleton<BindingCompilationService, BindingCompilationService>();
            services.TryAddSingleton<DirectiveCompilationService, DirectiveCompilationService>();
            services.TryAddSingleton<GridViewDataSetBindingProvider>();
            services.TryAddSingleton<IControlUsageValidator, DefaultControlUsageValidator>();
            services.TryAddSingleton<ILocalResourceUrlManager, LocalResourceUrlManager>();
            services.TryAddSingleton<IResourceHashService, DefaultResourceHashService>();
            services.TryAddSingleton<StaticCommandBindingCompiler, StaticCommandBindingCompiler>();
            services.TryAddSingleton<JavascriptTranslator, JavascriptTranslator>();
            services.TryAddSingleton<IHttpRedirectService, DefaultHttpRedirectService>();
            services.TryAddSingleton<IExpressionToDelegateCompiler, DefaultExpressionToDelegateCompiler>();

            services.TryAddScoped<DotvvmRequestContextStorage>(_ => new DotvvmRequestContextStorage());
            services.TryAddScoped<IDotvvmRequestContext>(s => s.GetRequiredService<DotvvmRequestContextStorage>().Context!);

            services.AddScoped<IRequestTracer>(s => {
                var config = s.GetRequiredService<DotvvmConfiguration>();
                return (config.Diagnostics.PerfWarnings.IsEnabled ? (IRequestTracer?)s.GetService<PerformanceWarningTracer>() : null) ?? NullRequestTracer.Instance;
            });
            services.TryAddSingleton<JsonSizeAnalyzer>();
            services.TryAddScoped<PerformanceWarningTracer>();
            services.TryAddScoped<RuntimeWarningCollector>();
            services.AddTransient<IDotvvmWarningSink, AspNetCoreLoggerWarningSink>();
            services.TryAddScoped<AggregateRequestTracer, AggregateRequestTracer>();
            services.TryAddScoped<ResourceManager, ResourceManager>();
            services.TryAddSingleton<ValidationPathFormatter>();
            services.TryAddSingleton(s => DotvvmConfiguration.CreateDefault(s));
            services.TryAddSingleton(s => s.GetRequiredService<DotvvmConfiguration>().Markup);
            services.TryAddSingleton(s => s.GetRequiredService<DotvvmConfiguration>().Resources);
            services.TryAddSingleton(s => s.GetRequiredService<DotvvmConfiguration>().RouteTable);
            services.TryAddSingleton(s => s.GetRequiredService<DotvvmConfiguration>().Runtime);
            services.TryAddSingleton(s => s.GetRequiredService<DotvvmConfiguration>().Security);
            services.TryAddSingleton(s => s.GetRequiredService<DotvvmConfiguration>().Styles);

            services.ConfigureWithServices<BindingCompilationOptions>((o, s) => {
                 o.TransformerClasses.Add(ActivatorUtilities.CreateInstance<BindingPropertyResolvers>(s));
            });

            services.ConfigureWithServices<ViewCompilerConfiguration>((o, s) => {
                var controlResolver = s.GetRequiredService<IControlResolver>();
                o.TreeVisitors.Add(() => ActivatorUtilities.CreateInstance<ObsoletionVisitor>(s));
                o.TreeVisitors.Add(() => ActivatorUtilities.CreateInstance<AliasingVisitor>(s));
                o.TreeVisitors.Add(() => ActivatorUtilities.CreateInstance<StylingVisitor>(s));
                var requiredResourceControl = controlResolver.ResolveControl(new ResolvedTypeDescriptor(typeof(RequiredResource)));
                o.TreeVisitors.Add(() => new StyleTreeShufflingVisitor(controlResolver));
                o.TreeVisitors.Add(() => new ControlPrecompilationVisitor(s));
                o.TreeVisitors.Add(() => new LiteralOptimizationVisitor());
                o.TreeVisitors.Add(() => new BindingRequiredResourceVisitor((ControlResolverMetadata)requiredResourceControl));
                var requiredGlobalizeControl = controlResolver.ResolveControl(new ResolvedTypeDescriptor(typeof(GlobalizeResource)));
                o.TreeVisitors.Add(() => new GlobalizeResourceVisitor((ControlResolverMetadata)requiredGlobalizeControl));
                o.TreeVisitors.Add(() => ActivatorUtilities.CreateInstance<DataContextPropertyAssigningVisitor>(s));
                o.TreeVisitors.Add(() => new UsedPropertiesFindingVisitor());
                o.TreeVisitors.Add(() => new LifecycleRequirementsAssigningVisitor());
                o.TreeVisitors.Add(() => new UnsupportedCallSiteCheckingVisitor());
            });

            services.TryAddSingleton<IDotvvmCacheAdapter, DefaultDotvvmCacheAdapter>();
            services.TryAddSingleton<DotvvmErrorPageRenderer>();
            services.AddSingleton<DotvvmViewCompilationService.CompilationTracer>();
            services.AddSingleton<IDotvvmViewCompilationService, DotvvmViewCompilationService>();
            services.AddSingleton<CompilationPageApiPresenter>();


            return services;
        }

        public static void ConfigureWithServices<TObject, TService>(this IServiceCollection services, Action<TObject, TService> configure)
            where TObject: class
            where TService: notnull
        {
            services.AddSingleton<IConfigureOptions<TObject>>(s => new ConfigureOptions<TObject>(o => configure(o, s.GetRequiredService<TService>())));
        }

        public static void ConfigureWithServices<TObject, TService1, TService2>(this IServiceCollection services, Action<TObject, TService1, TService2> configure)
            where TObject: class
            where TService1: notnull
            where TService2: notnull
        {
            services.AddSingleton<IConfigureOptions<TObject>>(s => new ConfigureOptions<TObject>(o => configure(o, s.GetRequiredService<TService1>(), s.GetRequiredService<TService2>())));
        }

        public static void ConfigureWithServices<TObject>(this IServiceCollection services, Action<TObject, IServiceProvider> configure)
            where TObject: class
        {
            services.AddSingleton<IConfigureOptions<TObject>>(s => new ConfigureOptions<TObject>(o => configure(o, s)));
        }
    }
}
