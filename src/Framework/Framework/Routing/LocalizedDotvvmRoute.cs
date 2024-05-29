﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;

namespace DotVVM.Framework.Routing
{
    /// <summary>
    /// Represents a localizable route with different matching pattern for each culture.
    /// Please note that the extraction of the culture from the URL and setting the culture must be done at the beginning of the request pipeline.
    /// Therefore, the route only matches the URL for the current culture.
    /// </summary>
    public sealed class LocalizedDotvvmRoute : RouteBase
    {
        private static readonly HashSet<string> AvailableCultureNames = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(c => c != CultureInfo.InvariantCulture)
            .Select(c => c.Name)
            .ToHashSet();

        private readonly SortedDictionary<string, DotvvmRoute> localizedRoutes = new();

        public override string UrlWithoutTypes => GetRouteForCulture(CultureInfo.CurrentUICulture).UrlWithoutTypes;

        /// <summary>
        /// Gets the names of the route parameters in the order in which they appear in the URL.
        /// </summary>
        public override IEnumerable<string> ParameterNames => GetRouteForCulture(CultureInfo.CurrentUICulture).ParameterNames;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmRoute"/> class.
        /// </summary>
        public LocalizedDotvvmRoute(string defaultLanguageUrl, LocalizedRouteUrl[] localizedUrls, string virtualPath, object? defaultValues, Func<IServiceProvider, IDotvvmPresenter> presenterFactory, DotvvmConfiguration configuration)
            : base(defaultLanguageUrl, virtualPath, defaultValues)
        {
            if (!localizedUrls.Any())
            {
                throw new ArgumentException("There must be at least one localized route URL!", nameof(localizedUrls));
            }

            foreach (var localizedUrl in localizedUrls)
            {
                var localizedRoute = new DotvvmRoute(localizedUrl.RouteUrl, virtualPath, defaultValues, presenterFactory, configuration);
                localizedRoutes.Add(localizedUrl.CultureIdentifier, localizedRoute);
            }

            var defaultRoute = new DotvvmRoute(defaultLanguageUrl, virtualPath, defaultValues, presenterFactory, configuration);
            localizedRoutes.Add("", defaultRoute);
        }

        public DotvvmRoute GetRouteForCulture(string cultureIdentifier)
        {
            ValidateCultureName(cultureIdentifier);
            return GetRouteForCulture(CultureInfo.GetCultureInfo(cultureIdentifier));
        }

        public DotvvmRoute GetRouteForCulture(CultureInfo culture)
        {
            return localizedRoutes.TryGetValue(culture.Name, out var exactMatchRoute) ? exactMatchRoute
                : localizedRoutes.TryGetValue(culture.TwoLetterISOLanguageName, out var languageMatchRoute) ? languageMatchRoute
                : localizedRoutes.TryGetValue("", out var defaultRoute) ? defaultRoute
                : throw new NotSupportedException("Invalid localized route - no default route found!");
        }

        public static void ValidateCultureName(string cultureIdentifier)
        {
            if (!AvailableCultureNames.Contains(cultureIdentifier))
            {
                throw new ArgumentException($"Culture {cultureIdentifier} was not found!", nameof(cultureIdentifier));
            }
        }

        /// <summary>
        /// Processes the request.
        /// </summary>
        public override IDotvvmPresenter GetPresenter(IServiceProvider provider) => GetRouteForCulture(CultureInfo.CurrentCulture).GetPresenter(provider);

        /// <summary>
        /// Determines whether the route matches to the specified URL and extracts the parameter values.
        /// </summary>
        public override bool IsMatch(string url, [MaybeNullWhen(false)] out IDictionary<string, object?> values) => GetRouteForCulture(CultureInfo.CurrentCulture).IsMatch(url, out values);

        protected internal override string BuildUrlCore(Dictionary<string, object?> values) => GetRouteForCulture(CultureInfo.CurrentCulture).BuildUrlCore(values);

        protected override void Freeze2()
        {
            foreach (var route in localizedRoutes)
            {
                route.Value.Freeze();
            }
        }
    }
}
