using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Tasks;
using CheckTestOutput;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Tests.Binding;
using DotVVM.Framework.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotVVM.Framework.Testing;
using DotVVM.Framework.Compilation.Styles;

namespace DotVVM.Framework.Tests.ControlTests
{
    [TestClass]
    public class ResourceDataContextTests
    {
        static readonly ControlTestHelper cth = new ControlTestHelper(config: config => {
            _ = Controls.Repeater.RenderAsNamedTemplateProperty;
            config.Styles.Register<Repeater>().SetProperty(r => r.RenderAsNamedTemplate, false, StyleOverrideOptions.Ignore);
        });
        OutputChecker check = new OutputChecker("testoutputs");

        [TestMethod]
        public async Task Error_ValueBindingUsage()
        {
            var e = await Assert.ThrowsExceptionAsync<DotvvmCompilationException>(() => cth.RunPage(typeof(TestViewModel), @"
                    <div DataContext={resource: ServerOnlyCustomer}>
                       {{value: Name}}
                    </div>
               "
            ));

            StringAssert.StartsWith(e.GetBaseException().Message, "_this of type ResourceDataContextTests.TestViewModel.CustomerData cannot be translated to JavaScript");
        }

        [TestMethod]
        public async Task Error_StaticCommandBindingUsage()
        {
            var e = await Assert.ThrowsExceptionAsync<DotvvmCompilationException>(() => cth.RunPage(typeof(TestViewModel), @"
                    <div DataContext={resource: ServerOnlyCustomer}>
                        <dot:Button Click={staticCommand: _root.CommandData = Name} />
                    </div>
               "
            ));

            StringAssert.StartsWith(e.GetBaseException().Message, "_this of type ResourceDataContextTests.TestViewModel.CustomerData cannot be translated to JavaScript, it can only be used in resource and command bindings.");
        }

        [TestMethod]
        public async Task BasicDataContext()
        {
            var r = await cth.RunPage(typeof(TestViewModel), @"
                    <div DataContext={resource: ServerOnlyCustomer}>
                        <span class=name data-id={resource: Id}>{{resource: Name}}</span>

                        <span>{{value: _parent.CommandData}}</span>

                        <dot:Button Click={command: _root.TestMethod(Name)} />
                    </div>
               "
            );

            check.CheckString(r.FormattedHtml, fileExtension: "html");

            await r.RunCommand("_root.TestMethod(Name)");

            Assert.AreEqual((string)r.ViewModelJson["CommandData"], "Server o. Customer");
        }

        [TestMethod]
        public async Task Repeater()
        {
            var r = await cth.RunPage(typeof(TestViewModel), @"

                    <!-- without wrapper tag -->
                    <dot:Repeater DataSource={resource: Customers.Items} RenderWrapperTag=false>
                        <EmptyDataTemplate> This would be here if the Customers.Items were empty </EmptyDataTemplate>
                        <SeparatorTemplate>
                            -------------------
                        </SeparatorTemplate>
                        <span class=name data-id={resource: Id}>{{resource: Name}}</span>

                        <span>{{value: _parent.CommandData}}</span>
                    </dot:Repeater>

                    <!-- with wrapper tag -->
                    <dot:Repeater DataSource={resource: Customers} WrapperTagName=div>
                        <EmptyDataTemplate> This would be here if the Customers.Items were empty </EmptyDataTemplate>
                        <SeparatorTemplate>
                            -------------------
                        </SeparatorTemplate>
                        <span class=name data-id={resource: Id}>{{resource: Name}}</span>

                        <span>{{value: _parent.CommandData}}</span>
                    </dot:Repeater>
               "
            );

            check.CheckString(r.FormattedHtml, fileExtension: "html");
        }

