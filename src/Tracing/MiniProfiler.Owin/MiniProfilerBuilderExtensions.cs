using DotVVM.Framework.Configuration;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Runtime.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Profiling;

namespace DotVVM.Tracing.MiniProfiler.Owin
{
    public static class MiniProfilerBuilderExtensions
    {
        /// <summary>
        /// Registers MiniProfiler tracer and MiniProfilerWidget
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IDotvvmServiceCollection AddMiniProfilerEventTracing(this IDotvvmServiceCollection services)
        {
            services.Services.AddScoped<IRequestTracer, MiniProfilerTracer>();
            services.Services.AddScoped<IMiniProfilerRequestTracer, MiniProfilerTracer>();
            services.Services.AddScoped<IRequestTimingStorage, DotvvmTimingStorage>();

            services.Services.Configure((MiniProfilerOptions opt) =>
            {
                opt.IgnoredPaths.Add("/_dotvvm/");
            });

            services.Services.AddTransient<IConfigureOptions<DotvvmConfiguration>, MiniProfilerSetup>();
            return services;
        }
    }

    internal class MiniProfilerSetup : IConfigureOptions<DotvvmConfiguration>
    {
        public void Configure(DotvvmConfiguration options)
        {
            options.Markup.AddCodeControls(DotvvmConfiguration.DotvvmControlTagPrefix, typeof(MiniProfilerWidget));
            options.Runtime.GlobalFilters.Add(new MiniProfilerActionFilter());
            options.Resources.Register(MiniProfilerWidget.IntegrationJSResourceName,
                    new ScriptResource(location: new EmbeddedResourceLocation(
                        typeof(MiniProfilerWidget).Assembly,
                        MiniProfilerWidget.IntegrationJSEmbeddedResourceName)) {
                        Dependencies = new[] { ResourceConstants.DotvvmResourceName },
                        RenderPosition = ResourceRenderPosition.Head
                    });
        }
    }
}
