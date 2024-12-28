using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Binding;
using System.Text;
using DotVVM.Framework.Configuration;

namespace DotVVM.Framework.Runtime
{
    public class DefaultOutputRenderer : IOutputRenderer
    {
        protected virtual MemoryStream RenderPage(IDotvvmRequestContext context, DotvvmView view)
        {
            var outStream = new MemoryStream();
            using (var textWriter = new StreamWriter(outStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 4096, leaveOpen: true))
            {
                var htmlWriter = new HtmlWriter(textWriter, context);
                view.Render(htmlWriter, context);
            }
            outStream.Position = 0;
            return outStream;
        }

        public virtual async Task WriteHtmlResponse(IDotvvmRequestContext context, DotvvmView view)
        {
            // return the response
            context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
            SetCacheHeaders(context.HttpContext);
            using (var html = RenderPage(context, view))
            {
                CheckRenderedResources(context);
                context.HttpContext.Response.Headers["Content-Length"] = html.Length.ToString();
                await html.CopyToAsync(context.HttpContext.Response.Body);
            }
        }

        private void CheckRenderedResources(IDotvvmRequestContext context)
        {
            var resourceManager = context.ResourceManager;
            if (!resourceManager.BodyRendered || !resourceManager.HeadRendered)
                throw new Exception($"Required resources were not rendered, make sure that page contains <head> and <body> elements or <dot:HeadResourceLinks> and <dot:BodyResourceLinks> controls.");
        }

        public virtual IEnumerable<(string name, string html)> RenderPostbackUpdatedControls(IDotvvmRequestContext context, DotvvmView page)
        {
            var stack = new Stack<DotvvmControl>();
            stack.Push(page);
            do
            {
                var control = stack.Pop();

                if (control.properties.TryGet(PostBack.UpdateProperty, out var val) && true.Equals(val))
                {
                    using (var w = new StringWriter())
                    {
                        control.Render(new HtmlWriter(w, context), context);

                        var clientId = control.GetDotvvmUniqueId().ValueOrDefault;
                        if (clientId == null)
                        {
                            throw new DotvvmControlException(control, "This control cannot use PostBack.Update=\"true\" because it has dynamic ID. This happens when the control is inside a Repeater or other data-bound control and the RenderSettings.Mode=\"Client\".");
                        }
                        var result = w.ToString();
                        if (string.IsNullOrWhiteSpace(result))
                        {
                            throw new DotvvmControlException(control, "The PostBack.Update=\"true\" property is set on this control, but the control does not render anything. ");
                        }
                        if (!result.Contains(clientId))
                        {
                            throw new DotvvmControlException(control, "The PostBack.Update=\"true\" property is set on this control, but the control does not render the correct client ID.");
                        }
                        yield return (clientId, result);
                    }
                }
                else
                {
                    foreach (var child in control.Children)
                    {
                        stack.Push(child);
                    }
                }

            } while (stack.Count > 0);
        }


        public virtual async Task WriteViewModelResponse(IDotvvmRequestContext context, DotvvmView view, string serializedViewModel)
        {
            // return the response
            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
            SetCacheHeaders(context.HttpContext);
            await context.HttpContext.Response.WriteAsync(serializedViewModel);
        }

        public virtual async Task WriteStaticCommandResponse(IDotvvmRequestContext context, ReadOnlyMemory<byte> json)
        {
            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
            SetCacheHeaders(context.HttpContext);
            await context.HttpContext.Response.WriteAsync(json);
        }

        public virtual async Task RenderPlainJsonResponse(IHttpContext context, string json)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            SetCacheHeaders(context);
            await context.Response.WriteAsync(json);
        }
        public virtual async Task RenderPlainJsonResponse(IHttpContext context, ReadOnlyMemory<byte> json)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            SetCacheHeaders(context);
            await context.Response.WriteAsync(json);
        }

        public virtual async Task RenderHtmlResponse(IHttpContext context, string html)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "text/html; charset=utf-8";
            SetCacheHeaders(context);
            await context.Response.WriteAsync(html);
        }

        public virtual async Task RenderPlainTextResponse(IHttpContext context, string text)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "text/plain; charset=utf-8";
            SetCacheHeaders(context);
            await context.Response.WriteAsync(text);
        }

        private static void SetCacheHeaders(IHttpContext context)
        {
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "-1";
        }
    }
}