        [TestMethod]
        public async Task RepeaterWithCommand()
        {
            var r = await cth.RunPage(typeof(TestViewModel), @"
                    <dot:Repeater DataSource={resource: Customers.Items}>
                        <dot:Button Click={command: _root.TestMethod(Name)} />
                    </dot:Repeater>
               "
            );

            await r.RunCommand("_root.TestMethod(Name)", x => x is TestViewModel.CustomerData { Id: 1 });
            Assert.AreEqual((string)r.ViewModelJson["CommandData"], "One");

            await r.RunCommand("_root.TestMethod(Name)", x => x is TestViewModel.CustomerData { Id: 2 });
            Assert.AreEqual((string)r.ViewModelJson["CommandData"], "Two");
        }

        [TestMethod]
        public async Task DataContextRevert()
        {
            // revert client-side data context by DataContext={value: _root...}
            var r = await cth.RunPage(typeof(TestViewModel), @"

                    <dot:Repeater DataSource={resource: Customers.Items} RenderWrapperTag=false>
                        <span class=name data-id={resource: Id}>{{resource: Name}}</span>

                        <dot:Repeater DataSource={value: _root.FewStrings}>
                            {{value: _root.StringPrefix + _this}}
                        </dot:Repeater>
                    </dot:Repeater>
               "
            );

            check.CheckString(r.FormattedHtml, fileExtension: "html");
        }

        [TestMethod]
        public async Task HierarchyRepeater_SimpleTemplate()
        {
            var r = await cth.RunPage(typeof(TestViewModel), @"

                    <!-- without wrapper tags -->
                    <dot:HierarchyRepeater DataSource={resource: Customers.Items}
                                           ItemChildrenBinding={resource: NextLevelCustomers}
                                           RenderWrapperTag=false>
                        <EmptyDataTemplate> This would be here if the Customers.Items were empty </EmptyDataTemplate>
                        <span data-id={resource: Id}>{{resource: Name}}</span>
                    </dot:HierarchyRepeater>

                    <!-- with wrapper tags -->
                    <dot:HierarchyRepeater DataSource={resource: Customers.Items}
                                           ItemChildrenBinding={resource: NextLevelCustomers}
                                           LevelWrapperTagName=ul
                                           ItemWrapperTagName=li>
                        <span data-id={resource: Id}>{{resource: Name}}</span>
                    </dot:HierarchyRepeater>
               "
            );

            check.CheckString(r.FormattedHtml, fileExtension: "html");
        }

        [TestMethod]
        public async Task GridView()
        {
            var r = await cth.RunPage(typeof(TestViewModel), @"
                <dot:GridView DataSource={resource: Customers}>
                    <dot:GridViewTextColumn HeaderText=Id ValueBinding={resource: Id} />
                    <dot:GridViewTemplateColumn HeaderText=Name>
                        {{resource: Name}}
                    </dot:GridViewTemplateColumn>
                </dot:GridView>");
            check.CheckString(r.FormattedHtml, fileExtension: "html");
        }


        public class TestViewModel: DotvvmViewModelBase
        {
            public string NullableString { get; } = null;


            [Bind(Direction.None)]
            public CustomerData ServerOnlyCustomer { get; set; } = new CustomerData(100, "Server o. Customer", new());

            public GridViewDataSet<CustomerData> Customers { get; set; } = new GridViewDataSet<CustomerData>() {
                RowEditOptions = {
                    EditRowId = 1,
                    PrimaryKeyPropertyName = nameof(CustomerData.Id)
                },
                Items = {
                    new CustomerData(1, "One", new()),
                    new CustomerData(2, "Two", new() {
                        new CustomerData(21, "first pyramid customer", new() {
                            new CustomerData(211, "second pyramid customer", new())
                        })
                    })
                }
            };

            public List<string> FewStrings { get; set; } = new List<string>() { "Hi!", "Ahoj!" };

            public string StringPrefix { get; set; } = "...";

            public UploadedFilesCollection Files { get; set; } = new UploadedFilesCollection();

            public record CustomerData(
                int Id,
                [property: Required]
                string Name,
                // software for running MLM 😂
                List<CustomerData> NextLevelCustomers
            );

            public string CommandData { get; set; }

            public void TestMethod(string data)
            {
                CommandData = data;
            }
        }
    }
}
