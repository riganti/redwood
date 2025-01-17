using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Routing;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Testing;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Globalization;
using System.Threading;
using DotVVM.Framework.Tests.Binding;

namespace DotVVM.Framework.Tests.Routing
{
    [TestClass]
    public class DotvvmRouteTests
    {
        DotvvmConfiguration configuration = DotvvmTestHelper.DefaultConfig;

        [TestMethod]
        public void DotvvmRoute_IsMatch_RouteMustNotStartWithSlash()
        {
            Assert.ThrowsException<ArgumentException>(() => {
                var route = new DotvvmRoute("/Test", null, "testpage", null, null, configuration);
            });
        }


        [TestMethod]
        public void DotvvmRoute_IsMatch_RouteMustNotEndWithSlash()
        {
            Assert.ThrowsException<ArgumentException>(() => {
                var route = new DotvvmRoute("Test/", null, "testpage", null, null, configuration);
            });
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_EmptyRouteMatchesEmptyUrl()
        {
            var route = new DotvvmRoute("", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("", out parameters);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_UrlWithoutParametersExactMatch()
        {
            var route = new DotvvmRoute("Hello/Test/Page.txt", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Hello/Test/Page.txt", out parameters);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_UrlWithoutParametersNoMatch()
        {
            var route = new DotvvmRoute("Hello/Test/Page.txt", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Hello/Test/Page", out parameters);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_UrlTwoParametersBothSpecified()
        {
            var route = new DotvvmRoute("Article/{Id}/{Title}", null, "testpage", new { Title = "test" }, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/15/Test-title", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual("15", parameters["Id"]);
            Assert.AreEqual("Test-title", parameters["Title"]);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_UrlTwoParametersOneSpecifiedOneDefault()
        {
            var route = new DotvvmRoute("Article/{Id}/{Title}", null, "testpage", new { Title = "test" }, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/15", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual("15", parameters["Id"]);
            Assert.AreEqual("test", parameters["Title"]);
        }


        [TestMethod]
        public void DotvvmRoute_IsMatch_UrlTwoParametersBothRequired_NoMatchWhenOneSpecified()
        {
            var route = new DotvvmRoute("Article/{Id}/{Title}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/15", out parameters);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_UrlTwoParametersBothRequired_DifferentPart()
        {
            var route = new DotvvmRoute("Article/id_{Id}/{Title}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Articles/id_15", out parameters);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_UrlOneParameterRequired_TwoSpecified()
        {
            var route = new DotvvmRoute("Article/{Id}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/15/test", out parameters);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_UrlTwoParametersBothRequired_BothSpecified()
        {
            var route = new DotvvmRoute("Article/id_{Id}/{Title}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/id_15/test", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual("15", parameters["Id"]);
            Assert.AreEqual("test", parameters["Title"]);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_OneOptionalPrefixedParameter()
        {
            var route = new DotvvmRoute("{Id?}/Article", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(0, parameters.Count);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_OneOptionalSuffixedParameter_WithConstraint()
        {
            var route = new DotvvmRoute("Article/{Id?:int}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(0, parameters.Count);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_OneOptionalSuffixedParameter_WithConstraint_SlashAtTheEnd()
        {
            var route = new DotvvmRoute("Article/{Id?:int}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(0, parameters.Count);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_JustOneOptionalParameter()
        {
            var route = new DotvvmRoute("{Id?}", null, "testpage", null, null, configuration);

            Assert.IsTrue(route.IsMatch("", out var params1));
            Assert.AreEqual(0, params1.Count);

            Assert.IsTrue(route.IsMatch("a", out var params2));
            Assert.AreEqual(1, params2.Count);
            Assert.AreEqual("a", params2["Id"]);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_JustOneOptionalParameterWithConstraint()
        {
            var route = new DotvvmRoute("{Id?:int}", null, "testpage", null, null, configuration);

            Assert.IsTrue(route.IsMatch("", out var params1));
            Assert.AreEqual(0, params1.Count);

            Assert.IsFalse(route.IsMatch("a", out var params2));

            Assert.IsTrue(route.IsMatch("1", out var params3));
            Assert.AreEqual(1, params3.Count);
            Assert.AreEqual(1, params3["Id"]);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_JustOneOptionalParameterWithConstraint_DefaultValue()
        {
            var route = new DotvvmRoute("{Id?:int}", null, "testpage", new { Id = 0 }, null, configuration);

            Assert.IsTrue(route.IsMatch("", out var params1));
            Assert.AreEqual(1, params1.Count);
            Assert.AreEqual(0, params1["Id"]);

            Assert.IsFalse(route.IsMatch("a", out var params2));

            Assert.IsTrue(route.IsMatch("1", out var params3));
            Assert.AreEqual(1, params3.Count);
            Assert.AreEqual(1, params3["Id"]);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_OneOptionalParameter()
        {
            var route = new DotvvmRoute("Article/{Id?}/edit", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/edit", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(0, parameters.Count);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_OneOptionalParameter_DefaultValue()
        {
            var route = new DotvvmRoute("Article/{Id?}/edit", null, "testpage", new { Id = 0 }, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/edit", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(1, parameters.Count);
            Assert.AreEqual(0, parameters["Id"]);
        }

        [TestMethod]
        public void DotvvmRoute_IsMatch_TwoParameters_OneOptional_Suffix()
        {
            var route = new DotvvmRoute("Article/Test/{Id?}/{Id2}/suffix", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/Test/5/suffix", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(1, parameters.Count);
        }

        [TestMethod]
        public void LocalizedDotvvmRoute_IsMatch_ExactCultureMatch()
        {
            CultureUtils.RunWithCulture("cs-CZ", () =>
            {
                var route = new LocalizedDotvvmRoute("cs-CZ", new [] {
                    new LocalizedRouteUrl("cs", "cs"),
                    new LocalizedRouteUrl("cs-CZ", "cs-CZ"),
                    new LocalizedRouteUrl("en", "en")
                }, "", "testpage", null, _ => null, configuration);

                var result = route.IsMatch("cs-CZ", out var parameters);
                Assert.IsTrue(result);
            });
        }

        [TestMethod]
        public void LocalizedDotvvmRoute_IsMatch_TwoLetterCultureMatch()
        {
            CultureUtils.RunWithCulture("en-US", () => {
                var route = new LocalizedDotvvmRoute("en", new[] {
                    new LocalizedRouteUrl("cs", "cs"),
                    new LocalizedRouteUrl("cs-CZ", "cs-CZ"),
                    new LocalizedRouteUrl("en", "en")
                }, "", "testpage", null, _ => null, configuration);

                var result = route.IsMatch("en", out var parameters);
                Assert.IsTrue(result);
            });
        }

        [TestMethod]
        public void LocalizedDotvvmRoute_IsMatch_InvalidCultureMatch()
        {
            CultureUtils.RunWithCulture("en-US", () => {
                var route = new LocalizedDotvvmRoute("", new[] {
                    new LocalizedRouteUrl("cs", "cs"),
                    new LocalizedRouteUrl("cs-CZ", "cs-CZ"),
                    new LocalizedRouteUrl("en", "en")
                }, "", "testpage", null, _ => null, configuration);

                var result = route.IsMatch("cs", out var parameters);
                Assert.IsFalse(result);
            });
        }

        [TestMethod]
        public void LocalizedDotvvmRoute_IsPartialMatch()
        {
            CultureUtils.RunWithCulture("en-US", () => {
                var route = new LocalizedDotvvmRoute("", new[] {
                    new LocalizedRouteUrl("cs", "cs"),
                    new LocalizedRouteUrl("cs-CZ", "cs-CZ"),
                    new LocalizedRouteUrl("en", "en")
                }, "", "testpage", null, _ => null, configuration);

                var result = route.IsPartialMatch("cs", out var matchedRoute, out var parameters);
                Assert.IsTrue(result);
                Assert.AreEqual("cs", matchedRoute.Url);
            });
        }

        [DataTestMethod]
        [DataRow("product/{id?}/{name:maxLength(5)}", "en/products/{id?}/{name:maxLength(10)}")]
        [DataRow("product/{id?}/{name:maxLength(5)}", "en/products/{id?}/{name}")]
        [DataRow("product/{id?}/{name:maxLength(5)}", "en/products/{Id:int?}/{name}")]
        [DataRow("product/{id?}/{name:maxLength(5)}", "en/products/{abc}")]
        [DataRow("product/{id?}/{name:maxLength(5)}", "en/products/{Id?}/{name:maxLength(5)}")]
        public void LocalizedDotvvmRoute_RouteConstraintChecks(string defaultRoute, string localizedRoute)
        {
            Assert.ThrowsException<ArgumentException>(() => {
                var route = new LocalizedDotvvmRoute(defaultRoute, new[] {
                    new LocalizedRouteUrl("en", localizedRoute)
                }, "", "testpage", null, _ => null, configuration);
            });
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_UrlTwoParameters()
        {
            var route = new DotvvmRoute("Article/id_{Id}/{Title}", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { Id = 15, Title = "Test" });

            Assert.AreEqual("~/Article/id_15/Test", result);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_OnePart()
        {
            var route = new DotvvmRoute("Article", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { });

            Assert.AreEqual("~/Article", result);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_TwoParts()
        {
            var route = new DotvvmRoute("Article/Test", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { });

            Assert.AreEqual("~/Article/Test", result);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_TwoParts_OptionalParameter_NoValue()
        {
            var route = new DotvvmRoute("Article/Test/{Id?}", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { });

            Assert.AreEqual("~/Article/Test", result);
        }


        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_TwoParts_OptionalParameter_WithValue()
        {
            var route = new DotvvmRoute("Article/Test/{Id?}", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { Id = 5 });

            Assert.AreEqual("~/Article/Test/5", result);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_TwoParts_TwoParameters_OneOptional_NoValue()
        {
            var route = new DotvvmRoute("Article/Test/{Id}/{Id2?}", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { Id = 5 });

            Assert.AreEqual("~/Article/Test/5", result);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_TwoParts_TwoParameters_OneOptional_NoValue_Suffix()
        {
            var route = new DotvvmRoute("Article/Test/{Id}/{Id2?}/suffix", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { Id = 5 });

            Assert.AreEqual("~/Article/Test/5/suffix", result);
        }


        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_TwoParts_TwoParameters_OneOptional_WithValue()
        {
            var route = new DotvvmRoute("Article/Test/{Id}/{Id2?}", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { Id = 5, Id2 = "aaa" });

            Assert.AreEqual("~/Article/Test/5/aaa", result);
        }



        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_TwoParts_TwoParameters_OneOptional_WithValue_Suffix()
        {
            var route = new DotvvmRoute("Article/Test/{Id}/{Id2?}/suffix", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { Id = 5, Id2 = "aaa" });

            Assert.AreEqual("~/Article/Test/5/aaa/suffix", result);
        }



        [TestMethod]
        public void DotvvmRoute_BuildUrl_Static_TwoParts_TwoParameters_FirstOptionalOptional_Suffix()
        {
            var route = new DotvvmRoute("Article/Test/{Id?}/{Id2}/suffix", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { Id2 = "aaa" });

            Assert.AreEqual("~/Article/Test/aaa/suffix", result);
        }


        [TestMethod]
        public void DotvvmRoute_BuildUrl_CombineParameters_OneOptional()
        {
            var route = new DotvvmRoute("Article/{Id?}", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new Dictionary<string, object>() { { "Id", 5 } }, new { });

            Assert.AreEqual("~/Article/5", result);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_ParameterOnly()
        {
            var route = new DotvvmRoute("{Id?}", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { });

            Assert.AreEqual("~/", result);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_OptionalParameter()
        {
            var route = new DotvvmRoute("myPage/{Id?}/edit", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { });
            var result2 = route.BuildUrl(new Dictionary<string, object> { ["Id"] = null });

            Assert.AreEqual("~/myPage/edit", result);
            Assert.AreEqual("~/myPage/edit", result2);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_OneOptionalPrefixedParameter()
        {
            var route = new DotvvmRoute("{Id?}/Article", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(new { });
            var result2 = route.BuildUrl(new Dictionary<string, object> { ["Id"] = 0 });

            Assert.AreEqual("~/Article", result);
            Assert.AreEqual("~/0/Article", result2);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_NullInParameter()
        {
            var route = new DotvvmRoute("myPage/{Id}/edit", null, "testpage", null, null, configuration);

            var ex = Assert.ThrowsException<DotvvmRouteException>(() => {
                route.BuildUrl(new Dictionary<string, object> { ["Id"] = null });
            });
            Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentNullException));
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_NoParameter()
        {
            var route = new DotvvmRoute("RR", null, "testpage", null, null, configuration);

            var result = route.BuildUrl(null);

            Assert.AreEqual("~/RR", result);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_InvariantCulture()
        {
            CultureUtils.RunWithCulture("cs-CZ", () =>
            {
                var route = new DotvvmRoute("RR-{p}", null, "testpage", null, null, configuration);

                var result = route.BuildUrl(new { p = 1.1 });

                Assert.AreEqual("~/RR-1.1", result);
            });
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_UrlEncode()
        {
            CultureUtils.RunWithCulture("cs-CZ", () => {
                var route = new DotvvmRoute("RR-{p}", null, "testpage", null, null, configuration);

                var result = route.BuildUrl(new { p = 1.1});

                Assert.AreEqual("~/RR-1.1", result);
            });
        }

        [TestMethod]

        public void DotvvmRoute_BuildUrl_CustomPrimitiveType()
        {
            CultureUtils.RunWithCulture("cs-CZ", () => {
                var route = new DotvvmRoute("Test/{Id}", null, "testpage", null, null, configuration);

                var result = route.BuildUrl(new { Id = new DecimalNumber(123.4m) }) + UrlHelper.BuildUrlSuffix(null, new { Id = new DecimalNumber(555.5m) });
                Assert.AreEqual("~/Test/123%2C4?Id=555%2C5", result);
            });
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_Invalid_UnclosedParameter()
        {
            Assert.ThrowsException<ArgumentException>(() => {

                var route = new DotvvmRoute("{Id", null, "testpage", null, null, configuration);

                var result = route.BuildUrl(new { });
            });
        }

        [TestMethod]

        public void DotvvmRoute_BuildUrl_Invalid_UnclosedParameterConstraint()
        {
            Assert.ThrowsException<ArgumentException>(() => {

                var route = new DotvvmRoute("{Id:int", null, "testpage", null, null, configuration);

                var result = route.BuildUrl(new { });
            });
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_Parameter_UrlDecode()
        {
            var route = new DotvvmRoute("Article/{Title}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/" + Uri.EscapeDataString("x a d # ? %%%%% | ://"), out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(1, parameters.Count);
            Assert.AreEqual("x a d # ? %%%%% | ://", parameters["Title"]);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_ParameterConstraint_Int()
        {
            var route = new DotvvmRoute("Article/id_{Id:int}/{Title}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("Article/id_15/test", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual(15, parameters["Id"]);
            Assert.AreEqual("test", parameters["Title"]);
        }
        [TestMethod]
        public void DotvvmRoute_BuildUrl_ParameterConstraint_FloatingPoints()
        {
            var route = new DotvvmRoute("R/{float:float}-{double:double}-{decimal:decimal}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            var result = route.IsMatch("R/1.12-1.12-1.12", out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(3, parameters.Count);
            Assert.AreEqual(1.12f, parameters["float"]);
            Assert.AreEqual(1.12, parameters["double"]);
            Assert.AreEqual(1.12m, parameters["decimal"]);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_ParameterConstraint_Guid()
        {
            var route = new DotvvmRoute("{guid1:guid}{guid2:guid}{guid3:guid}{guid4:guid}", null, "testpage", null, null, configuration);
            var guids = new[]
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
            };

            IDictionary<string, object> parameters;
            var result = route.IsMatch(string.Concat(guids), out parameters);

            Assert.IsTrue(result);
            Assert.AreEqual(4, parameters.Count);
            Assert.AreEqual(guids[0], parameters["guid1"]);
            Assert.AreEqual(guids[1], parameters["guid2"]);
            Assert.AreEqual(guids[2], parameters["guid3"]);
            Assert.AreEqual(guids[3], parameters["guid4"]);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_ParameterConstraint_Max()
        {
            var route = new DotvvmRoute("{p:max(100)}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            Assert.IsFalse(route.IsMatch("101", out parameters));
            Assert.IsFalse(route.IsMatch("100.1", out parameters));
            Assert.IsFalse(route.IsMatch("djhsjlkdsjalkd", out parameters));
            Assert.IsTrue(route.IsMatch("54.11", out parameters));

            Assert.AreEqual(1, parameters.Count);
            Assert.AreEqual(54.11, parameters["p"]);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_ParameterConstraint_Ranges()
        {
            var route = new DotvvmRoute("{range:range(1, 100.1)}/{max:max(100)}/{min:min(-55)}/{negrange:range(-100, -12)}/{posint:posint}", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            Assert.IsTrue(route.IsMatch("50/0/0/-50/5", out parameters));
            Assert.IsTrue(route.IsMatch("100.045/0.444/0.84/-50.45/0", out parameters));
            Assert.IsFalse(route.IsMatch("100.045/0.444/0.84/-50.45/5.5", out parameters));
            Assert.IsFalse(route.IsMatch("120/0/0/-50/5", out parameters));
            Assert.IsFalse(route.IsMatch("50/100.01/0/-50/5", out parameters));
            Assert.IsFalse(route.IsMatch("50/50/-100/-101/5", out parameters));
            Assert.IsFalse(route.IsMatch("50/50/-100/-55/-5", out parameters));
            Assert.IsTrue(route.IsMatch("54.11/-1000000/0.84/-50.45/0044", out parameters));

            Assert.AreEqual(5, parameters.Count);
            Assert.AreEqual(54.11, parameters["range"]);
            Assert.AreEqual(-1000000.0, parameters["max"]);
            Assert.AreEqual(0.84, parameters["min"]);
            Assert.AreEqual(-50.45, parameters["negrange"]);
            Assert.AreEqual(44, parameters["posint"]);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_ParameterConstraint_Bool()
        {
            var route = new DotvvmRoute("la{bool:bool}eheh", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            Assert.IsTrue(route.IsMatch("latrueeheh", out parameters));
            Assert.IsTrue(route.IsMatch("lafALseeheh", out parameters));

            Assert.AreEqual(1, parameters.Count);
            Assert.AreEqual(false, parameters["bool"]);
        }

        [TestMethod]
        public void DotvvmRoute_BuildUrl_ParameterConstraintAlpha()
        {
            var route = new DotvvmRoute("la1{aplha:alpha}7huh", null, "testpage", null, null, configuration);

            IDictionary<string, object> parameters;
            Assert.IsTrue(route.IsMatch("la1lala7huh", out parameters));
            Assert.IsTrue(route.IsMatch("la1ahoj7huh", out parameters));

            Assert.AreEqual(1, parameters.Count);
            Assert.AreEqual("ahoj", parameters["aplha"]);
        }

        [TestMethod]
        public void DotvvmRoute_Performance()
        {
            var route = new DotvvmRoute("Article/{name}@{domain}/{id:int}", null, "testpage", null, null, configuration);
            IDictionary<string, object> parameters;
            Assert.IsFalse(route.IsMatch("Article/f" + new string('@', 2000) + "f/4f", out parameters));
        }

        [TestMethod]
        public void DotvvmRoute_PresenterFactoryMethod()
        {
            var configuration = DotvvmConfiguration.CreateDefault(services => {
                services.TryAddScoped<TestPresenter>();
            });

            var table = new DotvvmRouteTable(configuration);
            table.Add("Article", "", typeof(TestPresenter), null);
            Assert.IsInstanceOfType(table.First().GetPresenter(configuration.ServiceProvider), typeof(TestPresenter));

            Assert.ThrowsException<ArgumentException>(() => {
                table.Add("Blog", "", typeof(TestPresenterWithoutInterface));
            });
        }

        [TestMethod]
        public void DotvvmRoute_PresenterType()
        {
            var configuration = DotvvmConfiguration.CreateDefault(services => {
                services.TryAddScoped<TestPresenter>();
            });

            var table = new DotvvmRouteTable(configuration);
            table.Add("Article", "", provider => provider.GetRequiredService<TestPresenter>(), null);
            Assert.IsInstanceOfType(table.First().GetPresenter(configuration.ServiceProvider), typeof(TestPresenter));
        }

        [TestMethod]
        public void DotvvmRoute_RegexConstraint()
        {
            var route = new DotvvmRoute("test/{Name:regex((aa|bb|cc))}", null, "testpage", null, null, configuration);
            Assert.IsTrue(route.IsMatch("test/aa", out var parameters));
            Assert.IsTrue(route.IsMatch("test/bb", out parameters));
            Assert.IsTrue(route.IsMatch("test/cc", out parameters));
            Assert.IsFalse(route.IsMatch("test/aaaa", out parameters));
        }

        [TestMethod]
        public void DotvvmRoute_UrlWithoutTypes()
        {
            string parse(string url) => new DotvvmRoute(url, null, "testpage", null, null, configuration).UrlWithoutTypes;

            Assert.AreEqual(parse("test/xx/12"), "test/xx/12");
            Assert.AreEqual(parse("test/{Param}-{PaRAM2}"), "test/{param}-{param2}");
            Assert.AreEqual(parse("test/{Param?}-{PaRAM2?}"), "test/{param}-{param2}");
            Assert.AreEqual(parse("test/{Param:int}-{PaRAM2?:regex(.*)}"), "test/{param}-{param2}");
            Assert.AreEqual(parse("test/{Param:int}-{PaRAM2?:regex((.){4,10})}"), "test/{param}-{param2}");
        }
    }

    record struct DecimalNumber(decimal Value) : IFormattable
    {
        public static bool TryParse(string value, out decimal result)
        {
            if (decimal.TryParse(value, out var r))
            {
                result = r;
                return true;
            }
            result = default;
            return false;
        }
        public override string ToString() => Value.ToString();
        public string ToString(string format, IFormatProvider formatProvider) => Value.ToString(null, formatProvider);
    }

    public class TestPresenterWithoutInterface
    {

    }

    public class TestPresenter : IDotvvmPresenter
    {
        public Task ProcessRequest(IDotvvmRequestContext context)
        {
            throw new NotImplementedException();
        }
    }
}
