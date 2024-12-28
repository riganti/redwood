#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Routing;
using DotVVM.Framework.Runtime.Tracing;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.Framework.Testing
{
    public class TestDotvvmRequestContext : IDotvvmRequestContext
    {
        public IHttpContext HttpContext { get; set; }
        public string CsrfToken { get; set; }
        public JsonDocument ReceivedViewModelJson { get; set; }
        public object ViewModel { get; set; }
        public DotvvmConfiguration Configuration { get; set; }
        public IDotvvmPresenter Presenter { get; set; }
        public RouteBase Route { get; set; }
        public bool IsPostBack => RequestType == DotvvmRequestType.Command;
        public DotvvmRequestType RequestType { get; set; } = DotvvmRequestType.Navigate;
        public IDictionary<string, object> Parameters { get; set; }
        public ResourceManager ResourceManager { get; set; }
        public ModelState ModelState { get; set; }
        public IQueryCollection Query { get; set; }
        public bool IsCommandExceptionHandled { get; set; }
        public bool IsPageExceptionHandled { get; set; }
        public Exception CommandException { get; set; }
        public bool IsSpaRequest => RequestType == DotvvmRequestType.SpaNavigate;
        public bool IsInPartialRenderingMode => RequestType is DotvvmRequestType.SpaNavigate or DotvvmRequestType.Command;
        public string ApplicationHostPath { get; set; }
        public string ResultIdFragment { get; set; }
        public DotvvmView View { get; set; }

        private IServiceProvider _services;

        public IServiceProvider Services
        {
            get => _services ?? Configuration?.ServiceProvider ?? throw new NotSupportedException();
            set => _services = value;
        }

        public CustomResponsePropertiesManager CustomResponseProperties { get; } = new CustomResponsePropertiesManager();

        public TestDotvvmRequestContext() { }
        public TestDotvvmRequestContext(IServiceProvider services)
        {
            this.Services = services;
            this.Configuration = services.GetService<DotvvmConfiguration>();
            this.ResourceManager = services.GetService<ResourceManager>();
        }
    }
}
