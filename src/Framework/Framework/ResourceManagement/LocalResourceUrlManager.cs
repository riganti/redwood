using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Hosting.Middlewares;
using DotVVM.Framework.Routing;
using DotVVM.Framework.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotVVM.Framework.ResourceManagement
{
    public class LocalResourceUrlManager : ILocalResourceUrlManager
    {
        private const string HashParameterName = "hash";
        private const string NameParameterName = "name";

        private readonly IResourceHashService hasher;
        private readonly RouteBase resourceRoute;
        private readonly DotvvmResourceRepository resources;
        private readonly ConcurrentDictionary<string, string?>? alternateDirectories;
        private readonly bool suppressVersionHash;

        public LocalResourceUrlManager(DotvvmConfiguration configuration, IResourceHashService hasher)
        {
            this.resourceRoute = new DotvvmRoute(
                url: $"{HostingConstants.ResourceRouteName}/{{{HashParameterName}}}/{{{NameParameterName}:regex(.*)}}",
                virtualPath: "",
                name: $"_dotvvm_{nameof(LocalResourceUrlManager)}",
                defaultValues: null,
                presenterFactory: _ => throw new NotSupportedException(),
                configuration: configuration);
            this.hasher = hasher;
            this.resources = configuration.Resources;
            this.alternateDirectories = configuration.Debug ? new ConcurrentDictionary<string, string?>() : null;
            this.suppressVersionHash = configuration.Debug;
        }

        public string GetResourceUrl(ILocalResourceLocation resource,
            IDotvvmRequestContext context,
            string name)
        {
            return resourceRoute.BuildUrl(new Dictionary<string, object?> {
                [HashParameterName] = GetVersionHash(resource, context, name),
                [NameParameterName] = EncodeResourceName(name)
            });
        }

        protected virtual string EncodeResourceName(string name)
        {
            return name.Replace(":", "---").Replace(".", "--");
        }

        protected virtual string DecodeResourceName(string name)
        {
            return name.Replace("---", ":").Replace("--", ".");
        }

        protected virtual string GetVersionHash(ILocalResourceLocation location, IDotvvmRequestContext context, string name) =>
            suppressVersionHash ? // don't generate the hash iff !Debug, as it clears breakpoints in debugger when url changes
            EncodeResourceName(name) :
            hasher.GetVersionHash(location, context);

        public ILocalResourceLocation? FindResource(string url, IDotvvmRequestContext context, out string? mimeType)
        {
            mimeType = null;

            var routeMatchUrl = DotvvmRoutingMiddleware.GetRouteMatchUrl(context);
            if (DotvvmRoutingMiddleware.FindExactMatchRoute(new[] { resourceRoute }, routeMatchUrl, out var parameters) == null)
            {
                return null;
            }

            var name = DecodeResourceName((string)parameters![NameParameterName]!);
            var hash = (string)parameters[HashParameterName].NotNull();
            var type = context.Query.TryGetValue("type", out var x) ? x : null;
            if (resources.FindResource(name) is IResource resource &&
                FindLocation(resource, type, out mimeType) is ILocalResourceLocation location)
            {
                if (GetVersionHash(location, context, name) == hash) // check if the resource matches so that nobody can guess the url by chance
                {
                    if (alternateDirectories != null)
                    {
                        alternateDirectories.GetOrAdd(hash, _ => (location as IDebugFileLocalLocation)?.GetFilePath(context));
                    }

                    return location;
                }
            }

            return TryLoadAlternativeFile(name, hash, context);
        }

        private ILocalResourceLocation? TryLoadAlternativeFile(string name, string hash, IDotvvmRequestContext context)
        {
            if (alternateDirectories != null && alternateDirectories.TryGetValue(hash, out var filePath) && filePath != null)
            {
                var directory = Path.GetDirectoryName(Path.Combine(context.Configuration.ApplicationPhysicalPath, filePath));
                if (directory != null)
                {
                    var sourceFile = Path.Combine(directory, name);
                    if (File.Exists(sourceFile))
                    {
                        return new FileResourceLocation(sourceFile);
                    }
                }
            }
            return null;
        }

        protected ILocalResourceLocation? FindLocation(IResource resource, string? type, out string? mimeType)
        {
            if (!(resource is ILinkResource link))
            {
                mimeType = null;
                return null;
            }

            mimeType = link.MimeType;
            return link
                .GetLocations(type)
                .OfType<ILocalResourceLocation>()
                .FirstOrDefault();
        }
    }
}
