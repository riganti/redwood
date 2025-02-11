using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.Logging;

namespace DotVVM.Framework.Hosting.ErrorPages
{
    public class DotvvmErrorPageRenderer
    {
        private readonly ILogger<DotvvmErrorPageRenderer>? logger;
        private readonly DotvvmConfiguration config;

        public ErrorFormatter? Formatter { get; set; }

        public DotvvmErrorPageRenderer(DotvvmConfiguration config, ILogger<DotvvmErrorPageRenderer>? logger = null)
        {
            this.config = config;
            this.logger = logger;
        }

        /// <summary>
        /// Renders the error response.
        /// </summary>
        public Task RenderErrorResponse(IHttpContext context, Exception error)
        {
            try
            {
                // EventId is the same as ASP.NET Core error page uses: https://github.com/dotnet/aspnetcore/blob/9068fcf8cfe289735c0f8244aedf6b7798523cbe/src/Middleware/Diagnostics/src/DiagnosticsLoggerExtensions.cs#L11
                logger?.LogError(new EventId(1, "UnhandledException"), error, "An unhandled exception has occurred while executing the request.");

                var text = (Formatter ?? (Formatter = CreateDefaultWithDemystifier()))
                    .ErrorHtml(error, context);
                context.Response.ContentType = "text/html; charset=utf-8";
                return context.Response.WriteAsync(text);
            }
            catch (Exception exc)
            {
                return RenderFallbackMessage(context, error, exc);
            }
        }

        private static async Task RenderFallbackMessage(IHttpContext context, Exception error, Exception exc)
        {
            try
            {
                context.Response.ContentType = "text/plain; charset=utf-8";
                using (var writer = new StreamWriter(context.Response.Body))
                {
                    await writer.WriteLineAsync("Error in DotVVM Application:");
                    await writer.WriteLineAsync(error.ToString());
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync("Error occurred while displaying the error page. This is internal error and should not happen, please report it:");
                    await writer.WriteLineAsync(exc.ToString());
                }
            }
            catch { }
            throw new Exception("Error occurred inside DotVVM error handler, this is internal error and should not happen; \n Original error:" + error.ToString(), exc);
        }

        private ErrorFormatter CreateDefaultWithDemystifier()
        {
            var errorFormatter = ErrorFormatter.CreateDefault(config);

            var insertPosition = errorFormatter.Formatters.Count > 0 ? 1 : 0;

            return errorFormatter;
        }
    }
}
