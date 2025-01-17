using System;
using System.Collections.Generic;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace DotVVM.Framework.Routing
{
    public abstract class RouteBase
    {
        /// <summary>
        /// Gets or sets a factory that provides an implementation of IDotvvmPresenter to handle the matching requests.
        /// </summary>
        public Func<IServiceProvider, IDotvvmPresenter> PresenterFactory { get; }

        /// <summary>
        /// Gets the URL pattern for the route.
        /// </summary>
        public string Url { get; private set; }

        /// <summary>
        /// Gets the URL pattern for the route, but it must not contain type parameter types (i.e. should turn a/{id:int}/{name:regex(...)} into a/{id}/{name}
        /// </summary>
        public abstract string UrlWithoutTypes { get; }

        /// <summary>
        /// Gets key of route.
        /// </summary>
        public string RouteName { get; }

        /// <summary>
        /// Gets the default values of the optional parameters.
        /// </summary>
        public IReadOnlyDictionary<string, object?> DefaultValues => _defaultValues;
        private FreezableDictionary<string, object?> _defaultValues;

        /// <summary>
        /// Gets or sets the virtual path to the view.
        /// </summary>
        public string? VirtualPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteBase"/> class.
        /// </summary>
        public RouteBase(string url, string? virtualPath, string name, object? defaultValues, Func<IServiceProvider, IDotvvmPresenter> presenterFactory)
            : this(url, virtualPath, name, new Dictionary<string, object?>(), presenterFactory)
        {
            AddOrUpdateParameterCollection(_defaultValues, defaultValues);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteBase"/> class.
        /// </summary>
        public RouteBase(string url, string? virtualPath, string name, IDictionary<string, object?>? defaultValues, Func<IServiceProvider, IDotvvmPresenter> presenterFactory)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            RouteName = name;
            Url = url;
            VirtualPath = virtualPath;
            PresenterFactory = presenterFactory;

            if (defaultValues != null)
            {
                _defaultValues = new FreezableDictionary<string, object?>(defaultValues, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _defaultValues = new FreezableDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }
        }



        /// <summary>
        /// Gets the names of the route parameters in the order in which they appear in the URL.
        /// </summary>
        public abstract IEnumerable<string> ParameterNames { get; }

        /// <summary>
        /// Gets the metadata of the route parameters.
        /// </summary>
        public abstract IEnumerable<KeyValuePair<string, DotvvmRouteParameterMetadata>> ParameterMetadata { get; }

        /// <summary>
        /// Determines whether the route matches to the specified URL and extracts the parameter values.
        /// </summary>
        public abstract bool IsMatch(string url, [MaybeNullWhen(false)] out IDictionary<string, object?> values);

        /// <summary>
        /// Builds the URL with the specified parameters.
        /// </summary>
        public string BuildUrl(IDictionary<string, object?> currentRouteValues, IDictionary<string, object?> newRouteValues)
        {
            if (currentRouteValues == null)
                throw new ArgumentNullException(nameof(currentRouteValues));
            if (newRouteValues == null)
                throw new ArgumentNullException(nameof(newRouteValues));

            var values = new Dictionary<string, object?>(_defaultValues, StringComparer.OrdinalIgnoreCase);
            AddOrUpdateParameterCollection(values, currentRouteValues);
            AddOrUpdateParameterCollection(values, newRouteValues);

            return BuildUrlCore(values);
        }

        /// <summary>
        /// Builds the URL.
        /// </summary>
        public string BuildUrl(IDictionary<string, object?> currentRouteValues, object? newRouteValues)
        {
            if (currentRouteValues == null)
                throw new ArgumentNullException(nameof(currentRouteValues));

            var values = new Dictionary<string, object?>(_defaultValues, StringComparer.OrdinalIgnoreCase);
            AddOrUpdateParameterCollection(values, currentRouteValues);
            AddOrUpdateParameterCollection(values, newRouteValues);

            return BuildUrl(values);
        }

        /// <summary>
        /// Builds the URL.
        /// </summary>
        public string BuildUrl(object? routeValues = null)
        {
            var values = new Dictionary<string, object?>(_defaultValues, StringComparer.OrdinalIgnoreCase);
            AddOrUpdateParameterCollection(values, routeValues);

            return BuildUrl(values);
        }

        /// <summary>
        /// Builds the URL with the specified parameters.
        /// </summary>
        public string BuildUrl(IDictionary<string, object?> routeValues)
        {
            if (routeValues == null)
            {
                routeValues = new Dictionary<string, object?>();
            }

            var values = new Dictionary<string, object?>(_defaultValues, StringComparer.OrdinalIgnoreCase);
            AddOrUpdateParameterCollection(values, routeValues);

            return BuildUrlCore(values);
        }

        /// <summary>
        /// Builds the URL core from the parameters.
        /// </summary>
        /// <remarks>The default values are already included in the <paramref name="values"/> collection.</remarks>
        protected internal abstract string BuildUrlCore(Dictionary<string, object?> values);

        /// <summary>
        /// Adds or updates the parameter collection with the specified values from the anonymous object.
        /// </summary>
        public static void AddOrUpdateParameterCollection(IDictionary<string, object?> targetCollection, object? anonymousObject)
        {
            if (anonymousObject is IEnumerable<KeyValuePair<string, string?>> stringPairs)
            {
                foreach (var item in stringPairs)
                {
                    targetCollection[item.Key] = item.Value;
                }
            }
            else if (anonymousObject is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var item in pairs)
                {
                    targetCollection[item.Key] = item.Value;
                }
            }
            else if (anonymousObject != null)
            {
                foreach (var prop in anonymousObject.GetType().GetProperties())
                {
                    targetCollection[prop.Name] = prop.GetValue(anonymousObject);
                }
            }
        }

        /// <summary>
        /// Adds or updates the parameter collection with the specified values from the other parameter collection.
        /// </summary>
        public static void AddOrUpdateParameterCollection(IDictionary<string, object?> targetCollection, IDictionary<string, object?> newValues)
        {
            foreach (var pair in newValues)
            {
                targetCollection[pair.Key] = pair.Value;
            }
        }

        /// <summary>
        /// Returns a presenter that processes the request.
        /// </summary>
        public virtual IDotvvmPresenter GetPresenter(IServiceProvider provider) => PresenterFactory(provider);

        public Dictionary<string, object?> CloneDefaultValues() => new Dictionary<string, object?>(_defaultValues, StringComparer.OrdinalIgnoreCase);


        private bool isFrozen = false;

        private void ThrowIfFrozen()
        {
            if (isFrozen)
                throw FreezableUtils.Error(this.GetType().Name);
        }
        public void Freeze()
        {
            this.isFrozen = true;
            this._defaultValues.Freeze();

            Freeze2();
        }

        // Freeze must not be virtual since it would allow someone to suppress the freezing (probably accidentally).

        /// <summary> This method should freeze the contents of a derived class from <see cref="RouteBase" />. Make sure that the implementation is sealed, so that the derived class cannot suppress the freezing process. If you want to allow inheritance from you class, create an abstract Freeze3 method for that. </summary>
        protected abstract void Freeze2();
    }
}
