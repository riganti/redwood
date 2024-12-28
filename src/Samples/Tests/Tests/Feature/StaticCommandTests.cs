using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using OpenQA.Selenium;
using Riganti.Selenium.Core;
using Riganti.Selenium.Core.Abstractions;
using Riganti.Selenium.DotVVM;
using Xunit;
using Xunit.Abstractions;

namespace DotVVM.Samples.Tests.Feature
{
    public class StaticCommandTests : AppSeleniumTest
    {
        public StaticCommandTests(ITestOutputHelper output) : base(output)
        {
        }
        ICollection<string> rowWithId1 = new List<string> { "1", "Alice", "Red", "true", "11", "6/2/2018 3:20:45 AM" };
        ICollection<string> rowWithId2 = new List<string> { "2", "Dean", "Green", "false", "3", "6/2/2018 8:45:29 PM" };
        ICollection<string> rowWithId3 = new List<string> { "3", "Everett", "Blue", "false", "5", "1/18/2018 12:09:20 AM" };
        ICollection<string> rowWithId4 = new List<string> { "4", "Jenny", "Blue", "true", "93", "10/20/2018 1:16:35 PM" };
        ICollection<string> rowWithId5 = new List<string> { "5", "Carl", "Blue", "true", "3", "5/29/2019 4:47:25 PM" };
        ICollection<string> rowWithId6 = new List<string> { "6", "Karen", "Red", "false", "121", "2/15/2019 11:37:15 AM" };
        ICollection<string> rowWithId7 = new List<string> { "7", "John", "Red", "true", "12", "5/28/2020 8:57:41 PM" };
        ICollection<string> rowWithId8 = new List<string> { "8", "Johnny", "Red", "false", "15", "1/21/2018 7:03:41 AM" };
        ICollection<string> rowWithId9 = new List<string> { "9", "Robert", "Green", "true", "19", "5/22/2019 6:58:33 PM" };
        ICollection<string> rowWithId10 = new List<string> { "10", "Roger", "-1", "false", "27", "12/1/2020 6:57:57 AM" };

        [Fact]
        [SampleReference(nameof(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_ValueAssignmentInControl))]
        public void Feature_StaticCommand_ValueAssignmentInControl()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_ValueAssignmentInControl);

                var result = browser.First("#result");

                var setText = browser.First("#setText");
                var setNull = browser.First("#setNull");

                //default
                AssertUI.TextEquals(result, "");

                setText.Click();
                AssertUI.TextEquals(result, "text");

                setNull.Click();
                AssertUI.TextEquals(result, "");

