using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Routing;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.ViewModel.Serialization;
using Microsoft.Extensions.DependencyInjection;
using DotVVM.Framework.Utils;
using System.Text.Json;

namespace DotVVM.Framework.Hosting
{
    public class DotvvmRequestContext : IDotvvmRequestContext
    {
        public string? CsrfToken { get; set; }
        public JsonDocument? ReceivedViewModelJson { get; set; }


        /// <summary>
        /// Gets the route that was used for this request.
        /// </summary>
        public RouteBase? Route { get; set; }

        /// <summary>
        /// Determines whether this HTTP request is a postback or a classic GET request.
        /// </summary>
        public bool IsPostBack => RequestType == DotvvmRequestType.Command;

        /// <summary>
        /// Determines type of the request - initial GET, command, staticCommand, ...
        /// </summary>
        public DotvvmRequestType RequestType { get; }

        /// <summary>
        /// Gets the view model object for the current HTTP request.
        /// </summary>
        public object? ViewModel { get; set; }

        /// <summary>
        /// Gets the top-level control representing the whole view for the current request.
        /// </summary>
        public DotvvmView? View { get; set; }

        /// <summary>
        /// Gets the values of parameters specified in the <see cref="P:Route" /> property.
        /// </summary>
        public IDictionary<string, object?>? Parameters { get; set; }

        /// <summary>
        /// Gets the <see cref="IDotvvmPresenter"/> that is responsible for handling this HTTP request.
        /// </summary>
        public IDotvvmPresenter? Presenter { get; set; }

        /// <summary>
        /// Gets the global configuration of DotVVM.
        /// </summary>
        public DotvvmConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets the resource manager that is responsible for rendering script and stylesheet resources.
        /// </summary>
        private ResourceManager? _resourceManager;
        public ResourceManager ResourceManager => _resourceManager ?? (_resourceManager = Services.GetRequiredService<ResourceManager>());

        /// <summary>
        /// Gets the <see cref="ModelState"/> object that manages validation errors for the viewmodel in Command requests. In StaticCommand, please use <see cref="StaticCommandModelState"/>
        /// </summary>
        public ModelState ModelState { get; } = new ModelState();

        /// <summary>
        /// Gets the query string parameters specified in the URL of the current HTTP request.
        /// </summary>
        public IQueryCollection Query => HttpContext.Request.Query;

        /// <summary>
        /// Gets or sets the value indicating whether the exception that occurred in the command execution was handled. 
        /// This property is typically set from the exception filter's OnCommandException method.
        /// </summary>
        public bool IsCommandExceptionHandled { get; set; }


        /// <summary>
        /// Gets or sets the value indicating whether the exception that occurred during the page execution was handled and that the OnPageExceptionHandled will not be called on the next action filters. 
        /// This property is typically set from the action filter's OnPageExceptionHandled method.
        /// </summary>
        public bool IsPageExceptionHandled { get; set; }


        /// <summary>
        /// Gets or sets the exception that occurred when the command was executed.
        /// </summary>
        public Exception? CommandException { get; set; }

        /// <summary>
        /// Gets or sets new url fragment (the part after #) to be set on client
        /// </summary>
        public string? ResultIdFragment { get; set; }


        /// <summary>
        /// Gets a value indicating whether the HTTP request wants to render only content of a specific SpaContentPlaceHolder.
        /// </summary>
        public bool IsSpaRequest => RequestType is DotvvmRequestType.SpaNavigate;

        /// <summary>
        /// Gets a value indicating whether this HTTP request is made from single page application and only the SpaContentPlaceHolder content will be rendered.
        /// </summary>
        public bool IsInPartialRenderingMode => RequestType is DotvvmRequestType.Command or DotvvmRequestType.SpaNavigate;

        [Obsolete("Get the IViewModelSerializer from IServiceProvider")]
        public IViewModelSerializer ViewModelSerializer => Services.GetRequiredService<IViewModelSerializer>();

        private IServiceProvider? _services;

        public IServiceProvider Services
        {
            get => _services ?? (_services = Configuration.ServiceProvider ?? throw new NotSupportedException());
            set => _services = value;
        }

        public IHttpContext HttpContext { get; set; }

        public DotvvmRequestContext(
            IHttpContext httpContext,
            DotvvmConfiguration configuration,
            IServiceProvider? services,
            DotvvmRequestType? requestType = null)
        {
            if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            HttpContext = httpContext;
            RequestType = requestType ?? DetermineRequestType(httpContext);
            Configuration = configuration;
            _services = services;
        }

        public static DotvvmRequestType DetermineRequestType(IHttpContext context)
        {
            var method = context.Request.Method;
            if (method == "GET")
            {
                if (context.Request.Headers.ContainsKey(HostingConstants.SpaContentPlaceHolderHeaderName))
                {
                    return DotvvmRequestType.SpaNavigate;
                }
                return DotvvmRequestType.Navigate;
            }
            if (method == "POST")
            {
                if (context.Request.Headers.TryGetValue("X-PostbackType", out var postbackType))
                {
                    if (postbackType[0] == "StaticCommand")
                    {
                        return DotvvmRequestType.StaticCommand;
                    }
                }
                if (context.Request.Headers.ContainsKey(HostingConstants.PostBackHeaderName))
                {
                    return DotvvmRequestType.Command;
                }
                // Unknown POST request is treated as a Navigate request
                // it is useful for submitting classic <form method=POST> elements (as a no-JS fallback, login forms, etc.)
                return DotvvmRequestType.Navigate;
            }
            return DotvvmRequestType.Unknown;
        }

        /// <summary>
        /// Gets the current DotVVM context.
        /// </summary>
        public static DotvvmRequestContext GetCurrent(IHttpContext httpContext) => TryGetCurrent(httpContext).NotNull();

        /// <summary>
        /// Gets the current DotVVM context or null.
        /// </summary>
        public static DotvvmRequestContext? TryGetCurrent(IHttpContext httpContext)
        {
            return httpContext.GetItem<DotvvmRequestContext>(HostingConstants.DotvvmRequestContextKey);
        }

        public CustomResponsePropertiesManager CustomResponseProperties { get; } = new CustomResponsePropertiesManager();

    }
}
