using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Routing;
using DotVVM.Framework.ViewModel;

namespace DotVVM.Samples.BasicSamples.ViewModels
{
    public class DefaultViewModel : SamplesViewModel
    {
        public string Title { get; set; }

        [Bind(Direction.None)]
        public List<RouteData> Routes { get; set; }

        public override Task Init()
        {
            Routes = Context.Configuration.RouteTable
                .Select(r => new RouteData()
                {
                    RouteName = r.RouteName,
                    Url = Context.TranslateVirtualPath(r.BuildUrl())
                })
                .ToList();

            return base.Init();
        }

        public class RouteData
        {
            public string Url { get; set; }

            public string RouteName { get; set; }
        }
    }
}
