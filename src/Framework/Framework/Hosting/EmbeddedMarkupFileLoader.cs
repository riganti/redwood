using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DotVVM.Framework.Configuration;

namespace DotVVM.Framework.Hosting
{
    public class EmbeddedMarkupFileLoader : IMarkupFileLoader
    {
        /// <summary>
        /// Gets the markup file for the specified virtual path.
        /// </summary>
        public MarkupFile? GetMarkup(DotvvmConfiguration configuration, string virtualPath)
        {
            if (!virtualPath.StartsWith("embedded://", StringComparison.Ordinal))
            {
                return null;
            }

            string resourceName = virtualPath.Remove(0, "embedded://".Length);

            if (resourceName.IndexOf('/') == -1 || resourceName.IndexOf('/') == 0)
            {
                throw new ArgumentException("Wrong string format", "virtualPath");
            }

            string assemblyName = resourceName.Substring(0, resourceName.IndexOf('/'));

            Assembly? assembly = null;
            try
            {
                assembly = Assembly.Load(new AssemblyName(assemblyName));
            }

            //no such assembly found
            catch (FileLoadException)
            {
                throw new ArgumentException("No such assembly was found", "virtualPath");
            }

            //no such resource found
            resourceName = resourceName.Replace('/', '.');
            if (assembly.GetManifestResourceInfo(resourceName) == null)
            {
                throw new ArgumentException("No such resource was found", "virtualPath");
            }

            //load the file
            using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
            using (StreamReader sr = new StreamReader(stream))
                return new MarkupFile(virtualPath, virtualPath, sr.ReadToEnd());
        }

        /// <summary>
        /// Gets the markup file virtual path from the current request URL.
        /// </summary>
        public string GetMarkupFileVirtualPath(IDotvvmRequestContext context)
        {
            return context.Route!.VirtualPath
                ?? throw new Exception($"The route {context.Route.RouteName} must have a non-null virtual path.");
        }
    }
}

