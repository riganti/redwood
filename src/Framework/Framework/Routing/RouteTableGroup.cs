using System;
using System.Collections.Generic;
using System.Text;
using DotVVM.Framework.Hosting;

namespace DotVVM.Framework.Routing
{
    public class RouteTableGroup
    {
        public Action<RouteBase> AddToParentRouteTable { get; private set; }

        public Func<IServiceProvider, IDotvvmPresenter>? PresenterFactory { get; }

        public string GroupName { get; private set; }
        public string RouteNamePrefix { get; private set; }
        public string UrlPrefix { get; private set; }
        public string VirtualPathPrefix { get; private set; }

        public RouteTableGroup(string groupName, string routeNamePrefix, string urlPrefix, string virtualPathPrefix, Action<RouteBase> addToParentRouteTable, Func<IServiceProvider, IDotvvmPresenter>? presenterFactory)
        {
            GroupName = groupName;
            RouteNamePrefix = routeNamePrefix;
            UrlPrefix = urlPrefix;
            VirtualPathPrefix = virtualPathPrefix;
            AddToParentRouteTable = addToParentRouteTable;
            PresenterFactory = presenterFactory;
        }
    }
}
