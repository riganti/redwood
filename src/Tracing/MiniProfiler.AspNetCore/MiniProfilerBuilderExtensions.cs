using System;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Runtime.Tracing;
using DotVVM.Tracing.MiniProfiler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Profiling;

namespace DotVVM.Framework.Configuration
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

            services.Services.Configure((MiniProfilerOptions opt) => {
                opt.IgnoredPaths.Add("/_dotvvm/");
            });

            services.Services.Configure((DotvvmConfiguration conf) => {
                conf.Markup.AddCodeControls(DotvvmConfiguration.DotvvmControlTagPrefix, typeof(MiniProfilerWidget));
                conf.Runtime.GlobalFilters.Add(new MiniProfilerActionFilter());
                conf.Resources.RegisterScript(MiniProfilerWidget.IntegrationJSResourceName,
                    new EmbeddedResourceLocation(
                        typeof(MiniProfilerWidget).Assembly,
                        MiniProfilerWidget.IntegrationJSEmbeddedResourceName),
                    dependencies: new[] { ResourceConstants.DotvvmResourceName });
            });

            return services;
        }
    }
}
