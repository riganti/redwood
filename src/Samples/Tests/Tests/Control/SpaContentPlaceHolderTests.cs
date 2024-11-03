using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using Riganti.Selenium.Core;
using Riganti.Selenium.Core.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotVVM.Samples.Tests.Control
{
    public class SpaContentPlaceHolderTests : AppSeleniumTest
    {
        [Fact]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_Default))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB))]
        public void Control_SpaContentPlaceHolder_SpaContentPlaceHolder_HistoryApi()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_Default);

                // verify the URL after redirect to the DefaultRoute
                AssertUI.AlertTextEquals(browser, "javascript 2 resource loaded!");
                browser.ConfirmAlert();

                // navigate to SPA using link
                browser.ElementAt("a", 1).Click();

                AssertUI.AlertTextEquals(browser, "javascript resource loaded!");
                browser.ConfirmAlert();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB);

                // go to first page
                browser.ElementAt("a", 0).Click();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA + "/16");

                // test first page
                AssertUI.TextEquals(browser.ElementAt("span", 0), "0");
                browser.Click("input[type=button]");
                browser.Click("input[type=button]");
                browser.Click("input[type=button]");
                AssertUI.TextEquals(browser.ElementAt("span", 0), "3");

                // go to second page
                browser.FindElements("a").Single(l => l.GetText() == "Go to Task List").Click();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB);

                // try the task list
                browser.WaitFor(()=> browser.FindElements(".table tr").ThrowIfDifferentCountThan(3),1000);
                browser.SendKeys("input[type=text]", "DotVVM rocks!");
                browser.Click("input[type=button]");
                browser.WaitFor(() => browser.FindElements(".table tr").ThrowIfDifferentCountThan(4), 1000);
                browser.Last("tr a").Click();
                AssertUI.HasClass(browser.Last(".table tr"), "completed");

                // test the browse back button
                browser.NavigateBack();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA + "/16");

                // test first page
                AssertUI.TextEquals(browser.ElementAt("span", 0), "0");
                browser.Click("input[type=button]");
                browser.Click("input[type=button]");
                browser.Click("input[type=button]");
                AssertUI.TextEquals(browser.ElementAt("span", 0), "3");

                // test the forward button
                browser.NavigateForward();
                browser.FindElements(".table tr").ThrowIfDifferentCountThan(3);

                // test the redirect inside SPA
                browser.First(".navigation input[type=button]").Click();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA + "/15");

                // test the redirect outside SPA
                AssertUI.TextEquals(browser.ElementAt("span", 0), "0");
                browser.FindElements("a").Single(l => l.GetText().Contains("Exit SPA")).Click();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ComplexSamples_TaskList_ServerRenderedTaskList);
            });
        }

        [Fact]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_Default))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB))]
        public void Control_SpaContentPlaceHolder_SpaContentPlaceHolder_HistoryApi_RedirectFromOldUrl()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB + "#!/" + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA + "/16");

                // verify the URL after redirect to the desired page
                browser.WaitFor(() => {
                    AssertUI.AlertTextEquals(browser, "javascript resource loaded!");
                    browser.ConfirmAlert();
                }, 5000);

                browser.WaitFor(() => {
                    AssertUI.AlertTextEquals(browser, "javascript 2 resource loaded!");
                    browser.ConfirmAlert();
                }, 5000);

                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA + "/16");
            });
        }

        [Fact]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_Default))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB))]
        public void Control_SpaContentPlaceHolder_SpaContentPlaceHolder_HistoryApi_EnteredFromPageB()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB);
                browser.WaitFor(browser.HasAlert, 10000);

                // verify the URL after redirect to the DefaultRoute
                AssertUI.AlertTextEquals(browser, "javascript resource loaded!");
                browser.ConfirmAlert();

                browser.WaitFor(browser.HasAlert, 10000);
                AssertUI.AlertTextEquals(browser, "javascript 2 resource loaded!");
                browser.ConfirmAlert();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB);

                // go to first page
                browser.First("a").Click();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA + "/16");

                // test first page
                AssertUI.TextEquals(browser.ElementAt("span", 0), "0");
                browser.Click("input[type=button]");
                browser.Click("input[type=button]");
                browser.Click("input[type=button]");
                AssertUI.TextEquals(browser.ElementAt("span", 0), "3");

                // go to second page
                browser.FindElements("a").Single(l => l.GetText() == "Go to Task List").Click();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB);

                // try the task list
                browser.WaitFor(() => browser.FindElements(".table tr").ThrowIfDifferentCountThan(3),1000);
                browser.SendKeys("input[type=text]", "DotVVM rocks!");
                browser.Click("input[type=button]");
                browser.WaitFor(()=>browser.FindElements(".table tr").ThrowIfDifferentCountThan(4),1000);
                browser.Last("tr a").Click();
                AssertUI.HasClass(browser.Last(".table tr"), "completed");

                // test the browse back button
                browser.NavigateBack();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA + "/16");

                // test first page
                AssertUI.TextEquals(browser.ElementAt("span", 0), "0");
                browser.Click("input[type=button]");
                browser.Click("input[type=button]");
                browser.Click("input[type=button]");
                AssertUI.TextEquals(browser.ElementAt("span", 0), "3");

                // test the forward button
                browser.NavigateForward();
                browser.FindElements(".table tr").ThrowIfDifferentCountThan(3);

                // test the redirect inside SPA
                browser.First(".navigation input[type=button]").Click();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA + "/15");

                // test the redirect outside SPA
                AssertUI.TextEquals(browser.ElementAt("span", 0), "0");
                browser.FindElements("a").Single(l => l.GetText().Contains("Exit SPA")).Click();
                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ComplexSamples_TaskList_ServerRenderedTaskList);
            });
        }

        [Fact]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_Default))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB))]
        public void Control_SpaContentPlaceHolder_SpaContentPlaceHolder_HistoryApi_EnteredFromPageB_RedirectFromOldUrl()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_Default + "#!/" + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB);

                // verify the URL after redirect to the desired page
                browser.WaitFor(() => {
                    AssertUI.AlertTextEquals(browser, "javascript 2 resource loaded!");
                    browser.ConfirmAlert();
                }, 5000);

                browser.WaitFor(() => {
                    AssertUI.AlertTextEquals(browser, "javascript resource loaded!");
                    browser.ConfirmAlert();
                }, 5000);

                AssertUI.UrlEquals(browser, browser.BaseUrl + SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_PageB);
            });
        }

        [Fact]
        public void Control_SpaContentPlaceHolder_SpaContentPlaceHolder_HistoryApi_MultipleSpas()
        {
            void CheckOnlyOneSpaRouteLink(IBrowserWrapper browser)
                => Xunit.Assert.StrictEqual(1, browser.FindElements("a").Where(elem => elem.GetText().Contains(": Go to ")).Count());

            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_SpaContentPlaceHolder_HistoryApi_MultiSpaDefault);

                var defaultUrl = browser.CurrentUrl;
                var baseUrl = defaultUrl.Substring(0, defaultUrl.LastIndexOf('/'));
                var routeLinks = browser.FindElements("a");

                routeLinks.First().Click();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa1PageA");
                CheckOnlyOneSpaRouteLink(browser);
                browser.FindElements("a").Single(elem => elem.GetText() == "SPA1: Go to Second Page");

                routeLinks.Skip(1).First().Click();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa1PageB");
                CheckOnlyOneSpaRouteLink(browser);
                browser.FindElements("a").Single(elem => elem.GetText() == "SPA1: Go to First Page");

                routeLinks.Skip(2).First().Click();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa2PageA");
                CheckOnlyOneSpaRouteLink(browser);
                browser.FindElements("a").Single(elem => elem.GetText() == "SPA2: Go to Second Page");

                routeLinks.Skip(3).First().Click();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa2PageB");
                CheckOnlyOneSpaRouteLink(browser);
                browser.FindElements("a").Single(elem => elem.GetText().Contains("SPA2: Go to First Page"));

                routeLinks.Skip(4).First().Click();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa1Spa2Page");
                browser.FindElements("span").Single(elem => elem.GetText() == "SPA1: Hello World!");
                browser.FindElements("span").Single(elem => elem.GetText() == "SPA2: Hello World!");

                browser.NavigateBack();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa2PageB");
                browser.NavigateBack();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa2PageA");
                browser.NavigateBack();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa1PageB");
                browser.NavigateBack();
                AssertUI.UrlEquals(browser, $"{baseUrl}/Spa1PageA");
                browser.NavigateBack();
                AssertUI.UrlEquals(browser, defaultUrl);
            });
        }

        public SpaContentPlaceHolderTests(ITestOutputHelper output) : base(output)
        {
        }
    }
}
