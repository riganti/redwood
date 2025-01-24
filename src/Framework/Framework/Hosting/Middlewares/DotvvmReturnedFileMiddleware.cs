using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using DotVVM.Core.Storage;
#if DotNetCore
using Microsoft.Net.Http.Headers;
#else
using System.Net.Http.Headers;
#endif

namespace DotVVM.Framework.Hosting.Middlewares
{
    public class DotvvmReturnedFileMiddleware : IMiddleware
    {
        public async Task<bool> Handle(IDotvvmRequestContext request)
        {
            var url = DotvvmMiddlewareBase.GetCleanRequestUrl(request.HttpContext);

            if (url.StartsWith("_dotvvm/returnedFile", StringComparison.Ordinal))
            {
                await ValidateSecFetch(request);
                await RenderReturnedFile(request.HttpContext, request.Services.GetRequiredService<IReturnedFileStorage>());
                return true;
            }
            else return false;
        }

        private async Task ValidateSecFetch(IDotvvmRequestContext c)
        {
            var dest = c.HttpContext.Request.Headers["Sec-Fetch-Dest"];
            var site = c.HttpContext.Request.Headers["Sec-Fetch-Site"];
            if (!string.IsNullOrEmpty(site))
            {
                // this will be validated always, this is designed to work with the redirect to file that we do
                // we want to prevent usage of the returned file anywhere where it should not be used, since
                // the user may have control over contents of the file and it may served from a trusted domain.
                // if you don't like this behavior, you can return the file from your own middleware,
                // we'll not add an option to disable this check.

                // in addition to navigation, we also allow usage from JS, since it simplifies service worker implementation
                // and there probably isn't a way to do harm (we need to prevent the files getting into script tags, styles, ...)
                if (!(site != "cross-site" && dest is "document" or "empty"))
                    await c.RejectRequest("Returned file can only be used from same-site navigation.");
            }
        }

        private async Task RenderReturnedFile(IHttpContext context, IReturnedFileStorage returnedFileStorage)
        {

            var id = Guid.Parse(context.Request.Query["id"]);

            var returnedFile = await returnedFileStorage.GetFileAsync(id);
            using (var stream = returnedFile.Stream)
            {
#if DotNetCore
                var contentDispositionValue = new ContentDispositionHeaderValue(returnedFile.Metadata.AttachmentDispositionType);
                contentDispositionValue.SetHttpFileName(returnedFile.Metadata.FileName);
                context.Response.Headers[HeaderNames.ContentDisposition] = contentDispositionValue.ToString();
#else
                var contentDispositionValue = new ContentDispositionHeaderValue(returnedFile.Metadata.AttachmentDispositionType)
                {
                    FileName = returnedFile.Metadata.FileName,
                    FileNameStar = returnedFile.Metadata.FileName
                };
                context.Response.Headers["Content-Disposition"] = contentDispositionValue.ToString();
#endif
                context.Response.ContentType = returnedFile.Metadata.MimeType;
                if (returnedFile.Metadata.AdditionalHeaders != null)
                {
                    foreach (var header in returnedFile.Metadata.AdditionalHeaders)
                    {
                        context.Response.Headers.Add(new KeyValuePair<string, string[]>(header.Key, header.Value));
                    }
                }

                if (stream.CanSeek)
                    context.Response.Headers["Content-Length"] = stream.Length.ToString();

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await stream.CopyToAsync(context.Response.Body);
            }
        }
    }
}
