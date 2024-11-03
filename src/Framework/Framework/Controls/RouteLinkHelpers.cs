using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Routing;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.ViewModel.Serialization;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Configuration;
using System.Collections.Immutable;
using System.Text.Json;

namespace DotVVM.Framework.Controls
{
    public static class RouteLinkHelpers
    {
        public static void WriteRouteLinkHrefAttribute(RouteLink control, IHtmlWriter writer, IDotvvmRequestContext context)
        {
            var routeName = control.RouteName;
            if (string.IsNullOrEmpty(routeName))
            {
                if (control.HasBinding(RouteLink.RouteNameProperty))
                    throw new DotvvmControlException(control, $"RouteName property is set to a binding {control.GetBinding(RouteLink.RouteNameProperty)} which evaluates to null. If you have placed the RouteLink in a Repeater, use server rendering - client-side rendering of variable RouteName is not supported.");
                else
                    throw new DotvvmControlException(control, "RouteName property is set to null.");
            }
            EnsureUsesOnlyDefinedParameters(routeName, control, context);

            // Render client-side knockout expression only if there exists a parameter with value binding
            var containsBinding =
                control.QueryParameters.RawValues.Any(p => p.Value is IValueBinding) ||
                control.Params.RawValues.Any(p => p.Value is IValueBinding) ||
                control.GetValueRaw(RouteLink.UrlSuffixProperty) is IValueBinding;

            if (containsBinding)
            {
                var group = new KnockoutBindingGroup();
                group.Add("href", GenerateKnockoutHrefExpression(routeName, control, context));
                writer.AddKnockoutDataBind("attr", group);
            }

            try
            {
                writer.AddAttribute("href", EvaluateRouteUrl(routeName, control, context));
            }
            catch when (!control.RenderOnServer && containsBinding)
            {
                // ignore exception when binding is also rendered
            }
        }

        private static void EnsureUsesOnlyDefinedParameters(string routeName, RouteLink control, IDotvvmRequestContext context)
        {
            var parameterReferences = control.Params;
            var route = context.Configuration.RouteTable[routeName];
            var parameterDefinitions = route.ParameterNames;

            var invalidReferences = parameterReferences.Where(param =>
                !parameterDefinitions.Contains(param.Key, StringComparer.OrdinalIgnoreCase));

            if (invalidReferences.Any())
            {
                var parameters = invalidReferences.Select(kv => kv.Key).ToImmutableArray();
                throw new RouteMissingParametersException(route, parameters) {
                    RelatedControl = control
                };
            }
        }

        public static string EvaluateRouteUrl(string routeName, RouteLink control, IDotvvmRequestContext context)
        {
            var urlSuffix = GenerateUrlSuffixCore(control.GetValue(RouteLink.UrlSuffixProperty) as string, control);
            var coreUrl = GenerateRouteUrlCore(routeName, control, context) + urlSuffix;

            if ((bool)control.GetValue(Internal.IsSpaPageProperty)! && !(bool)control.GetValue(Internal.UseHistoryApiSpaNavigationProperty)!)
            {
                return "#!/" + (coreUrl.StartsWith("~/", StringComparison.Ordinal) ? coreUrl.Substring(2) : coreUrl);
            }
            else
            {
                return context.TranslateVirtualPath(coreUrl);
            }
        }

        private static string GenerateRouteUrlCore(string routeName, RouteLink control, IDotvvmRequestContext context)
        {
            var route = GetRoute(context, routeName, control.Culture);
            var parameters = ComposeNewRouteParameters(control, context, route);

            // evaluate bindings on server
            foreach (var param in parameters
#if !DotNetCore
// .NET framework does not allow dictionary modification while it's being enumerated.
                .ToArray()
#endif
            )
            {
                if (param.Value is IStaticValueBinding binding)
                    parameters[param.Key] = binding.Evaluate(control);
            }

            // generate the URL
            return route.BuildUrl(parameters);
        }

        private static string GenerateUrlSuffixCore(string? urlSuffix, RouteLink control)
        {
            // generate the URL suffix
            var queryParams = control.QueryParameters.ToArray();
            Array.Sort(queryParams, (a, b) => a.Key.CompareTo(b.Key)); // deterministic order of query params
            return UrlHelper.BuildUrlSuffix(urlSuffix, queryParams);
        }

