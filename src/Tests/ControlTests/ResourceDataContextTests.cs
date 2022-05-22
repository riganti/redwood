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
using System.Security.Claims;

namespace DotVVM.Framework.Tests.ControlTests
{
    [TestClass]
    public class ResourceDataContextTests
    {
        static readonly ControlTestHelper cth = new ControlTestHelper(config: config => {
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

                        <dot:Button Click={command: _root.TestMethod(Name)} />
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

            await r.RunCommand("_root.TestMethod(Name)", x => x is TestViewModel.CustomerData { Id: 1 });
            Assert.AreEqual((string)r.ViewModelJson["CommandData"], "One");

            await r.RunCommand("_root.TestMethod(Name)", x => x is TestViewModel.CustomerData { Id: 2 });
            Assert.AreEqual((string)r.ViewModelJson["CommandData"], "Two");
        }

        public class TestViewModel: DotvvmViewModelBase
        {
            public string NullableString { get; } = null;


            [Bind(Direction.None)]
            public CustomerData ServerOnlyCustomer { get; set; } = new CustomerData(100, "Server o. Customer");

            public GridViewDataSet<CustomerData> Customers { get; set; } = new GridViewDataSet<CustomerData>() {
                RowEditOptions = {
                    EditRowId = 1,
                    PrimaryKeyPropertyName = nameof(CustomerData.Id)
                },
                Items = {
                    new CustomerData(1, "One"),
                    new CustomerData(2, "Two")
                }
            };

            public UploadedFilesCollection Files { get; set; } = new UploadedFilesCollection();

            public record CustomerData(
                int Id,
                [property: Required]
                string Name,
                bool Enabled = true
            );

            public string CommandData { get; set; }

            public void TestMethod(string data)
            {
                CommandData = data;
            }
        }
    }
}
