using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CheckTestOutput;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotVVM.Framework.Tests.Runtime
{
    [TestClass]
    public class DotvvmControlRenderedHtmlTests : DotvvmControlTestBase
    {
        static readonly BindingTestHelper bindingHelper = new BindingTestHelper();
        readonly OutputChecker outputChecker = new OutputChecker("testoutputs");
        [TestMethod]
        public void GridViewTextColumn_RenderedHtmlTest_ServerRendering()
        {
            var gridView = new GridView(new GridViewDataSetBindingProvider(BindingService), BindingService) {
                Columns = new List<GridViewColumn>
                {
                    new GridViewTextColumn() { HeaderCssClass = "lol", HeaderText="Header Text", ValueBinding = ValueBindingExpression.CreateBinding(BindingService, h => (object)h[0], (DataContextStack)null) }
                },
                DataSource = ValueBindingExpression.CreateBinding(BindingService, h => (IList)h[0], (DataContextStack)null),
            };
            gridView.SetValue(RenderSettings.ModeProperty, RenderMode.Server);
            var viewModel = new[] { "ROW 1", "ROW 2", "ROW 3" };
            var html = InvokeLifecycleAndRender(gridView, CreateContext(viewModel));

            StringAssert.Contains(html, "ROW 2");
            StringAssert.Contains(html, "class=lol");
        }

        [TestMethod]
        public void RepeaterEmptyData_RenderedHtmlTest_ServerRendering()
        {
            Repeater createRepeater(RenderMode renderMode)
            {
                var repeater = new Repeater() {
                    RenderAsNamedTemplate = false,
                    ItemTemplate = new CloneTemplate(new HtmlGenericControl("ITEM_TAG")),
                    EmptyDataTemplate = new CloneTemplate(new HtmlGenericControl("EMPTY_DATA")),
                    DataSource = ValueBindingExpression.CreateThisBinding<string[]>(Configuration.ServiceProvider.GetRequiredService<BindingCompilationService>(), null),
                    RenderWrapperTag = false
                };
                repeater.SetValue(RenderSettings.ModeProperty, renderMode);
                return repeater;
            }
            var viewModel = new string[] { };
            var clientHtml = InvokeLifecycleAndRender(createRepeater(RenderMode.Client), CreateContext(viewModel));

            Assert.IsTrue(clientHtml.Contains("<ITEM_TAG"));
            Assert.IsTrue(clientHtml.Contains("<EMPTY_DATA"));
            Assert.IsTrue(!clientHtml.Contains("<div"));
            Assert.IsTrue(clientHtml.Contains("<!-- ko "));

            var serverHtml = InvokeLifecycleAndRender(createRepeater(RenderMode.Server), CreateContext(viewModel));
            Assert.IsTrue(serverHtml.Contains("<EMPTY_DATA"));
            Assert.IsTrue(!serverHtml.Contains("<div"));
        }

        [TestMethod]
        public void RouteLink_SpaNavigation()
        {
            DotvvmConfiguration configuration;

            RouteLink createRouteLink()
            {
                var routeLink = new RouteLink() {
                    RouteName = "TestRoute"
                };
                routeLink.SetValue(Internal.IsSpaPageProperty, true);

                configuration = DotvvmTestHelper.CreateConfiguration();
                configuration.RouteTable.Add("TestRoute", "TestRoute", "");
                configuration.Freeze();
                return routeLink;
            }

            var routeLinkWithoutTarget = createRouteLink();
            var clientHtml1 = InvokeLifecycleAndRender(routeLinkWithoutTarget, DotvvmTestHelper.CreateContext(configuration));
            Assert.IsTrue(clientHtml1.Contains("dotvvm.handleSpaNavigation(this)"));

            var routeLinkWithTarget = createRouteLink();
            routeLinkWithTarget.Attributes.Add("target", "_blank");
            var clientHtml2 = InvokeLifecycleAndRender(routeLinkWithTarget, DotvvmTestHelper.CreateContext(configuration));
            Assert.IsFalse(clientHtml2.Contains("dotvvm.handleSpaNavigation(this)"));
        }

        [TestMethod]
        public void BindingGroup_EnumProperty()
        {
            var writer = new StringWriter();
            var html = new HtmlWriter(writer, CreateContext(new object()));
            html.AddKnockoutDataBind("tt", new KnockoutBindingGroup() {
                { "test", new TextBox(){ Type = TextBoxType.Date }, TextBox.TypeProperty }
            });
            html.RenderSelfClosingTag("span");
            Assert.AreEqual("<spandata-bind='tt:{test:\"Date\"}'/>", writer.ToString().Replace(" ", ""));
        }

        [TestMethod]
        public void BindingGroup_MultipleBindings_InProperOrder()
        {
            var textbox = new OrderedDataBindTextBox();
            textbox.SetBinding(TextBox.TextProperty,
                ValueBindingExpression.CreateThisBinding<string>(Configuration.ServiceProvider.GetRequiredService<BindingCompilationService>(), null));

            var html = InvokeLifecycleAndRender(textbox, CreateContext(string.Empty));

            StringAssert.Contains(html.Replace(" ", ""), "data-bind=\"first:true,dotvvm-textbox-text:$rawData,second:true\"");
        }

        [TestMethod]
        public void MarkupControl_NoWrapperTagDirective()
        {
            var viewModel = new string[] { };
            var clientHtml = InvokeLifecycleAndRender(new HtmlGenericControl("elem1") {
                Children = {
                    new DotvvmMarkupControl(){
                        Directives = {
                            ["noWrapperTag"] = ""
                        },
                        Children = {
                            new HtmlGenericControl("elem2")
                        }
                    }
                }
            }, CreateContext(viewModel));

            StringAssert.Contains(clientHtml, "<elem1");
            StringAssert.Contains(clientHtml, "<elem2");
            Assert.IsFalse(clientHtml.Contains("<div"), clientHtml);
        }

        [TestMethod]
        public void HtmlGenericControl_MetaTag_RenderContentAttribute()
        {
            var context = CreateContext(new object());
            context.HttpContext = new TestHttpContext { Request = { PathBase = "home" } };

            var clientHtml = InvokeLifecycleAndRender(new HtmlGenericControl("meta").SetAttribute("content", "~/test"), context);

            Assert.IsTrue(clientHtml.Contains("<meta"));
            Assert.IsTrue(clientHtml.Contains("/home/test"));
            Assert.IsTrue(!clientHtml.Contains("~"));
        }

        [TestMethod]
        public void MarkupControl_WrapperTagDirective()
        {
            var viewModel = new string[] { };
            var clientHtml = InvokeLifecycleAndRender(new HtmlGenericControl("elem1") {
                Children = {
                    new DotvvmMarkupControl(){
                        Directives = {
                            ["wrapperTag"] = "elem3"
                        },
                        Children = {
                            new HtmlGenericControl("elem2")
                        }
                    }
                }
            }, CreateContext(viewModel));

            Assert.IsTrue(clientHtml.Contains("<elem1"));
            Assert.IsTrue(clientHtml.Contains("<elem2"));
            Assert.IsTrue(!clientHtml.Contains("<div"));
            Assert.IsTrue(clientHtml.Contains("<elem3"));
        }

        [TestMethod]
        [ExpectedException(typeof(DotvvmControlException))]
        public void MarkupControl_WrapperTagAndNoWrapperTagDirective()
        {
            var viewModel = new string[] { };
            var clientHtml = InvokeLifecycleAndRender(new HtmlGenericControl("elem1") {
                Children = {
                    new DotvvmMarkupControl(){
                        Directives = {
                            ["wrapperTag"] = "elem3",
                            ["noWrapperTag"] = ""
                        },
                        Children = {
                            new HtmlGenericControl("elem2")
                        }
                    }
                }
            }, CreateContext(viewModel));
        }

        [TestMethod]
        public void Literal_DateTimeToBrowserLocalTime_RenderOnServer()
        {
            DotvvmTestHelper.RunInCulture(new CultureInfo("sv"), () =>
            {
                var vm = new LiteralDateTimeViewModel();
                var control = $@"@viewModel {vm.GetType().FullName}
<dot:Literal Text={{resource: DateTime.ToBrowserLocalTime()}} RenderSettings.Mode=Server /><dot:Literal Text={{resource: NullableDateTime.ToBrowserLocalTime()}} RenderSettings.Mode=Server />";

                var dotvvmBuilder = CreateDotvvmViewBuilder(control);
                var context = CreateContext(vm);
                var html = InvokeLifecycleAndRender(dotvvmBuilder.BuildView(context), context);

                Assert.AreEqual(@"<span>2021-01-02 03:04:05</span><span>2021-01-02 03:04:05</span>", html);
            });
        }

        [TestMethod]
        public void Button_ClickArgumentsCommand()
        {
            var vm = new LiteralDateTimeViewModel();
            var command = bindingHelper.Command("null", [ typeof(LiteralDateTimeViewModel) ], typeof(Func<DateTime, int, Task>));
            var button = new Button("text", command) {
                ClickArguments = new object[] {
                    bindingHelper.ValueBinding<DateTime>("DateTime", [ typeof(LiteralDateTimeViewModel) ]),
                    1
                }
            };

            var html = InvokeLifecycleAndRender(button, CreateContext(vm));
            outputChecker.CheckString(html, "Button_ClickArgumentsCommand", fileExtension: "html");
        }


        public class OrderedDataBindTextBox : TextBox
        {
            protected override void AddAttributesToRender(IHtmlWriter writer, IDotvvmRequestContext context)
            {
                writer.AddKnockoutDataBind("first", "true");

                base.AddAttributesToRender(writer, context);
                
                writer.AddKnockoutDataBind("second", "true");
            }
        }
    }

    public class LiteralDateTimeViewModel
    {
        public DateTime DateTime { get; set; } = new DateTime(2021, 1, 2, 3, 4, 5);
        public DateTime? NullableDateTime { get; set; } = new DateTime(2021, 1, 2, 3, 4, 5);
    }
}
