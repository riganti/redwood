using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using RecordExceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotVVM.Framework.Routing
{
    /// <summary>
    /// A default route discover strategy which browses the Views folder and automatically creates routes for all *.dothtml files.
    /// </summary>
    public class DefaultRouteStrategy : IRoutingStrategy
    {
        public record DirectoryNotFoundException(string Directory): RecordException
        {
            public override string Message => $"Cannot auto-discover DotVVM routes. The directory '{Directory}' was not found!";
        }

        protected readonly DotvvmConfiguration configuration;

        protected readonly string pattern;

        protected readonly string applicationPhysicalPath;
        protected readonly DirectoryInfo viewsFolderDirectoryInfo;


        public DefaultRouteStrategy(DotvvmConfiguration configuration, string viewsFolder = "Views", string pattern = "*.dothtml")
        {
            this.configuration = configuration;
            this.pattern = pattern;
            this.applicationPhysicalPath = Path.GetFullPath(configuration.ApplicationPhysicalPath);

            var directory = Path.Combine(applicationPhysicalPath, viewsFolder);
            viewsFolderDirectoryInfo = new DirectoryInfo(directory);
        }


        public virtual IEnumerable<RouteBase> GetRoutes()
        {
            var existingRouteNames = new HashSet<string>(configuration.RouteTable.Select(r => r.RouteName), StringComparer.OrdinalIgnoreCase);

            return DiscoverMarkupFiles()
                .SelectMany(BuildRoutes)
                .Where(r => !existingRouteNames.Contains(r.RouteName));
        }

        /// <summary>
        /// Discovers all markup files for which the route should be created.
        /// </summary>
        protected virtual IEnumerable<RouteStrategyMarkupFileInfo> DiscoverMarkupFiles()
        {
            if (!viewsFolderDirectoryInfo.Exists)
            {
                throw new DirectoryNotFoundException(viewsFolderDirectoryInfo.FullName);
            }

            return viewsFolderDirectoryInfo
                .GetFiles(pattern, SearchOption.AllDirectories)
                .Select(file => new RouteStrategyMarkupFileInfo()
                {
                    AbsolutePath = file.FullName,
                    // Get relative path from applicationPhysicalPath to markup file
                    AppRelativePath = GetRelativePathBetween(applicationPhysicalPath, file.FullName),
                    // Get relative path from viewsFolderDirectory to markup file
                    ViewsFolderRelativePath = GetRelativePathBetween(viewsFolderDirectoryInfo.FullName, file.FullName).TrimStart('/')
                });
        }

        internal static string GetRelativePathBetween(string from, string to)
        {
            var fromPath = from.Replace('\\', '/');
            var toPath = to.Replace('\\', '/');
            var fromPathSegments = fromPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var toPathSegments = toPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Find index of the first path segments where they differ
            var index = 0;
            for (index = 0; index < Math.Min(fromPathSegments.Length, toPathSegments.Length); index++)
            {
                if (fromPathSegments[index] == toPathSegments[index])
                    continue;

                break;
            }

            // Construct path
            var goBack = Enumerable.Repeat("..", fromPathSegments.Length - index) ?? Enumerable.Empty<string>();
            var goForward = Enumerable.Skip(toPathSegments, index) ?? Enumerable.Empty<string>();
            var resultPathSegments = goBack.Concat(goForward).ToArray();
            return Path.Combine(resultPathSegments).Replace('\\', '/');
        }

        /// <summary>
        /// Builds a set of routes for the specified markup file.
        /// </summary>
        protected virtual IEnumerable<RouteBase> BuildRoutes(RouteStrategyMarkupFileInfo file) => new [] { this.BuildRoute(file) };

        /// <summary>
        /// Builds a route for the specified markup file.
        /// </summary>
        protected virtual RouteBase BuildRoute(RouteStrategyMarkupFileInfo file)
        {
            var routeName = GetRouteName(file);
            var url = GetRouteUrl(file);
            var defaultParameters = GetRouteDefaultParameters(file);
            var presenterFactory = GetRoutePresenterFactory(file);

            return new DotvvmRoute(url, file.AppRelativePath, routeName, defaultParameters, presenterFactory, configuration);
        }

        protected virtual string GetRouteName(RouteStrategyMarkupFileInfo file)
        {
            return GetRouteUrl(file).Replace('/', '_');
        }

        protected virtual string GetRouteUrl(RouteStrategyMarkupFileInfo file)
        {
            var pathWithoutExtension = file.ViewsFolderRelativePath.Substring(0, file.ViewsFolderRelativePath.Length - ".dothtml".Length);
            return pathWithoutExtension;
        }

        protected virtual object? GetRouteDefaultParameters(RouteStrategyMarkupFileInfo file)
        {
            return null;
        }

        protected virtual Func<IServiceProvider, IDotvvmPresenter> GetRoutePresenterFactory(RouteStrategyMarkupFileInfo file)
        {
            return configuration.RouteTable.GetDefaultPresenter;
        }
    }

    public class CustomListRoutingStrategy: DefaultRouteStrategy
    {
        private readonly Func<string, IEnumerable<string>> getRouteList;
        public CustomListRoutingStrategy(DotvvmConfiguration configuration, Func<string, IEnumerable<string>>? getRouteList = null, string viewsFolder = "Views", string pattern = "*.dothtml") : base(configuration, viewsFolder, pattern)
        {
            this.getRouteList = getRouteList ?? GetRoutesForFile;
        }

        private static HashSet<string> defaultFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Index", "Default" };

        private static IEnumerable<string> GetRoutesForFile(string fileName)
        {
            var slashIndex = fileName.LastIndexOf('/');
            var pureName = Path.GetFileNameWithoutExtension(slashIndex < 0 ? fileName : fileName.Substring(slashIndex + 1));
            var directory = slashIndex < 0 ? "" : fileName.Remove(slashIndex);

            if (!char.IsLetterOrDigit(pureName[0])) yield break;

            if (defaultFiles.Contains(pureName)) yield return directory;

            if (!string.IsNullOrEmpty(directory)) yield return directory + "/" + pureName;
            else yield return pureName;
        }

        protected override IEnumerable<RouteBase> BuildRoutes(RouteStrategyMarkupFileInfo file)
        {
            return getRouteList(file.AppRelativePath)
                   .Select(url => new DotvvmRoute(url, file.AppRelativePath, url, GetRouteDefaultParameters(file), GetRoutePresenterFactory(file), this.configuration));
        }
    }
}
