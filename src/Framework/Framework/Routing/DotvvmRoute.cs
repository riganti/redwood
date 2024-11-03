using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Hosting;
using System.Text.RegularExpressions;
using DotVVM.Framework.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace DotVVM.Framework.Routing
{
    public sealed class DotvvmRoute : RouteBase
    {
        private Regex routeRegex;
        private List<Func<Dictionary<string, string?>, string>> urlBuilders;
        private List<KeyValuePair<string, Func<string, ParameterParseResult>?>> parameters;
        private string urlWithoutTypes;
        private List<KeyValuePair<string, DotvvmRouteParameterMetadata>> parameterMetadata;

        /// <summary>
        /// Gets the names of the route parameters in the order in which they appear in the URL.
        /// </summary>
        public override IEnumerable<string> ParameterNames => parameters.Select(p => p.Key);

        public override IEnumerable<KeyValuePair<string, DotvvmRouteParameterMetadata>> ParameterMetadata => parameterMetadata;

        public override string UrlWithoutTypes => urlWithoutTypes;


        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmRoute"/> class.
        /// </summary>
        public DotvvmRoute(string url, string? virtualPath, string name, object? defaultValues, Func<IServiceProvider, IDotvvmPresenter> presenterFactory, DotvvmConfiguration configuration)
            : base(url, virtualPath, name, defaultValues, presenterFactory)
        {
            ParseRouteUrl(configuration);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmRoute"/> class.
        /// </summary>
        public DotvvmRoute(string url, string? virtualPath, string name, IDictionary<string, object?>? defaultValues, Func<IServiceProvider, IDotvvmPresenter> presenterFactory, DotvvmConfiguration configuration)
            : base(url, virtualPath, name, defaultValues, presenterFactory)
        {
            ParseRouteUrl(configuration);
        }


        /// <summary>
        /// Parses the route URL and extracts the components.
        /// </summary>
        [MemberNotNull(nameof(routeRegex), nameof(urlBuilders), nameof(parameters), nameof(parameterMetadata), nameof(urlWithoutTypes))]
        private void ParseRouteUrl(DotvvmConfiguration configuration)
        {
            var parser = new DotvvmRouteParser(configuration.RouteConstraints);

            var result = parser.ParseRouteUrl(Url, DefaultValues);

            routeRegex = result.RouteRegex;
            urlBuilders = result.UrlBuilders;
            parameters = result.Parameters;
            parameterMetadata = result.ParameterMetadata;
            urlWithoutTypes = result.UrlWithoutTypes;
        }

        /// <summary>
        /// Determines whether the route matches to the specified URL and extracts the parameter values.
        /// </summary>
        public override bool IsMatch(string url, [MaybeNullWhen(false)] out IDictionary<string, object?> values)
        {
            if (!url.StartsWith("/"))
                url = '/' + url;

            var match = routeRegex.Match(url);
            if (!match.Success)
            {
                values = null!;
                return false;
            }

            values = CloneDefaultValues();
            foreach (var parameter in parameters)
            {
                var g = match.Groups["param" + parameter.Key];
                if (g.Success && g.Length > 0)
                {
                    var decodedValue = Uri.UnescapeDataString(g.Value);
                    if (parameter.Value != null)
                    {
                        var r = parameter.Value(decodedValue);
                        if (!r.IsOK) return false;
                        values[parameter.Key] = r.Value;
                    }
                    else
                        values[parameter.Key] = decodedValue;
                }
                else if (DefaultValues.TryGetValue(parameter.Key, out var defaultValue))
                {
                    values[parameter.Key] = defaultValue;
                }
            }
            return true;
        }

        /// <summary>
        /// Builds the URL core from the parameters.
        /// </summary>
        protected internal override string BuildUrlCore(Dictionary<string, object?> values)
        {
            var convertedValues =
                values.ToDictionary(
                    v => v.Key,
                    v => UrlHelper.ParameterToString(v.Value) is string x ? Uri.EscapeDataString(x) : null,
                    StringComparer.OrdinalIgnoreCase
                );
            try
            {
                var parts = new string[urlBuilders.Count];
                for (int i = 0; i < urlBuilders.Count; i++)
                {
                    parts[i] = urlBuilders[i](convertedValues);
                }
                var url = string.Concat(parts);

                if (url == "~")
                    return "~/";

                return url;
            }
            catch (Exception ex)
            {
                throw new DotvvmRouteException($"Could not build URL for route '{ this.Url }' with values {{{ string.Join(", ", values.Select(kvp => kvp.Key + ": " + kvp.Value)) }}}", this, ex);
            }
        }

        protected override void Freeze2()
        {
            // there is no property that would have to be frozen
        }
    }
}
