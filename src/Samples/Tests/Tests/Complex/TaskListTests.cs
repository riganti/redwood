using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using Riganti.Selenium.Core;
using Riganti.Selenium.DotVVM;
using Xunit;

namespace DotVVM.Samples.Tests.Complex
{
    public class TaskListTests : AppSeleniumTest
    {
        public TaskListTests(Xunit.Abstractions.ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Complex_TaskList_TaskListAsyncCommands()
        {
            RunInAllBrowsers(browser =>
            {
                browser.NavigateToUrl(SamplesRouteUrls.ComplexSamples_TaskList_TaskListAsyncCommands);

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(3);

                //add task
                browser.SendKeys("input[type=text]", "DotVVM");
                browser.ElementAt("input[type=button]",0).Click();
                browser.WaitForPostback();

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(4);

                //mark last task as completed
                browser.Last("a").Click();
                browser.WaitForPostback();

                AssertUI.ClassAttribute(browser.Last(".table tr"), a => a.Contains("completed"), "Last task is not marked as completed.");

                browser.ElementAt("input[type=button]", 1).Click();
                browser.WaitForPostback();
                browser.FindElements(".table tr").ThrowIfDifferentCountThan(5);
            });
        }

        [Fact]
        public void Complex_TaskList_ServerRenderedTaskList()
        {
            RunInAllBrowsers(browser =>
            {
                browser.NavigateToUrl(SamplesRouteUrls.ComplexSamples_TaskList_ServerRenderedTaskList);

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(3);

                //add task
                browser.SendKeys("input[type=text]", "DotVVM");
                browser.Click("input[type=submit]");

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(4);

                //mark last task as completed
                browser.Last("a").Click();

                AssertUI.ClassAttribute(browser.Last(".table tr"), a => a.Contains("completed"),
                    "Last task is not marked as completed.");
            });
        }

        [Fact]
        public void Complex_TaskList_TaskListAsyncCommands_ViewModelRestore()
        {
            // view model should be restored after back/forward navigation, but not on refresh
            RunInAllBrowsers(browser =>
            {
                browser.NavigateToUrl("/");
                browser.NavigateToUrl(SamplesRouteUrls.ComplexSamples_TaskList_TaskListAsyncCommands);

                browser.SendKeys("input[type=text]", "test1");
                browser.Click("input[type=button]");

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(4);

                browser.NavigateBack();
                browser.WaitUntilDotvvmInited();
                browser.NavigateForward();

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(4);

                browser.Refresh();

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(3);

                browser.SendKeys("input[type=text]", "test2");
                browser.Click("input[type=button]");
                browser.FindElements(".table tr").ThrowIfDifferentCountThan(4);

                browser.NavigateToUrl("/");
                browser.NavigateToUrl(SamplesRouteUrls.ComplexSamples_TaskList_TaskListAsyncCommands);

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(3);

                browser.NavigateBack();
                browser.WaitUntilDotvvmInited();
                browser.NavigateBack();

                browser.FindElements(".table tr").ThrowIfDifferentCountThan(4);
            });
        }
    }
}
