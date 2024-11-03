using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotVVM.Framework.Configuration;

namespace DotVVM.Framework.Hosting
{
    public class DefaultMarkupFileLoader : IMarkupFileLoader
    {
        /// <summary>
        /// Gets the markup file for the specified virtual path.
        /// </summary>
        public MarkupFile? GetMarkup(DotvvmConfiguration configuration, string virtualPath)
        {
            // check that we are not outside application directory
            var fullPath = Path.Combine(configuration.ApplicationPhysicalPath, virtualPath);
            try
            {
                fullPath = Path.GetFullPath(fullPath);
            }
            catch(NotSupportedException)
            {
                return null;
            }

            if (File.Exists(fullPath))
            {
                // load the file
                return new MarkupFile(virtualPath, fullPath);
            }

            return null;
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
