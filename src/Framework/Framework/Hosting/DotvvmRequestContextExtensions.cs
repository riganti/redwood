using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Storage;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using DotVVM.Framework.Hosting.Middlewares;
using DotVVM.Framework.ViewModel.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using DotVVM.Framework.Routing;
using DotVVM.Framework.Hosting;
using DotVVM.Core.Storage;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.ViewModel.Validation;

public static class DotvvmRequestContextExtensions
{
    /// <summary>
    /// Gets the unique id of the SpaContentPlaceHolder that should be loaded.
    /// </summary>
    public static string? GetSpaContentPlaceHolderUniqueId(this IDotvvmRequestContext context)
    {
        return DotvvmPresenter.DetermineSpaContentPlaceHolderUniqueId(context.HttpContext);
    }

    /// <summary>
    /// Gets cancellation token for the request
    /// </summary>
    public static CancellationToken GetCancellationToken(this IDotvvmRequestContext context)
    {
        var cancellationService = context.Services.GetRequiredService<IRequestCancellationTokenProvider>();
        return cancellationService.GetCancellationToken(context);
    }

    /// <summary>
    /// Changes the current culture of this HTTP request.
    /// </summary>
    [Obsolete("This method only assigns CultureInfo.CurrentCulture, which is not preserved in async methods. You should assign it manually, or use RequestLocalization middleware or LocalizablePresenter.")]
    public static void ChangeCurrentCulture(this IDotvvmRequestContext context, string cultureName)
        => context.ChangeCurrentCulture(cultureName, cultureName);

    /// <summary>
    /// Changes the current culture of this HTTP request.
    /// </summary>
    [Obsolete("This method only assigns CultureInfo.CurrentCulture, which is not preserved in async methods. You should assign it manually, or use RequestLocalization middleware or LocalizablePresenter.")]
    public static void ChangeCurrentCulture(this IDotvvmRequestContext context, string cultureName, string uiCultureName)
    {
#if DotNetCore
        CultureInfo.CurrentCulture = new CultureInfo(cultureName);
        CultureInfo.CurrentUICulture = new CultureInfo(uiCultureName);
#else
        Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(uiCultureName);
#endif
    }

