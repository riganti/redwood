using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.ResourceManagement;
using System.Collections.Concurrent;
using System.Threading;
using DotVVM.Framework.Hosting.ErrorPages;
using DotVVM.Framework.Hosting.Middlewares;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.ViewModel;
using DotVVM.Framework.ViewModel.Serialization;
using Microsoft.AspNetCore.Http;
using DotVVM.Framework.Routing;

namespace DotVVM.Framework.Hosting
{
    /// <summary>
    /// A middleware that handles DotVVM HTTP requests.
    /// </summary>
    public class DotvvmMiddleware : DotvvmMiddlewareBase
    {
        public readonly DotvvmConfiguration Configuration;


        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmMiddleware"/> class.
        /// </summary>
        public DotvvmMiddleware(RequestDelegate next, DotvvmConfiguration configuration, IList<IMiddleware> middlewares)
        {
            this.next = next;
            Configuration = configuration;
            this.middlewares = middlewares;
        }

        private int configurationSaved = 0;
        private readonly RequestDelegate next;
        private readonly IList<IMiddleware> middlewares;

        /// <summary>
        /// Process an individual request.
        /// </summary>
        public async Task Invoke(HttpContext context)
        {
            if (Interlocked.Exchange(ref configurationSaved, 1) == 0)
            {
                VisualStudioHelper.DumpConfiguration(Configuration, Configuration.ApplicationPhysicalPath);
            }
            // create the context
            var dotvvmContext = CreateDotvvmContext(context);
            context.Items.Add(HostingConstants.DotvvmRequestContextOwinKey, dotvvmContext);

            foreach (var middleware in middlewares)
            {
                if (await middleware.Handle(dotvvmContext)) return;
            }

            await next(context);
        }


        public static IHttpContext ConvertHttpContext(HttpContext context)
        {
            var httpContext = context.Features.Get<IHttpContext>();
            if (httpContext == null)
            {
                httpContext = new DotvvmHttpContext(context)
                {
                    Response = new DotvvmHttpResponse(
                        context.Response,
                        httpContext,
                        new DotvvmHeaderCollection(context.Response.Headers)
                        ),
                    Request = new DotvvmHttpRequest(
                        context.Request,
                        httpContext,
                        new DotvvmHttpPathString(context.Request.Path),
                        new DotvvmHttpPathString(context.Request.PathBase),
                        new DotvvmQueryCollection(context.Request.Query),
                        new DotvvmHeaderCollection(context.Request.Headers),
                        new DotvvmCookieCollection(context.Request.Cookies)
                        )
                };

                context.Features.Set(httpContext);
            }
            return httpContext;
        }

        protected DotvvmRequestContext CreateDotvvmContext(HttpContext context)
        {
            return new DotvvmRequestContext()
            {
                HttpContext = ConvertHttpContext(context),
                Configuration = Configuration,
                ResourceManager = new ResourceManager(Configuration),
                ViewModelSerializer = Configuration.ServiceLocator.GetService<IViewModelSerializer>(),
            };
        }

        public static bool IsInCurrentVirtualDirectory(IHttpContext context, ref string url)
        {
            var virtualDirectory = GetVirtualDirectory(context);
            if (url.StartsWith(virtualDirectory, StringComparison.Ordinal))
            {
                url = url.Substring(virtualDirectory.Length).TrimStart('/');
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get clean request url without slashes.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetCleanRequestUrl(HttpContext context)
        {
            return context.Request.Path.Value.TrimStart('/').TrimEnd('/');
        }
    }
}