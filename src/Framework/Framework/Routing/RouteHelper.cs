using DotVVM.Framework.Configuration;
using DotVVM.Framework.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotVVM.Framework.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.Framework.Routing
{
    public static class RouteHelper
    {
        public static void AutoRegisterRoutes(this DotvvmRouteTable routeTable, DotvvmConfiguration configuration, string path = "", string pattern = "*.dothtml") =>
            routeTable.AutoRegisterRoutes(configuration, null, path, pattern);
        public static void AutoRegisterRoutes(this DotvvmConfiguration config, string path = "", string pattern = "*.dothtml") =>
            config.RouteTable.AutoRegisterRoutes(config, null, path, pattern);

        public static void AutoRegisterRoutes(this DotvvmConfiguration config, Func<string, IEnumerable<string>> getRouteList, string path = "/", string pattern = "*.dothtml") =>
            config.RouteTable.AutoRegisterRoutes(config, getRouteList, path, pattern);
        public static void AutoRegisterRoutes(this DotvvmRouteTable routeTable, DotvvmConfiguration config, Func<string, IEnumerable<string>>? getRouteList, string path = "/", string pattern = "*.dothtml") =>
            routeTable.AutoDiscoverRoutes(new CustomListRoutingStrategy(config, getRouteList, path, pattern));

        /// <summary>
        /// Registers all routes discovered by specified <see cref="IRoutingStrategy"/> in the RouteTable.
        /// </summary>
        /// <param name="strategy">A strategy that provides list of routes.</param>
        /// <param name="table">A table of DotVVM routes by specified name.</param>
        public static void AutoDiscoverRoutes(this DotvvmRouteTable table, IRoutingStrategy strategy)
        {
            foreach (var route in strategy.GetRoutes())
            {
                table.Add(route);
            }
        }
        /// <summary>
        /// Verify whether all provided virtual paths exist and required properties are set. 
        /// </summary>
        /// <exception cref="DotvvmConfigurationException">Throws exception when configuration has invalid registrations.</exception> 
        public static void AssertConfigurationIsValid(this DotvvmConfiguration config)
        {
            var invalidRoutes = new List<DotvvmConfigurationAssertResult<RouteBase>>();
            var invalidControls = new List<DotvvmConfigurationAssertResult<DotvvmControlConfiguration>>();
            var loader = config.ServiceProvider.GetRequiredService<IMarkupFileLoader>();

            foreach (var route in config.RouteTable.Where(s => !string.IsNullOrWhiteSpace(s.VirtualPath)))
            {
                if (string.IsNullOrWhiteSpace(route.RouteName))
                {
                    invalidRoutes.Add(new DotvvmConfigurationAssertResult<RouteBase>(route, DotvvmConfigurationAssertReason.MissingRouteName));
                }

                var content = loader.GetMarkup(config, route.VirtualPath!);
                if (content == null)
                {
                    invalidRoutes.Add(new DotvvmConfigurationAssertResult<RouteBase>(route, DotvvmConfigurationAssertReason.MissingFile));
                }
            }
            var validControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var control in config.Markup.Controls)
            {

                if (string.IsNullOrEmpty(control.TagPrefix))
                {
                    throw new Exception("The TagPrefix must not be empty and must not contain non-alphanumeric characters!");       // TODO: exception handling
                }

                if (string.IsNullOrEmpty(control.TagName))
                {
                    if (!string.IsNullOrEmpty(control.Src) || string.IsNullOrEmpty(control.Namespace) || string.IsNullOrEmpty(control.Assembly))
                    {
                        invalidControls.Add(new DotvvmConfigurationAssertResult<DotvvmControlConfiguration>(control, DotvvmConfigurationAssertReason.InvalidCombination));
                    }
                }
                else
                {
                    // check controls are unique
                    if (!validControls.Add($"{control.TagPrefix}:{control.TagName}"))
                    {
                        invalidControls.Add(new DotvvmConfigurationAssertResult<DotvvmControlConfiguration>(
                            control,
                            DotvvmConfigurationAssertReason.Conflict
                        ));
                    }

                    if (string.IsNullOrEmpty(control.Src) || !string.IsNullOrEmpty(control.Namespace) || !string.IsNullOrEmpty(control.Assembly))
                    {
                        invalidControls.Add(new DotvvmConfigurationAssertResult<DotvvmControlConfiguration>(control, DotvvmConfigurationAssertReason.InvalidCombination));

                    }
                }

                if (!string.IsNullOrWhiteSpace(control.Src) && loader.GetMarkup(config, control.Src!) == null)
                {
                    invalidControls.Add(new DotvvmConfigurationAssertResult<DotvvmControlConfiguration>(control, DotvvmConfigurationAssertReason.MissingFile));
                }
            }

            if (invalidControls.Any() || invalidRoutes.Any())
            {
                throw new DotvvmConfigurationException(invalidRoutes, invalidControls);
            }

            ValidateFeatureFlags(config);
        }

        private static void ValidateFeatureFlags(DotvvmConfiguration config)
        {
            var routeNameSet = new HashSet<string>(config.RouteTable.Select(r => r.RouteName), StringComparer.Ordinal);
            var featureFlags = new DotvvmFeatureFlag[] {
                config.Security.FrameOptionsCrossOrigin,
                config.Security.FrameOptionsSameOrigin,
                config.Security.XssProtectionHeader,
                config.Security.ContentTypeOptionsHeader,
                config.Security.VerifySecFetchForCommands,
                config.Security.VerifySecFetchForPages,
                config.Security.RequireSecFetchHeaders,
                config.Security.ReferrerPolicy,
                config.ExperimentalFeatures.LazyCsrfToken,
                config.ExperimentalFeatures.ServerSideViewModelCache
            };
            var invalidRoutesInFlags = (
                    from flag in featureFlags
                    from route in Enumerable.Concat(flag.ExcludedRoutes, flag.IncludedRoutes)
                    where !config.RouteTable.Contains(route)
                    let include = flag.IncludedRoutes.Contains(route)
                    group (include, flag) by route into g
                    let includedFlags = g.Where(f => f.include).Select(f => f.flag)
                    let includedInStr = includedFlags.Any() ? $"included in feature flag {string.Join(", ", includedFlags.Select(f => f.FlagName))}" : ""
                    let excludedFlags = g.Where(f => !f.include).Select(f => f.flag)
                    let excludedInStr = excludedFlags.Any() ? $"excluded in feature flag {string.Join(", ", excludedFlags.Select(f => f.FlagName))}" : ""
                    select $"Route '{g.Key}' {includedInStr}{(includedInStr.Length > 0 && excludedInStr.Length > 0 ? " and " : "")}{excludedInStr} is not registered in the RouteTable"
                ).Take(4).ToArray();
            if (invalidRoutesInFlags.Any())
            {
                throw new DotvvmConfigurationException(string.Join("\n", invalidRoutesInFlags));
            }
        }
    }
}
