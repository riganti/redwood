using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Hosting
{
    /// <summary> Read markup files from embedded resources, if the virtual path has the following format: <c>embedded://{AssemblyName}/{ResourceName}</c></summary>
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
                throw new ArgumentException("Wrong embedded file format. Use `embedded://{AssemblyName}/{ResourceName}`", "virtualPath");
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
                throw new ArgumentException($"Assembly '{assemblyName}' was not found", "virtualPath");
            }

            //no such resource found
            resourceName = resourceName.Replace('/', '.');
            if (assembly.GetManifestResourceInfo(resourceName) == null)
            {
                throw new ArgumentException($"Resource '{resourceName}' was not found in assembly '{assembly.FullName}'", "virtualPath");
            }

            return new MarkupFile(virtualPath, virtualPath, () => {
                //load the file
                using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            });
        }

        /// <summary>
        /// Gets the markup file virtual path from the current request URL.
        /// </summary>
        public string GetMarkupFileVirtualPath(IDotvvmRequestContext context)
        {
            return context.Route!.VirtualPath;
        }
    }
}