        private static RouteBase GetRoute(IDotvvmRequestContext context, string routeName, string? cultureIdentifier)
        {
            var route = context.Configuration.RouteTable[routeName];
            if (!string.IsNullOrEmpty(cultureIdentifier))
            {
                if (route is not LocalizedDotvvmRoute localizedRoute)
                {
                    throw new DotvvmControlException($"The route {routeName} is not localizable, the Culture property cannot be used!");
                }
                route = localizedRoute.GetRouteForCulture(cultureIdentifier!);
            }
            return route;
        }

        public static string GenerateKnockoutHrefExpression(string routeName, RouteLink control, IDotvvmRequestContext context)
        {
            var link = GenerateRouteLinkCore(routeName, control, context);

            var urlSuffix = GetUrlSuffixExpression(control);
            return $"'{context.TranslateVirtualPath("~/")}' + {link}{(urlSuffix == null ? "" : " + " + urlSuffix)}";
        }

        private static string? GetUrlSuffixExpression(RouteLink control)
        {
            var urlSuffixBase =
                control.GetValueBinding(RouteLink.UrlSuffixProperty)
                ?.Apply(binding => binding.GetKnockoutBindingExpression(control, unwrapped: true))
                ?? KnockoutHelper.MakeStringLiteral(control.UrlSuffix ?? "");
            var queryParamsArray = control.QueryParameters.RawValues.ToArray();
            Array.Sort(queryParamsArray, (a, b) => a.Key.CompareTo(b.Key)); // deterministic order of query params
            var queryParams = queryParamsArray.Select(p => TranslateRouteParameter(control, p, true)).StringJoin(",");

            // generate the function call
            return
                queryParamsArray.Length > 0 ? $"dotvvm.buildUrlSuffix({urlSuffixBase}, {{{queryParams}}})" :
                urlSuffixBase != "\"\"" ? urlSuffixBase :
                null;
        }

        private static string GenerateRouteLinkCore(string routeName, RouteLink control, IDotvvmRequestContext context)
        {
            var route = GetRoute(context, routeName, control.Culture);
            var parameters = ComposeNewRouteParameters(control, context, route);

            var parametersExpression = parameters.Select(p => TranslateRouteParameter(control, p)).StringJoin(",");
            // generate the function call

            return
                route.ParameterNames.Any()
                    ? $"dotvvm.buildRouteUrl({KnockoutHelper.MakeStringLiteral(route.UrlWithoutTypes)}, {{{parametersExpression}}})"
                    : KnockoutHelper.MakeStringLiteral(route.Url);
        }

        private static string TranslateRouteParameter<T>(DotvvmBindableObject control, KeyValuePair<string, T> param, bool caseSensitive = false)
        {
            string expression = "";
            if (param.Value is IBinding binding)
            {
                EnsureValidBindingType(binding);

                expression = (param.Value as IValueBinding)?.GetKnockoutBindingExpression(control)
                    ?? JsonSerializer.Serialize((param.Value as IStaticValueBinding)?.Evaluate(control), DefaultSerializerSettingsProvider.Instance.Settings);
            }
            else
            {
                expression = JsonSerializer.Serialize(param.Value, DefaultSerializerSettingsProvider.Instance.Settings);
            }
            return KnockoutHelper.MakeStringLiteral(caseSensitive ? param.Key : param.Key.ToLowerInvariant()) + ": " + expression;
        }

        private static void EnsureValidBindingType(IBinding binding)
        {
            if (!(binding is IStaticValueBinding))
            {
                throw new Exception("Only value bindings are supported in <dot:RouteLink Param-xxx='' /> attributes!");
            }
        }

        private static Dictionary<string, object?> ComposeNewRouteParameters(RouteLink control, IDotvvmRequestContext context, RouteBase route)
        {
            var parameters = route.CloneDefaultValues();
            foreach (var param in context.Parameters!)
            {
                parameters[param.Key] = param.Value;
            }
            foreach (var item in control.Params.RawValues)
            {
                parameters[item.Key] = item.Value;
            }
            return parameters;
        }
    }
}
