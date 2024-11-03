using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;

namespace DotVVM.Framework.Routing
{
    /// <summary>
    /// Represents a localizable route with different matching pattern for each culture.
    /// Please note that the extraction of the culture from the URL and setting the culture must be done at the beginning of the request pipeline.
    /// Therefore, the route only matches the URL for the current culture.
    /// </summary>
    public sealed class LocalizedDotvvmRoute : RouteBase, IPartialMatchRoute
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

        public override IEnumerable<KeyValuePair<string, DotvvmRouteParameterMetadata>> ParameterMetadata => GetRouteForCulture(CultureInfo.CurrentUICulture).ParameterMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmRoute"/> class.
        /// </summary>
        public LocalizedDotvvmRoute(string defaultLanguageUrl, LocalizedRouteUrl[] localizedUrls, string? virtualPath, string name, object? defaultValues, Func<IServiceProvider, IDotvvmPresenter> presenterFactory, DotvvmConfiguration configuration)
            : base(defaultLanguageUrl, virtualPath, name, defaultValues, presenterFactory)
        {
            if (!localizedUrls.Any())
            {
                throw new ArgumentException("There must be at least one localized route URL!", nameof(localizedUrls));
            }

            var defaultRoute = new DotvvmRoute(defaultLanguageUrl, virtualPath, name, defaultValues, presenterFactory, configuration);

            var sortedParameters = defaultRoute.ParameterMetadata
                .OrderBy(n => n.Key)
                .ToArray();

            foreach (var localizedUrl in localizedUrls)
            {
                var localizedRoute = new DotvvmRoute(localizedUrl.RouteUrl, virtualPath, name, defaultValues, presenterFactory, configuration);
                if (!localizedRoute.ParameterMetadata.OrderBy(n => n.Key)
                        .SequenceEqual(sortedParameters))
                {
                    throw new ArgumentException($"Localized route URL '{localizedUrl.RouteUrl}' must contain the same parameters with equal constraints as the default route URL!", nameof(localizedUrls));
                }

                localizedRoutes.Add(localizedUrl.CultureIdentifier, localizedRoute);
            }

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

        public IReadOnlyDictionary<string, DotvvmRoute> GetAllCultureRoutes() => localizedRoutes;

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

        public bool IsPartialMatch(string url, [MaybeNullWhen(false)] out RouteBase matchedRoute, [MaybeNullWhen(false)] out IDictionary<string, object?> values)
        {
            RouteBase? twoLetterCultureMatch = null;
            IDictionary<string, object?>? twoLetterCultureMatchValues = null;

            foreach (var route in localizedRoutes)
            {
                if (route.Value.IsMatch(url, out values))
                {
                    if (route.Key.Length > 2)
                    {
                        // exact culture match - return immediately
                        matchedRoute = route.Value;
                        return true;
                    }
                    else if (route.Key.Length > 0 && twoLetterCultureMatch == null)
                    {
                        // match for two-letter culture - continue searching if there is a better match
                        twoLetterCultureMatch = route.Value;
                        twoLetterCultureMatchValues = values;
                    }
                    else
                    {
                        // ignore exact match - this was done using classic IsMatch
                    }
                }
            }

            if (twoLetterCultureMatch != null)
            {
                matchedRoute = twoLetterCultureMatch;
                values = twoLetterCultureMatchValues!;
                return true;
            }

            matchedRoute = null;
            values = null;
            return false;
        }

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
