using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Parser.Dothtml.Parser;
using DotVVM.Framework.Compilation.Parser.Dothtml.Tokenizer;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime.Caching;
using DotVVM.Framework.Security;
using DotVVM.Framework.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.Styles;
using DotVVM.Framework.Compilation.Validation;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Framework.Compilation.ViewCompiler;
using DotVVM.Framework.Routing;
using DotVVM.Framework.ResourceManagement;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace DotVVM.Framework.Testing
{
    public static class DotvvmTestHelper
    {
        public class FakeProtector : IViewModelProtector
        {
            public static readonly byte[] WarningPrefixBytes = Convert.FromBase64String("WARNING/NOT/ENCRYPTED+++");

            public byte[] Protect(byte[] serializedData, IDotvvmRequestContext context)
            {
                // I hope I will not see this message anywhere on the web ;)
                return [ ..WarningPrefixBytes, ..serializedData];
            }

            public byte[] Protect(byte[] plaintextData, params string[] purposes)
            {
                return [ ..WarningPrefixBytes, ..plaintextData];
            }

            public byte[] Unprotect(byte[] protectedData, IDotvvmRequestContext context)
            {
                if (!protectedData.AsSpan().StartsWith(WarningPrefixBytes)) throw new SecurityException($"");
                return protectedData.AsSpan(WarningPrefixBytes.Length).ToArray();
            }

            public byte[] Unprotect(byte[] protectedData, params string[] purposes)
            {
                if (!protectedData.AsSpan().StartsWith(WarningPrefixBytes)) throw new SecurityException($"");
                return protectedData.AsSpan(WarningPrefixBytes.Length).ToArray();
            }
        }

        public class NopProtector : IViewModelProtector
        {
            public byte[] Protect(byte[] serializedData, IDotvvmRequestContext context) => Convert.FromBase64String("XXXX");
            public byte[] Protect(byte[] plaintextData, params string[] purposes) => Convert.FromBase64String("XXXX");
            public byte[] Unprotect(byte[] protectedData, IDotvvmRequestContext context) => throw new NotImplementedException();
            public byte[] Unprotect(byte[] protectedData, params string[] purposes) => throw new NotImplementedException();
        }

        public class FakeCsrfProtector : ICsrfProtector
        {
            public string GenerateToken(IDotvvmRequestContext context)
            {
                return "Not a CSRF token.";
            }

            public void VerifyToken(IDotvvmRequestContext context, string token)
            {
                if (token != "Not a CSRF token.")
                    throw new Exception();
            }
        }

        public static void RegisterMockServices(IServiceCollection services)
        {
            services.TryAddSingleton<IViewModelProtector, FakeProtector>();
            services.TryAddSingleton<ICsrfProtector, FakeCsrfProtector>();
            services.TryAddSingleton<IDotvvmCacheAdapter, SimpleDictionaryCacheAdapter>();
            services.AddSingleton<CompiledAssemblyCache>(s => sharedAssemblyCache.Value);
            services.AddSingleton<ExtensionMethodsCache>(s => sharedExtensionMethodCache.Value);
        }

        private static Lazy<CompiledAssemblyCache> sharedAssemblyCache =
            new (() => new CompiledAssemblyCache(DefaultConfig));
        private static Lazy<ExtensionMethodsCache> sharedExtensionMethodCache =
            new (() => new ExtensionMethodsCache(sharedAssemblyCache.Value));

        private static Lazy<DotvvmConfiguration> _defaultConfig = new Lazy<DotvvmConfiguration>(() => {
            var config = CreateConfiguration();
            config.ExperimentalFeatures.UseDotvvmSerializationForStaticCommandArguments.Enable();
            config.RouteTable.Add("TestRoute", "TestRoute", "TestView.dothtml");
            config.Diagnostics.Apply(config);
            config.Freeze();
            return config;
        });
        public static DotvvmConfiguration DefaultConfig => _defaultConfig.Value;

        private static Lazy<DotvvmConfiguration> _debugConfig = new Lazy<DotvvmConfiguration>(() => {
            var config = CreateConfiguration();
            config.ExperimentalFeatures.UseDotvvmSerializationForStaticCommandArguments.Enable();
            config.RouteTable.Add("TestRoute", "TestRoute", "TestView.dothtml");
            config.Debug = true;
            config.Diagnostics.Apply(config);
            config.Freeze();
            return config;
        });
        public static DotvvmConfiguration DebugConfig => _debugConfig.Value;

        public static DotvvmConfiguration CreateConfiguration(Action<IServiceCollection>? customServices = null) =>
            DotvvmConfiguration.CreateDefault(s => {
                s.AddSingleton<ITestSingletonService, TestSingletonService>();
                LoggingServiceCollectionExtensions.AddLogging(s, log => {
                    log.AddConsole();
                });
                customServices?.Invoke(s);
                RegisterMockServices(s);
            });

        public static TestDotvvmRequestContext CreateContext(
            DotvvmConfiguration? configuration = null,
            RouteBase? route = null,
            DotvvmRequestType requestType = DotvvmRequestType.Navigate)
        {
            configuration = configuration ?? DefaultConfig;
            var httpContext = new TestHttpContext();
            IServiceProvider services = configuration.ServiceProvider.CreateScope().ServiceProvider;
            var context = new TestDotvvmRequestContext() {
                Configuration = configuration,
                Services = services,
                CsrfToken = "Test CSRF Token",
                ModelState = new ModelState(),
                ResourceManager = services.GetService<ResourceManager>(),
                HttpContext = httpContext,
                Parameters = new Dictionary<string, object>(),
                Route = route ?? configuration.RouteTable.FirstOrDefault(),
                RequestType = requestType
            };
            if (context.Route != null)
            {
                httpContext.Request.Path = context.TranslateVirtualPath(context.Route.BuildUrl(context.Parameters));
            }
            services.GetRequiredService<DotvvmRequestContextStorage>().Context = context;
            return context;
        }

        public static void CheckForErrors(DothtmlNode? node)
        {
            if (node is null) return;

            foreach (var n in node.EnumerateNodes())
                if (n.HasNodeErrors)
                    throw new DotvvmCompilationException(string.Join(", ", n.NodeErrors), n.Tokens);
        }
        public static ResolvedTreeRoot ParseResolvedTree(string markup, string fileName = "default.dothtml", DotvvmConfiguration? configuration = null, bool checkErrors = true)
        {
            configuration = configuration ?? DefaultConfig;

            var tokenizer = new DothtmlTokenizer();
            tokenizer.Tokenize(markup);

            var parser = new DothtmlParser();
            var tree = parser.Parse(tokenizer.Tokens);

            if (checkErrors) CheckForErrors(tree);

            var controlTreeResolver = configuration.ServiceProvider.GetRequiredService<IControlTreeResolver>();
            var controlResolver = configuration.ServiceProvider.GetRequiredService<IControlResolver>();
            var validator = ActivatorUtilities.CreateInstance<ControlUsageValidationVisitor>(configuration.ServiceProvider);

            var visitors = configuration.ServiceProvider.GetRequiredService<IOptions<ViewCompilerConfiguration>>().Value.TreeVisitors;
            var root = controlTreeResolver.ResolveTree(tree, fileName).CastTo<ResolvedTreeRoot>();
            if (checkErrors) CheckForErrors(root.DothtmlNode);
            foreach (var v in visitors)
            {
                v().VisitView(root);
            }
            if (checkErrors)
                validator.VisitAndAssert(root);
            else
                validator.VisitView(root);
            return root;
        }

        public static void EnsureCompiledAssemblyCache()
        {
            if (CompiledAssemblyCache.Instance == null)
            {
                new CompiledAssemblyCache(DefaultConfig);
            }
        }

        public static void RunInCulture(CultureInfo cultureInfo, Action action)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUICulture = CultureInfo.CurrentUICulture;

            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            try
            {
                action();
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        }
        public static async Task RunInCultureAsync(CultureInfo cultureInfo, Func<Task> action)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUICulture = CultureInfo.CurrentUICulture;

            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = cultureInfo;
            try
            {
                await action();
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        }


        public interface ITestSingletonService { }
        public class TestSingletonService: ITestSingletonService { }
    }
}