    /// <summary>
    /// Gets the current UI culture of this HTTP request.
    /// </summary>
    [Obsolete("This just returns CultureInfo.CurrentUICulture")]
    public static CultureInfo GetCurrentUICulture(this IDotvvmRequestContext context)
    {
        return CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// Gets the current culture of this HTTP request.
    /// </summary>
    [Obsolete("This just returns CultureInfo.CurrentCulture")]
    public static CultureInfo GetCurrentCulture(this IDotvvmRequestContext context)
    {
        return CultureInfo.CurrentCulture;
    }

    /// <summary>
    /// Interrupts the execution of the current request.
    /// </summary>
    [DebuggerHidden]
    public static void InterruptRequest(this IDotvvmRequestContext context)
    {
        throw new DotvvmInterruptRequestExecutionException();
    }

    /// <summary>
    /// Returns the redirect response and interrupts the execution of current request.
    /// </summary>
    public static void RedirectToUrl(this IDotvvmRequestContext context, string url, bool replaceInHistory = false, bool allowSpaRedirect = false)
    {
        context.SetRedirectResponse(context.TranslateVirtualPath(url), (int)HttpStatusCode.Redirect, replaceInHistory, allowSpaRedirect);
        throw new DotvvmInterruptRequestExecutionException(InterruptReason.Redirect, url);
    }

    /// <summary>
    /// Verifies that the URL is local and returns the redirect response and interrupts the execution of current request.
    /// </summary>
    public static void RedirectToLocalUrl(this IDotvvmRequestContext context, string url, bool replaceInHistory = false, bool allowSpaRedirect = false)
    {
        if (!UrlHelper.IsLocalUrl(url))
        {
            throw new InvalidOperationException($"The URL '{url}' is not local or contains invalid characters!");
        }

        context.RedirectToUrl(url, replaceInHistory, allowSpaRedirect);
    }

    /// <summary>
    /// Returns the redirect response and interrupts the execution of current request.
    /// </summary>
    public static void RedirectToRoute(this IDotvvmRequestContext context, string routeName, object? newRouteValues = null, bool replaceInHistory = false, bool allowSpaRedirect = true, string? urlSuffix = null, object? query = null)
    {
        var route = context.Configuration.RouteTable[routeName];
        var url = route.BuildUrl(context.Parameters!, newRouteValues) + UrlHelper.BuildUrlSuffix(urlSuffix, query);

        context.RedirectToUrl(url, replaceInHistory, allowSpaRedirect);
    }

    /// <summary>
    /// Returns the permanent redirect response and interrupts the execution of current request.
    /// </summary>
    public static void RedirectToUrlPermanent(this IDotvvmRequestContext context, string url, bool replaceInHistory = false, bool allowSpaRedirect = false)
    {
        context.SetRedirectResponse(context.TranslateVirtualPath(url), (int)HttpStatusCode.MovedPermanently, replaceInHistory, allowSpaRedirect);
        throw new DotvvmInterruptRequestExecutionException(InterruptReason.RedirectPermanent, url);
    }

    /// <summary>
    /// Returns the permanent redirect response and interrupts the execution of current request.
    /// </summary>
    public static void RedirectToRoutePermanent(this IDotvvmRequestContext context, string routeName, object? newRouteValues = null, bool replaceInHistory = false, bool allowSpaRedirect = true, string? urlSuffix = null, object? query = null)
    {
        var route = context.Configuration.RouteTable[routeName];
        var url = route.BuildUrl(context.Parameters!, newRouteValues) + UrlHelper.BuildUrlSuffix(urlSuffix, query);

        context.RedirectToUrlPermanent(url, replaceInHistory, allowSpaRedirect);
    }

    public static void SetRedirectResponse(this IDotvvmRequestContext context, string url, int statusCode = (int)HttpStatusCode.Redirect, bool replaceInHistory = false, bool allowSpaRedirect = false, string? downloadName = null) =>
        context.Configuration.ServiceProvider.GetRequiredService<IHttpRedirectService>().WriteRedirectResponse(context.HttpContext, url, statusCode, replaceInHistory, allowSpaRedirect, downloadName);

    internal static Task SetCachedViewModelMissingResponse(this IDotvvmRequestContext context)
    {
        context.HttpContext.Response.StatusCode = 200;
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        return context.HttpContext.Response.WriteAsync(DefaultViewModelSerializer.GenerateMissingCachedViewModelResponse());
    }

    /// <summary>
    /// Ends the request execution when the <see cref="ModelState"/> is not valid and displays the validation errors in <see cref="ValidationSummary"/> control.
    /// If it is valid, it does nothing.
    /// </summary>
    public static void FailOnInvalidModelState(this IDotvvmRequestContext context)
    {
        context.PreprocessModelState();
        if (!context.ModelState.IsValid)
        {
            DotvvmMetrics.ValidationErrorsReturned.Record(
                context.ModelState.ErrorsInternal.Count,
                context.RouteLabel(),
                context.RequestTypeLabel()
            );
            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
            context.HttpContext.Response
                .Write(context.Services.GetRequiredService<IViewModelSerializer>().SerializeModelState(context));
            throw new DotvvmInterruptRequestExecutionException(InterruptReason.ModelValidationFailed, "The ViewModel contains validation errors!");
        }
    }

    private static void PreprocessModelState(this IDotvvmRequestContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var modelStateErrorExpander = context.Services.GetRequiredService<IValidationErrorPathExpander>();
            modelStateErrorExpander.Expand(context.ModelState, context.ViewModel);
        }
    }

    /// <summary>
    /// Gets the serialized view model.
    /// </summary>
    public static string GetSerializedViewModel(this IDotvvmRequestContext context)
    {
        return context.Services.GetRequiredService<IViewModelSerializer>().SerializeViewModel(context);
    }

    /// <summary>
    /// Translates the virtual path (~/something) to the domain relative path (/virtualDirectory/something). 
    /// For example, when the app is configured to run in a virtual directory '/virtDir', the URL '~/myPage.dothtml' will be translated to '/virtDir/myPage.dothtml'.
    /// </summary>
    public static string TranslateVirtualPath(this IDotvvmRequestContext context, string virtualUrl)
    {
        return TranslateVirtualPath(virtualUrl, context.HttpContext);
    }

