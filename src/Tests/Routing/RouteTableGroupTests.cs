using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Routing;
using DotVVM.Framework.Tests.Routing;
using DotVVM.Framework.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotVVM.Framework.Tests.Routing
{
    [TestClass]
    public class RouteTableGroupTests
    {
        DotvvmConfiguration configuration = DotvvmTestHelper.DefaultConfig;

        [TestMethod]
        public void RouteTableGroup_UrlWithParameters()
        {
            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", "UrlPrefix/{Id}", "PathPrefix", opt => {
                opt.Add("Route", "Article/{Title}", "route.dothtml", null, null);
            });

            var group = table.GetGroup("Group");
            var route = group.First();
            Assert.AreEqual("Group_Route", route.RouteName);
            Assert.AreEqual("PathPrefix/route.dothtml", route.VirtualPath);
            Assert.IsTrue(route.IsMatch("UrlPrefix/5/Article/test", out var parameters));

            Assert.AreEqual("5", parameters["Id"]);
            Assert.AreEqual("test", parameters["Title"]);
        }

        [TestMethod]
        public void RouteTableGroup_EmptyRouteName()
        {
            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", "UrlPrefix/{Id}", null, opt => {
                opt.Add("Default", "", "route.dothtml", null, null, null);
            });

            var group = table.GetGroup("Group");
            var route = group.First();
            Assert.AreEqual(route.RouteName, "Group_Default");
            Assert.IsTrue(route.IsMatch("UrlPrefix/5", out var parameters));

            Assert.AreEqual("5", parameters["Id"]);
        }

        [TestMethod]
        public void RouteTableGroup_DefaultValues()
        {
            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", "UrlPrefix/{Id}", null, opt => {
                opt.Add("Route", "Article/{Title}", "route.dothtml", new { Title = "test" }, null, null);
            });

            var group = table.GetGroup("Group");
            var route = group.First();
            Assert.AreEqual(route.RouteName, "Group_Route");
            Assert.IsTrue(route.IsMatch("UrlPrefix/5/Article", out var parameters));

            Assert.AreEqual("5", parameters["Id"]);
            Assert.AreEqual("test", parameters["Title"]);
        }


        [TestMethod]
        public void RouteTableGroup_MultipleRoutes()
        {
            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", "UrlPrefix/{Id}", null, opt => {
                opt.Add("Route0", "Article0/{Title}", "route.dothtml", null, null, null);
                opt.Add("Route1", "Article1/{Title}", "route.dothtml", null, null, null);
            });

            var group = table.GetGroup("Group");
            Assert.IsTrue(group.Contains("Group_Route0"));
            Assert.IsTrue(group.Contains("Group_Route1"));
            Assert.IsTrue(group["Group_Route0"].IsMatch("UrlPrefix/0/Article0/test0", out _));
            Assert.IsTrue(group["Group_Route1"].IsMatch("UrlPrefix/1/Article1/test1", out _));
        }

        [TestMethod]
        public void RouteTableGroup_MultipleRoutesWithParameters()
        {
            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", "UrlPrefix/{Id}", null, opt => {
                opt.Add("Route0", "Article0/{Title}", "route.dothtml", null, null, null);
                opt.Add("Route1", "Article1/{Title}", "route.dothtml", null, null, null);
            });

            var group = table.GetGroup("Group");
            Assert.IsTrue(group["Group_Route0"].IsMatch("UrlPrefix/0/Article0/test0", out var parameters));
            Assert.AreEqual("0", parameters["Id"]);
            Assert.AreEqual("test0", parameters["Title"]);
            Assert.IsTrue(group["Group_Route1"].IsMatch("UrlPrefix/1/Article1/test1", out parameters));
            Assert.AreEqual("1", parameters["Id"]);
            Assert.AreEqual("test1", parameters["Title"]);
        }

        [TestMethod]
        public void RouteTableGroup_NestedGroups()
        {
            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group1", "UrlPrefix1", null, opt1 => {
                opt1.AddGroup("Group2", "UrlPrefix2", null, opt2 => {
                    opt2.AddGroup("Group3", "UrlPrefix3", null, opt3 => {
                        opt3.Add("Route3", "Article3", "route.dothtml", null, null, null);
                    });
                    opt2.Add("Route2", "Article2", "route.dothtml", null, null, null);
                });
                opt1.Add("Route1", "Article1", "route.dothtml", null, null, null);
            });

            var group = table.GetGroup("Group1");
            var nestedGroup = table.GetGroup("Group1").GetGroup("Group2");
            var nestedGroup2 = table.GetGroup("Group1").GetGroup("Group2").GetGroup("Group3");
            var route3 = "Group1_Group2_Group3_Route3";
            var route2 = "Group1_Group2_Route2";
            var route1 = "Group1_Route1";

            Assert.IsTrue(group.Contains(route3));
            Assert.IsTrue(nestedGroup.Contains(route3));
            Assert.IsTrue(nestedGroup2.Contains(route3));
            Assert.IsTrue(group.Contains(route2));
            Assert.IsTrue(nestedGroup.Contains(route2));
            Assert.IsFalse(nestedGroup2.Contains(route2));
            Assert.IsTrue(group.Contains(route1));
            Assert.IsFalse(nestedGroup.Contains(route1));
            Assert.IsFalse(nestedGroup2.Contains(route1));


            Assert.IsTrue(group[route3].IsMatch("UrlPrefix1/UrlPrefix2/UrlPrefix3/Article3", out _));
            Assert.IsTrue(group[route2].IsMatch("UrlPrefix1/UrlPrefix2/Article2", out _));
            Assert.IsTrue(group[route1].IsMatch("UrlPrefix1/Article1", out _));
        }

        [TestMethod]
        public void RouteTableGroup_PresenterType()
        {
            var configuration = DotvvmConfiguration.CreateDefault(services => {
                services.TryAddScoped<TestPresenter>();
            });

            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", null, null, opt => {
                opt.Add("Article", "", typeof(TestPresenter), null);
            });
            Assert.IsInstanceOfType(table.First().GetPresenter(configuration.ServiceProvider), typeof(TestPresenter));

            Assert.ThrowsException<ArgumentException>(() => {
                table.Add("Blog", "", typeof(TestPresenterWithoutInterface));
            });
        }

        [TestMethod]
        public void RouteTableGroup_PresenterFactoryMethod()
        {
            var configuration = DotvvmConfiguration.CreateDefault(services => {
                services.TryAddScoped<TestPresenter>();
            });

            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", null, null, opt => {
                opt.Add("Article", "", provider => provider.GetRequiredService<TestPresenter>(), null);
            });
            Assert.IsInstanceOfType(table.First().GetPresenter(configuration.ServiceProvider), typeof(TestPresenter));
        }

        [TestMethod]
        public void RouteTableGroup_DefaultPresenterFactory()
        {
            var configuration = DotvvmConfiguration.CreateDefault(services => {
                services.TryAddScoped<TestPresenter>();
            });

            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", null, null, opt => {
                opt.Add("Article", "", "");
            }, p => p.GetRequiredService<TestPresenter>());
            Assert.IsInstanceOfType(table.First().GetPresenter(configuration.ServiceProvider), typeof(TestPresenter));
        }

        [TestMethod]
        public void RouteTableGroup_Redirections()
        {
            var table = new DotvvmRouteTable(configuration);
            table.AddGroup("Group", "Prefix", "VirtualPathPrefix", opt => {
                opt.AddUrlRedirection("Url", "", "redirect.dothtml");
                opt.AddRouteRedirection("Route", "RedirectRoute", "Group_Url");
                opt.Add("Normal", "Normal", "normal.dothtml");
            });

            var group = table.GetGroup("Group");

            var urlRedirection = group.ElementAt(0);
            Assert.AreEqual("Group_Url", urlRedirection.RouteName);
            Assert.AreEqual("Prefix", urlRedirection.Url);
            Assert.IsNull(urlRedirection.VirtualPath);

            var routeRedirection = group.ElementAt(1);
            Assert.AreEqual("Group_Route", routeRedirection.RouteName);
            Assert.AreEqual("Prefix/RedirectRoute", routeRedirection.Url);
            Assert.IsNull(routeRedirection.VirtualPath);

            var normalRoute = group.ElementAt(2);
            Assert.AreEqual("Group_Normal", normalRoute.RouteName);
            Assert.AreEqual("Prefix/Normal", normalRoute.Url);
            Assert.AreEqual("VirtualPathPrefix/normal.dothtml", normalRoute.VirtualPath);
        }
    }
}