                setText.Click();
                AssertUI.TextEquals(result, "text");
            });
        }

        [Theory]
        [InlineData("compute-static-method")]
        [InlineData("compute-string")]
        [InlineData("compute-service-async")]
        [InlineData("compute-service")]
        public void Feature_StaticCommand_StaticCommand(string selector)
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand);
                browser.IsDotvvmPage();

                var b = browser.First(selector, SelectByDataUi);
                b.Click();
                AssertUI.InnerTextEquals(browser.First("span"), "Hello Deep Thought!");
            });
        }

        [Theory]
        [InlineData("service-null")]
        [InlineData("static-null")]
        [InlineData("null")]
        public void Feature_StaticCommand_StaticCommand_ObjectAssignation(string selector)
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand);
                browser.IsDotvvmPage();

                var name = browser.First("name", SelectByDataUi);
                var value = browser.First("value", SelectByDataUi);

                var a = browser.First("service-object", SelectByDataUi);
                a.Click();

                AssertUI.TextEquals(name, "Hello DotVVM!");
                AssertUI.TextEquals(value, "1");

                var b = browser.First(selector, SelectByDataUi);
                b.Click();

                AssertUI.TextEmpty(name);
                AssertUI.TextEmpty(value);

                a.Click();
                AssertUI.TextEquals(name, "Hello DotVVM!");
                AssertUI.TextEquals(value, "1");

                b.Click();
                AssertUI.TextEmpty(name);
                AssertUI.TextEmpty(value);
            });
        }

        [Fact]
        [SampleReference(nameof(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_NullBinding))]
        public void Feature_StaticCommand_NullBinding()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_NullBinding);
                browser.IsDotvvmPage();

                var showSelected = browser.First("#show-selected");
                AssertUI.IsNotDisplayed(showSelected);

                browser.First("#listObject > input:nth-child(2)").Click();
                AssertUI.IsDisplayed(showSelected);
                AssertUI.InnerTextEquals(showSelected, "Hello 2");

                browser.First("#listObject > input:nth-child(3)").Click();
                AssertUI.IsDisplayed(showSelected);
                AssertUI.IsDisplayed(showSelected);
                AssertUI.InnerTextEquals(showSelected, "Hello 3");

                browser.First("#listObject > input:nth-child(4)").Click();
                AssertUI.IsNotDisplayed(showSelected);

                browser.First("#listObject > input:nth-child(1)").Click();

                AssertUI.IsDisplayed(showSelected);
                AssertUI.InnerTextEquals(showSelected, "Hello 1");
            });
        }

        [Fact]
        [SampleReference(nameof(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_NullAssignment))]
        public void Feature_StaticCommand_NullAssignment()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_NullAssignment);
                browser.IsDotvvmPage();

                void TestSection(string sectionName)
                {
                    AssertUI.InnerTextEquals(browser.Single($".type-{sectionName} .item1"), "not null");
                    AssertUI.InnerTextEquals(browser.Single($".type-{sectionName} .item2"), "not null");
                    browser.Single($".type-{sectionName} input[type=button]").Click();
                    AssertUI.InnerTextEquals(browser.Single($".type-{sectionName} .item1"), "null");
                    AssertUI.InnerTextEquals(browser.Single($".type-{sectionName} .item2"), "null");
                }

                TestSection("int");
                TestSection("string");
                TestSection("datetime");
                TestSection("object");
            });
        }


        [Fact]
        [SampleReference(nameof(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_ComboBoxSelectionChanged))]
        public void Feature_StaticCommand_ComboBoxSelectionChanged()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_ComboBoxSelectionChanged);
                Feature_StaticCommand_ComboBoxSelectionChangedViewModel_Core(browser);
            });
        }

        [Fact]
        public void Feature_StaticCommand_StaticCommand_ComboBoxSelectionChanged_Objects()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_ComboBoxSelectionChanged_Objects);
                Feature_StaticCommand_ComboBoxSelectionChangedViewModel_Core(browser);
            });
        }

        private static void Feature_StaticCommand_ComboBoxSelectionChangedViewModel_Core(IBrowserWrapper browser)
        {
            browser.IsDotvvmPage();

            // select second value in the first combo box, the second one should select the second value too 
            browser.ElementAt("select", 0).Select(1);
            browser.ElementAt("select", 1).Select(1);
            AssertUI.IsSelected(browser.ElementAt("select", 0).ElementAt("option", 1));
            AssertUI.IsSelected(browser.ElementAt("select", 1).ElementAt("option", 1));

            // select third value in the first combo box, the second one should select the third value too 
            browser.ElementAt("select", 0).Select(2);
            browser.ElementAt("select", 1).Select(2);
            AssertUI.IsSelected(browser.ElementAt("select", 0).ElementAt("option", 2));
            AssertUI.IsSelected(browser.ElementAt("select", 1).ElementAt("option", 2));

            // select first value in the first combo box, the second one should select the first value too 
            browser.ElementAt("select", 0).Select(0);
            browser.ElementAt("select", 1).Select(0);
            AssertUI.IsSelected(browser.ElementAt("select", 0).ElementAt("option", 0));
            AssertUI.IsSelected(browser.ElementAt("select", 1).ElementAt("option", 0));

            // click the first button - the second value should be selected in the first select, the second select should not change
            browser.ElementAt("input", 0).Click();
            AssertUI.IsSelected(browser.ElementAt("select", 0).ElementAt("option", 1));
            AssertUI.IsSelected(browser.ElementAt("select", 1).ElementAt("option", 0));

            // click the second button - the third value should be selected in the second select, the first select should not change
            browser.ElementAt("input", 1).Click();
            AssertUI.IsSelected(browser.ElementAt("select", 0).ElementAt("option", 1));
            AssertUI.IsSelected(browser.ElementAt("select", 1).ElementAt("option", 2));

            // click the third button - the first value should be selected in the second select, the first select should not change
            browser.ElementAt("input", 2).Click();
            AssertUI.IsSelected(browser.ElementAt("select", 0).ElementAt("option", 1));
            AssertUI.IsSelected(browser.ElementAt("select", 1).ElementAt("option", 0));
        }

        [Fact]
        public void Feature_StaticCommand_StaticCommand_TaskSequence()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_TaskSequence);

                var textBox = browser.Single("input[type=text]");
                var commandButton = browser.ElementAt("input[type=button]", 0);
                var staticCommandButton = browser.ElementAt("input[type=button]", 1);
                var internalSequenceStaticCommandButton = browser.ElementAt("input[type=button]", 2);
                void resetTextbox()
                {
                    textBox.Clear();
                    textBox.SendKeys("0");
                    textBox.SendKeys(Keys.Tab);
                }

                commandButton.Click();
                AssertUI.Value(textBox, "55");


                resetTextbox();
                staticCommandButton.Click();

                AssertUI.Value(textBox, "55");

                resetTextbox();
                internalSequenceStaticCommandButton.Click();

                AssertUI.Value(textBox, "2");
            });
        }

        [Fact]
        public void Feature_StaticCommand_StaticCommand_ArrayAssignment()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_ArrayAssigment);

                AssertUI.InnerTextEquals(browser.ElementAt(".item", 0), "Anne");
                AssertUI.InnerTextEquals(browser.ElementAt(".item", 1), "Martin");

                var button = browser.Single("input[type=button]");
                button.Click();

                AssertUI.InnerTextEquals(browser.ElementAt(".item", 0), "Bob");
                AssertUI.InnerTextEquals(browser.ElementAt(".item", 1), "Oliver");
                AssertUI.InnerTextEquals(browser.ElementAt(".item", 2), "Pablo");
            });
        }

        [Fact]
        public void Feature_StaticCommand_StaticCommand_LoadComplexDataFromService()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_StaticCommand_LoadComplexDataFromService);

                var textBox = browser.Single("input[type=text]");
                textBox.SendKeys("Vindaloo");

                var button = browser.Single("input[type=button]");
                button.Click();

                AssertUI.InnerTextEquals(browser.ElementAt(".name", 0), "Martin");
                AssertUI.InnerTextEquals(browser.ElementAt(".name", 1), "Roman");
                AssertUI.InnerTextEquals(browser.ElementAt(".name", 2), "Igor");

                AssertUI.InnerTextEquals(browser.ElementAt(".food", 0), "Burger");
                AssertUI.InnerTextEquals(browser.ElementAt(".food", 1), "Pizza");
                AssertUI.InnerTextEquals(browser.ElementAt(".food", 2), "Vindaloo");
            });
        }

        [Fact]
        public void Feature_StaticCommand_CustomAwaiter()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_StaticCommand_CustomAwaitable);

                var testButton = browser.First("[data-ui=test]");
                var clearButton = browser.First("[data-ui=clear]");

                clearButton.Click();
                testButton.Click();

                AssertUI.TextEquals(browser.First("[data-ui=result]"), "Test ok");
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Id()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderBy Id");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }

        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Name()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderBy Name");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Category()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderBy Category");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId10.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId6.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_IsActive()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderBy IsActive");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId6.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId10.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Transactions()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderBy Transactions");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_RegisteredAt()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderBy RegisteredAt");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Descending_Id()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderByDescending Id");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId10.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId6.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Descending_Name()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderByDescending Name");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId10.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId6.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Descending_Category()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderByDescending Category");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Descending_IsActive()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderByDescending IsActive");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Descending_Transactions()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderByDescending Transactions");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId6.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId10.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Sort_By_Descending_RegisteredAt()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "OrderByDescending RegisteredAt");
                browser.WaitFor(() => Assert.Equal(10, rows.Count), 500);

                Assert.Equal(rowWithId10.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId6.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Get_customers_By_Color()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "Get all red customers");
                browser.WaitFor(() => Assert.Equal(4, rows.Count), 500);

                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId6.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());

                rows = GetSortedRow(browser, "Get all green customers");
                browser.WaitFor(() => Assert.Equal(2, rows.Count), 500);

                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());

                rows = GetSortedRow(browser, "Get all blue customers");
                browser.WaitFor(() => Assert.Equal(3, rows.Count), 500);

                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Get_Active_Customers()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "Get all active customers");
                browser.WaitFor(() => Assert.Equal(5, rows.Count), 500);

                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Take_Five_Customers()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "Take 5 customers");
                browser.WaitFor(() => Assert.Equal(5, rows.Count), 500);

                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Skip_Five_Customers()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "Skip 5 customers");
                browser.WaitFor(() => Assert.Equal(5, rows.Count), 500);

                Assert.Equal(rowWithId6.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId7.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId9.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId10.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Concat()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "Concat with itself");
                browser.WaitFor(() => Assert.Equal(20, rows.Count), 500);

                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 1, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 2, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 3, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 4, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 10, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId2.ToList(), RowContent(rows, 11, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 12, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId4.ToList(), RowContent(rows, 13, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
                Assert.Equal(rowWithId5.ToList(), RowContent(rows, 14, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_First_Or_Last()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);

                var rows = GetSortedRow(browser, "First customer");
                browser.WaitFor(() => Assert.Single(rows), 500);
                Assert.Equal(rowWithId1.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());

                rows = GetSortedRow(browser, "Last customer");
                browser.WaitFor(() => Assert.Single(rows), 500);
                Assert.Equal(rowWithId10.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());

                rows = GetSortedRow(browser, "First blue customer");
                browser.WaitFor(() => Assert.Single(rows), 500);
                Assert.Equal(rowWithId3.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());

                rows = GetSortedRow(browser, "Last red customer");
                browser.WaitFor(() => Assert.Single(rows), 500);
                Assert.Equal(rowWithId8.ToList(), RowContent(rows, 0, new List<int> { 0, 1, 2, 3, 4, 5 }).ToList());
            });
        }

        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Boolean_Operation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);
                var textbox = browser.First("[data-ui=textbox]");

                var orderByBtn = browser.First($"//input[@value='Are all customers active?']", By.XPath);
                orderByBtn.Click();
                AssertUI.InnerTextEquals(textbox, "false");

                orderByBtn = browser.First($"//input[@value='Are all customer Ids smaller than 20?']", By.XPath);
                orderByBtn.Click();
                AssertUI.InnerTextEquals(textbox, "true");

                orderByBtn = browser.First($"//input[@value='Is there a customer named Greg?']", By.XPath);
                orderByBtn.Click();
                AssertUI.InnerTextEquals(textbox, "false");

                orderByBtn = browser.First($"//input[@value='Is there a customer named Carl?']", By.XPath);
                orderByBtn.Click();
                AssertUI.InnerTextEquals(textbox, "true");
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_Other_Operation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);
                var textbox = browser.First("[data-ui=textbox]");

                var orderByBtn = browser.First($"//input[@value='Get maximum number of finished transactions']", By.XPath);
                orderByBtn.Click();
                AssertUI.InnerTextEquals(textbox, "121");

                orderByBtn = browser.First($"//input[@value='Get minimum number of finished transactions']", By.XPath);
                orderByBtn.Click();
                AssertUI.InnerTextEquals(textbox, "3");
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_List_Contains()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);
                var textbox = browser.First("[data-ui=textbox]");

                browser.First($"//input[@value='Is there green in the list']", By.XPath).Click();
                AssertUI.InnerTextEquals(textbox, "true");
            });
        }
        [Fact]
        public void Feature_Lambda_Expression_Static_Command_List_Sum()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_LambdaExpressions_StaticCommands);
                var textbox = browser.First("[data-ui=textbox]");

                browser.First($"//input[@value='OrderBy Id']", By.XPath).Click();
                browser.First($"//input[@value='Sum of name lengths']", By.XPath).Click();
                AssertUI.InnerTextEquals(textbox, "51");
                browser.First($"//input[@value='Skip 5 customers']", By.XPath).Click();
                browser.First($"//input[@value='Sum of name lengths']", By.XPath).Click();
                AssertUI.InnerTextEquals(textbox, "26");
            });
        }
        [Fact]
        public void Feature_List_Translation_Add_Item()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "Add (11)");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(11, column.Count), 500);
                Assert.Equal(new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" }, column);

                rows = GetSortedRow(browser, "Add (11)");
                column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(12, column.Count), 500);
                Assert.Equal(new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "11" }, column);
            });
        }
        [Fact]
        public void Feature_List_Translation_Add_Or_Update()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "AddOrUpdate");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(10, column.Count), 500);
                Assert.Equal(new List<string> { "1", "2", "3", "4", "54321", "6", "7", "8", "9", "10" }, column);

                rows = GetSortedRow(browser, "AddOrUpdate");
                column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(11, column.Count), 500);
                Assert.Equal(new List<string> { "1", "2", "3", "4", "54321", "6", "7", "8", "9", "10", "12345" }, column);
            });
        }

        [Fact]
        public void Feature_List_Translation_Add_Range()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "AddRange (first five)");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(15, column.Count), 500);
                Assert.Equal(new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "1", "2", "3", "4", "5" }, column);
            });
        }
        [Fact]
        public void Feature_List_Translation_Insert()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "Insert (1,22)");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(11, column.Count), 500);
                Assert.Equal(new List<string> { "1", "22", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, column);
            });
        }
        [Fact]
        public void Feature_List_Translation_Insert_Range()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "InsertRange (first five)");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(15, column.Count), 500);
                Assert.Equal(new List<string> { "1", "1", "2", "3", "4", "5", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, column);
            });
        }
        [Fact]
        public void Feature_List_Translation_Remove()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "RemoveAt (2)");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(9, column.Count), 500);
                Assert.Equal(new List<string> { "1", "2", "4", "5", "6", "7", "8", "9", "10" }, column);
            });
        }
        [Fact]
        public void Feature_List_Translation_Remove_By_Codition()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "RemoveAll (even)");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(5, column.Count), 500);
                Assert.Equal(new List<string> { "1", "3", "5", "7", "9" }, column);
            });
        }
        [Fact]
        public void Feature_List_Translation_Remove_One_By_Codition()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "RemoveFirst (even)");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(9, column.Count), 500);
                Assert.Equal(new List<string> { "1", "3", "4", "5", "6", "7", "8", "9", "10" }, column);

                rows = GetSortedRow(browser, "RemoveLast (even)");
                column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(8, column.Count), 500);
                Assert.Equal(new List<string> { "1", "3", "4", "5", "6", "7", "8", "9" }, column);
            });
        }

        [Fact]
        public void Feature_List_Translation_Remove_Range()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "RemoveRange (2,5)");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => {
                    column = GetColumnContent(SelectRows(browser), 0);
                    Assert.Equal(5, column.Count);
                }, 500);
                Assert.Equal(new List<string> { "1", "2", "8", "9", "10" }, column);
            });
        }
        
        [Fact]
        public void Feature_List_Translation_Remove_Reverse()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);

                var rows = GetSortedRow(browser, "Reverse");
                var column = GetColumnContent(rows, 0);
                browser.WaitFor(() => Assert.Equal(10, column.Count), 500);
                Assert.Equal(new List<string> { "10", "9", "8", "7", "6", "5", "4", "3", "2", "1" }, column);
            });
        }

        [Fact]
        public async Task Feature_ExtensionMethodsNotResolvedOnStartup()
        {
            var client = new HttpClient();

            // try to visit the page
            var pageResponse = await client.GetAsync(TestSuiteRunner.Configuration.BaseUrls[0].TrimEnd('/') + "/" + SamplesRouteUrls.FeatureSamples_JavascriptTranslation_ListMethodTranslations);
            TestOutput.WriteLine($"Page response: {(int)pageResponse.StatusCode}");
            var wasError = pageResponse.StatusCode != HttpStatusCode.OK;

            // dump extension methods on the output
            var json = await client.GetStringAsync(TestSuiteRunner.Configuration.BaseUrls[0].TrimEnd('/') + "/dump-extension-methods");
            TestOutput.WriteLine(json);
            
            if (wasError)
            {
                // fail the test on error
                throw new Exception("Extension methods were not resolved on application startup.");
            }
        }

        protected IElementWrapperCollection<IElementWrapper, IBrowserWrapper> GetSortedRow(IBrowserWrapper browser, string btn)
        {
            var orderByBtn = browser.First($"//input[@value='{btn}']", By.XPath);
            orderByBtn.Click();
            return SelectRows(browser);
        }

        private static IElementWrapperCollection<IElementWrapper, IBrowserWrapper> SelectRows(IBrowserWrapper browser)
        {
            var filteredGrid = browser.First("[data-ui=grid]");
            var rows = filteredGrid.FindElements("tbody tr", By.CssSelector);
            return rows;
        }

        protected List<string> RowContent(IElementWrapperCollection<IElementWrapper, IBrowserWrapper> rows, int trIndex, ICollection<int> cols)
        {
            return RowContent(rows.ElementAt(trIndex), cols);
        }
        protected List<string> RowContent(IElementWrapper row, ICollection<int> cols)
        {
            var cells = row.FindElements("td", By.TagName);

            var content = new List<string>();
            foreach (var col in cols)
            {
                var text = cells.ElementAt(col).GetInnerText();
                text = Regex.Replace(text, "\\s+", " "); // diffrent version of localization libraries can produce different whitespace (space, or no-break space)
                content.Add(text);
            }

            return content;
        }
        public List<string> GetColumnContent(IElementWrapperCollection<IElementWrapper, IBrowserWrapper> rows, int column, int retries = 10)
        {
            // stale element are way too common here :/
            try
            {
                return rows.Select(row => row.FindElements("td").ElementAt(column).GetInnerText()).ToList();
            }
            catch (StaleElementReferenceException) when (retries > 0)
            {
                Thread.Sleep(100);
                return GetColumnContent(rows, column, retries - 1);
            }
        }
    }
}
