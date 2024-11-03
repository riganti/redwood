using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.Framework.Routing
{
    /// <summary>
    /// Represents the table of routes.
    /// </summary>
    public sealed class DotvvmRouteTable : IEnumerable<RouteBase>
    {
        private readonly DotvvmConfiguration configuration;
        private readonly List<KeyValuePair<string, RouteBase>> list = new();
        private readonly List<IPartialMatchRouteHandler> partialMatchHandlers = new();
        private readonly List<IPartialMatchRoute> partialMatchRoutes = new();

        private readonly Dictionary<string, RouteBase> dictionary
            = new Dictionary<string, RouteBase>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, DotvvmRouteTable> routeTableGroups
            = new Dictionary<string, DotvvmRouteTable>();

        private RouteTableGroup? group = null;

        public IReadOnlyList<IPartialMatchRouteHandler> PartialMatchHandlers => partialMatchHandlers;

        internal IEnumerable<IPartialMatchRoute> PartialMatchRoutes => partialMatchRoutes;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmRouteTable"/> class.
        /// </summary>
        public DotvvmRouteTable(DotvvmConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Returns RouteTable of specific group name.
        /// </summary>
        /// <param name="groupName">Name of the group</param>
        /// <returns></returns>
        public DotvvmRouteTable GetGroup(string groupName)
        {
            if (groupName == null)
                throw new ArgumentNullException("Name of the group cannot be null!");
            var group = routeTableGroups[groupName];
            return group;
        }

        /// <summary>
        /// Adds a group of routes
        /// </summary>
        /// <param name="groupName">Name of the group</param>
        /// <param name="urlPrefix">Url prefix of added routes</param>
        /// <param name="virtualPathPrefix">Virtual path prefix of added routes</param>
        /// <param name="content">Contains routes to be added</param>
        /// <param name="presenterFactory">Default presenter factory common to all routes in the group</param>
        public void AddGroup(string groupName,
            string urlPrefix,
            string virtualPathPrefix,
            Action<DotvvmRouteTable> content,
            Func<IServiceProvider, IDotvvmPresenter>? presenterFactory = null)
        {
            ThrowIfFrozen();
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException("Name of the group cannot be null or empty!");
            }
            if (routeTableGroups.ContainsKey(groupName))
            {
                throw new InvalidOperationException($"The group with name '{groupName}' has already been registered!");
            }
            urlPrefix = CombinePath(group?.UrlPrefix, urlPrefix);
            virtualPathPrefix = CombinePath(group?.VirtualPathPrefix, virtualPathPrefix);

            var newGroup = new DotvvmRouteTable(configuration);
            newGroup.group = new RouteTableGroup(
                groupName,
                routeNamePrefix: group?.RouteNamePrefix + groupName + "_",
                urlPrefix,
                virtualPathPrefix,
                addToParentRouteTable: Add,
                presenterFactory);

            content(newGroup);
            routeTableGroups.Add(groupName, newGroup);
        }

        /// <summary>
        /// Creates the default presenter factory.
        /// </summary>
        public IDotvvmPresenter GetDefaultPresenter(IServiceProvider provider)
        {
            if (group != null && group.PresenterFactory != null)
            {
                var presenter = group.PresenterFactory(provider);
                if (presenter == null)
                {
                    throw new InvalidOperationException("The presenter factory of a route " +
                        "group must not return null.");
                }
                return presenter;
            }
            return provider.GetRequiredService<IDotvvmPresenter>();
        }

        /// <summary>
        /// Adds the specified route name.
        /// </summary>
        /// <param name="routeName">Name of the route.</param>
        /// <param name="url">The URL.</param>
        /// <param name="virtualPath">The virtual path of the Dothtml file.</param>
        /// <param name="defaultValues">The default values.</param>
        /// <param name="presenterFactory">Delegate creating the presenter handling this route</param>
        public void Add(string routeName, string url, string? virtualPath, object? defaultValues = null, Func<IServiceProvider, IDotvvmPresenter>? presenterFactory = null, LocalizedRouteUrl[]? localizedUrls = null)
        {
            AddCore(routeName, url, virtualPath, defaultValues, presenterFactory, localizedUrls);
        }

        /// <summary>
        /// Adds the specified route name.
        /// </summary>
        /// <param name="routeName">Name of the route.</param>
        /// <param name="url">The URL.</param>
        /// <param name="defaultValues">The default values.</param>
        /// <param name="presenterFactory">The presenter factory.</param>
        public void Add(string routeName, string url, Func<IServiceProvider, IDotvvmPresenter> presenterFactory, object? defaultValues = null, LocalizedRouteUrl[]? localizedUrls = null)
        {
            AddCore(routeName, url, virtualPath: null, defaultValues, presenterFactory, localizedUrls);
        }

        /// <summary>
        /// Adds the specified route name.
        /// </summary>
        /// <param name="routeName">Name of the route.</param>
        /// <param name="url">The URL.</param>
        /// <param name="presenterType">The presenter factory.</param>
        /// <param name="defaultValues">The default values.</param>
        public void Add(string routeName, string url, Type presenterType, object? defaultValues = null, LocalizedRouteUrl[]? localizedUrls = null)
        {
            if (!typeof(IDotvvmPresenter).IsAssignableFrom(presenterType))
            {
                throw new ArgumentException($@"{nameof(presenterType)} has to inherit from DotVVM.Framework.Hosting.IDotvvmPresenter.", nameof(presenterType));
            }
            Func<IServiceProvider, IDotvvmPresenter> presenterFactory = provider => (IDotvvmPresenter)provider.GetRequiredService(presenterType);
            AddCore(routeName, url, virtualPath: null, defaultValues, presenterFactory, localizedUrls);
        }

        /// <summary>
        /// Adds the specified name.
        /// </summary>
        public void Add(RouteBase route)
        {
            ThrowIfFrozen();
            if (dictionary.ContainsKey(route.RouteName))
            {
                throw new InvalidOperationException($"The route with name '{route.RouteName}' has already been registered!");
            }

            group?.AddToParentRouteTable?.Invoke(route);

            // The list is used for finding the routes because it keeps the ordering, the dictionary is for checking duplicates
            list.Add(new KeyValuePair<string, RouteBase>(route.RouteName, route));
            dictionary.Add(route.RouteName, route);

            if (route is IPartialMatchRoute partialMatchRoute)
            {
                partialMatchRoutes.Add(partialMatchRoute);
            }
        }

        private void AddCore(string routeName, string url, string? virtualPath, object? defaultValues, Func<IServiceProvider, IDotvvmPresenter>? presenterFactory, LocalizedRouteUrl[]? localizedUrls = null)
        {
            ThrowIfFrozen();

            if (url == null)
                throw new ArgumentNullException(nameof(url));
            url = CombinePath(group?.UrlPrefix, url);

            virtualPath = CombinePath(group?.VirtualPathPrefix, virtualPath);
            if (virtualPath == null && presenterFactory == null)
            {
                throw new ArgumentNullException(nameof(presenterFactory), "The presenterFactory argument must be set when virtualPath is null!");
            }

            presenterFactory ??= GetDefaultPresenter;
            routeName = group?.RouteNamePrefix + routeName;

            RouteBase route = localizedUrls == null
                ? new DotvvmRoute(url, virtualPath, routeName, defaultValues, presenterFactory, configuration)
                : new LocalizedDotvvmRoute(url,
                    localizedUrls.Select(l => new LocalizedRouteUrl(l.CultureIdentifier, CombinePath(group?.UrlPrefix, l.RouteUrl))).ToArray(),
                    virtualPath, routeName, defaultValues, presenterFactory, configuration);
            Add(route);
        }

        /// <summary>
        /// Adds the specified URL redirection entry.
        /// </summary>
        /// <param name="routeName">Name of the redirection.</param>
        /// <param name="urlPattern">URL pattern to redirect from.</param>
        /// <param name="targetUrl">URL which will be used as a target for redirection.</param>
        public void AddUrlRedirection(string routeName, string urlPattern, string targetUrl, object? defaultValues = null, bool permanent = false)
            => AddUrlRedirection(routeName, urlPattern, _ => targetUrl, defaultValues, permanent);

        /// <summary>
        /// Adds the specified URL redirection entry.
        /// </summary>
        /// <param name="routeName">Name of the redirection.</param>
        /// <param name="urlPattern">URL pattern to redirect from.</param>
        /// <param name="targetUrlProvider">URL provider to obtain context-based redirection targets.</param>
        public void AddUrlRedirection(string routeName, string urlPattern, Func<IDotvvmRequestContext, string> targetUrlProvider, object? defaultValues = null, bool permanent = false)
        {
            IDotvvmPresenter presenterFactory(IServiceProvider serviceProvider) => new DelegatePresenter(context =>
            {
                var targetUrl = targetUrlProvider(context);

                if (permanent)
                    context.RedirectToUrlPermanent(targetUrl);
                else
                    context.RedirectToUrl(targetUrl);
            });
            AddCore(routeName, urlPattern, virtualPath: null, defaultValues, presenterFactory);
        }

        /// <summary>
        /// Adds the specified route redirection entry.
        /// </summary>
        /// <param name="routeName">Name of the redirection.</param>
        /// <param name="urlPattern">URL pattern to redirect from.</param>
        /// <param name="targetRouteName">Route name which will be used as a target for redirection.</param>
        /// <param name="urlSuffixProvider">Provider to obtain context-based URL suffix.</param>
        public void AddRouteRedirection(string routeName, string urlPattern, string targetRouteName,
            object? defaultValues = null, bool permanent = false, Func<IDotvvmRequestContext, Dictionary<string, object?>>? parametersProvider = null,
            Func<IDotvvmRequestContext, string>? urlSuffixProvider = null)
            => AddRouteRedirection(routeName, urlPattern, _ => targetRouteName, defaultValues, permanent, parametersProvider, urlSuffixProvider);

        /// <summary>
        /// Adds the specified route redirection entry.
        /// </summary>
        /// <param name="routeName">Name of the redirection.</param>
        /// <param name="urlPattern">URL pattern to redirect from.</param>
        /// <param name="targetRouteNameProvider">Route name provider to obtain context-based redirection targets.</param>
        /// <param name="urlSuffixProvider">Provider to obtain context-based URL suffix.</param>
        public void AddRouteRedirection(string routeName, string urlPattern, Func<IDotvvmRequestContext, string> targetRouteNameProvider,
            object? defaultValues = null, bool permanent = false, Func<IDotvvmRequestContext, Dictionary<string, object?>>? parametersProvider = null,
            Func<IDotvvmRequestContext, string>? urlSuffixProvider = null)
        {
            IDotvvmPresenter presenterFactory(IServiceProvider serviceProvider) => new DelegatePresenter(context =>
            {
                var targetRouteName = targetRouteNameProvider(context);
                var newParameterValues = parametersProvider?.Invoke(context);
                var urlSuffix = urlSuffixProvider != null ? urlSuffixProvider(context) : context.HttpContext.Request.Url.Query;

                if (permanent)
                    context.RedirectToRoutePermanent(targetRouteName, newParameterValues, urlSuffix: urlSuffix);
                else
                    context.RedirectToRoute(targetRouteName, newParameterValues, urlSuffix: urlSuffix);
            });
            AddCore(routeName, urlPattern, virtualPath: null, defaultValues, presenterFactory);
        }

        public void AddPartialMatchHandler(IPartialMatchRouteHandler handler)
        {
            ThrowIfFrozen();
            partialMatchHandlers.Add(handler);
        }

        public bool Contains(string routeName)
        {
            return dictionary.ContainsKey(routeName);
        }

        public bool TryGetValue(string routeName, [MaybeNullWhen(false)] out RouteBase route)
        {
            return dictionary.TryGetValue(routeName, out route);
        }

        public RouteBase this[string routeName]
        {
            get
            {
                if (!dictionary.TryGetValue(routeName, out var route))
                {
                    throw new ArgumentException($"The route with name '{routeName}' does not exist!");
                }
                return route;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        public IEnumerator<RouteBase> GetEnumerator()
        {
            return list.Select(l => l.Value).GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        [return: NotNullIfNotNull(nameof(appendedPath))]
        private string? CombinePath(string? prefix, string? appendedPath)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return appendedPath;
            }

            if (appendedPath == null)
            {
                return null;
            }
            else if (appendedPath == string.Empty)
            {
                return prefix ?? string.Empty;
            }

            return $"{prefix}/{appendedPath}";
        }

        private bool isFrozen = false;

        private void ThrowIfFrozen()
        {
            if (isFrozen)
                throw FreezableUtils.Error(nameof(DotvvmRouteTable));
        }
        public void Freeze()
        {
            this.isFrozen = true;
        }
    }
}