    /// <summary>
    /// Translates the virtual path (~/something) to the domain relative path (/virtualDirectory/something). 
    /// For example, when the app is configured to run in a virtual directory '/virtDir', the URL '~/myPage.dothtml' will be translated to '/virtDir/myPage.dothtml'.
    /// </summary>
    public static string TranslateVirtualPath(string virtualUrl, IHttpContext httpContext)
    {
        if (virtualUrl.StartsWith("~/", StringComparison.Ordinal))
        {
            var url = DotvvmMiddlewareBase.GetVirtualDirectory(httpContext) + "/" + virtualUrl.Substring(2);
            if (!url.StartsWith("/", StringComparison.Ordinal))
            {
                url = "/" + url;
            }
            return url;
        }
        else
        {
            return virtualUrl;
        }
    }

    /// <summary>
    /// Redirects the client to the specified file.
    /// </summary>
    [Obsolete("Use ReturnFileAsync() instead")]
    public static void ReturnFile(this IDotvvmRequestContext context, byte[] bytes, string fileName, string mimeType, IEnumerable<KeyValuePair<string, string>>? additionalHeaders = null, string? attachmentDispositionType = null) =>
        context.ReturnFile(new MemoryStream(bytes), fileName, mimeType, additionalHeaders, attachmentDispositionType);

    /// <summary>
    /// Redirects the client to the specified file.
    /// </summary>
    [Obsolete("Use ReturnFileAsync() instead")]
    public static void ReturnFile(this IDotvvmRequestContext context, Stream stream, string fileName, string mimeType, IEnumerable<KeyValuePair<string, string>>? additionalHeaders = null, string? attachmentDispositionType = null) =>
        context.ReturnFileAsync(stream, fileName, mimeType, additionalHeaders, attachmentDispositionType).GetAwaiter().GetResult();
    /// <summary>
    /// Redirects the client to the specified file.
    /// </summary>
    public static Task ReturnFileAsync(this IDotvvmRequestContext context, byte[] bytes, string fileName, string mimeType, IEnumerable<KeyValuePair<string, string>>? additionalHeaders = null, string? attachmentDispositionType = null) =>
        context.ReturnFileAsync(new MemoryStream(bytes), fileName, mimeType, additionalHeaders, attachmentDispositionType);

    /// <summary>
    /// Redirects the client to the specified file.
    /// </summary>
    public static async Task ReturnFileAsync(this IDotvvmRequestContext context, Stream stream, string fileName, string mimeType, IEnumerable<KeyValuePair<string, string>>? additionalHeaders = null, string? attachmentDispositionType = null)
    {
        var returnedFileStorage = context.Services.GetService<IReturnedFileStorage>();

        if (returnedFileStorage == null)
        {
            throw new DotvvmFileStorageMissingException($"Unable to resolve service for type '{typeof(IReturnedFileStorage).Name}'. " +
                $"Visit https://www.dotvvm.com/docs/tutorials/advanced-returning-files for more details!");
        }

        var metadata = new ReturnedFileMetadata()
        {
            FileName = fileName,
            MimeType = mimeType,
            AdditionalHeaders = additionalHeaders?.GroupBy(k => k.Key, k => k.Value)?.ToDictionary(k => k.Key, k => k.ToArray()),
            AttachmentDispositionType = attachmentDispositionType ?? "attachment"
        };

        var downloadAttribute = attachmentDispositionType == "inline" ? null : fileName;

        var generatedFileId = await returnedFileStorage.StoreFileAsync(stream, metadata).ConfigureAwait(false);
        context.SetRedirectResponse(context.TranslateVirtualPath("~/_dotvvm/returnedFile?id=" + generatedFileId), downloadName: downloadAttribute);
        throw new DotvvmInterruptRequestExecutionException(InterruptReason.ReturnFile, fileName);
    }

    internal static async Task RejectRequest(this IDotvvmRequestContext context, string message, int statusCode = 403)
    {
        DotvvmMetrics.RequestsRejected.Add(1,
            context.RouteLabel(),
            context.RequestTypeLabel());

        // push it as a warning to ILogger
        var msg = "request rejected: " + message;
        context.Services
            .GetRequiredService<RuntimeWarningCollector>()
            .Warn(new DotvvmRuntimeWarning(msg));
        context.HttpContext.Response.StatusCode = statusCode;
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(msg);
        throw new DotvvmInterruptRequestExecutionException(InterruptReason.RequestRejected, msg);
    }

    public static void DebugWarning(this IDotvvmRequestContext context, string message, Exception? relatedException = null, DotvvmBindableObject? relatedControl = null)
    {
        if (context.Configuration.Debug)
        {
            context.Services
                .GetRequiredService<RuntimeWarningCollector>()
                .Warn(new DotvvmRuntimeWarning(message, relatedException, relatedControl));
        }
    }
}
