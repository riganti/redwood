﻿using System.Linq;
using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using OpenQA.Selenium;
using Riganti.Selenium.Core;
using Riganti.Selenium.Core.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotVVM.Samples.Tests.Feature
{
    public class DataSetTests : AppSeleniumTest
    {
        public DataSetTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData(SamplesRouteUrls.FeatureSamples_DataSet_GitHubApi)]
        [InlineData(SamplesRouteUrls.FeatureSamples_DataSet_GitHubApiStaticCommands)]
        public void Feature_DataSet_GitHubApi_Next(string url)
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(url);
                VerifyPageIsNotThrowingAuthError(browser);

                var grid = browser.Single("next-grid", SelectByDataUi);
                var pager = browser.Single("next-pager", SelectByDataUi);

                // get first issue on the first page
                var issueId = grid.ElementAt("tbody tr td", 0).GetInnerText();

                // go next
                pager.ElementAt("li", 1).Single("a").Click().Wait(500);
                AssertUI.TextNotEquals(grid.ElementAt("tbody tr td", 0), issueId);

                // go to first page
                pager.ElementAt("li", 0).Single("a").Click().Wait(500);
                AssertUI.TextEquals(grid.ElementAt("tbody tr td", 0), issueId);
            });
        }

        [Theory]
        [InlineData(SamplesRouteUrls.FeatureSamples_DataSet_GitHubApi)]
        [InlineData(SamplesRouteUrls.FeatureSamples_DataSet_GitHubApiStaticCommands)]
        public void Feature_DataSet_GitHubApi_NextHistory(string url)
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(url);
                VerifyPageIsNotThrowingAuthError(browser);

                var grid = browser.Single("next-history-grid", SelectByDataUi);
                var pager = browser.Single("next-history-pager", SelectByDataUi);

                // get first issue on the first page
                var issueId1 = grid.ElementAt("tbody tr td", 0).GetInnerText();

                // go to page 2
                pager.ElementAt("li", 3).Single("a").Click().Wait(500);
                AssertUI.TextNotEquals(grid.ElementAt("tbody tr td", 0), issueId1);
                var issueId2 = grid.ElementAt("tbody tr td", 0).GetInnerText();

                // go to next page
                pager.ElementAt("li", 5).Single("a").Click().Wait(500);
                AssertUI.TextNotEquals(grid.ElementAt("tbody tr td", 0), issueId1);
                AssertUI.TextNotEquals(grid.ElementAt("tbody tr td", 0), issueId2);
                var issueId3 = grid.ElementAt("tbody tr td", 0).GetInnerText();

                // go to first page
                pager.ElementAt("li", 0).Single("a").Click().Wait(500);
                AssertUI.TextEquals(grid.ElementAt("tbody tr td", 0), issueId1);

                // go to page 4
                pager.ElementAt("li", 5).Single("a").Click().Wait(500);
                AssertUI.TextNotEquals(grid.ElementAt("tbody tr td", 0), issueId1);
                AssertUI.TextNotEquals(grid.ElementAt("tbody tr td", 0), issueId2);
                AssertUI.TextNotEquals(grid.ElementAt("tbody tr td", 0), issueId3);

                // go to previous page
                pager.ElementAt("li", 1).Single("a").Click().Wait(500);
                AssertUI.TextEquals(grid.ElementAt("tbody tr td", 0), issueId3);
            });
        }
        private void VerifyPageIsNotThrowingAuthError(IBrowserWrapper browser)
        {
            var elms = browser.FindElements(".exceptionMessage", By.CssSelector);
            var errorMessage = elms.FirstOrDefault();

            if (errorMessage is null) return;
            AssertUI.Text(errorMessage, e => !e.Contains("401"), "GitHub Authentication Failed!",
            waitForOptions: WaitForOptions.FromTimeout(1000));
        }
    }
}
