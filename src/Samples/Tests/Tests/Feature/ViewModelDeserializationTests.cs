using System.Text.RegularExpressions;
using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using Riganti.Selenium.Core;
using Xunit;
using Xunit.Abstractions;

namespace DotVVM.Samples.Tests.Feature
{
    public class ViewModelDeserializationTests : AppSeleniumTest
    {
        [Fact]
        public void Feature_ViewModelDeserialization_DoesNotDropObject()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_ViewModelDeserialization_DoesNotDropObject);

                AssertUI.InnerTextEquals(browser.First("span"), "0");
                //value++
                browser.ElementAt("input[type=button]", 2).Click();
                browser.ElementAt("input[type=button]", 2).Click();
                //check value
                AssertUI.InnerTextEquals(browser.First("span"), "2");
                //hide span
                browser.ElementAt("input[type=button]", 0).Click();
                //show span
                browser.ElementAt("input[type=button]", 1).Click();
                //value++
                browser.ElementAt("input[type=button]", 2).Click();
                //check value
                AssertUI.InnerTextEquals(browser.First("span"), "3");
            });
        }

        [Fact]
        public void Feature_ViewModelDeserialization_NegativeLongNumber()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_ViewModelDeserialization_NegativeLongNumber);

                var postback = browser.Single("[data-ui=decrement-postback]");
                var longNumber = browser.Single("[data-ui=long-number]");

                AssertUI.InnerTextEquals(longNumber, "0");

                // value--
                postback.Click();
                AssertUI.InnerTextEquals(longNumber, "-1");

                // value--
                postback.Click();
                AssertUI.InnerTextEquals(longNumber, "-2");
            });
        }

        // different versions of localization libraries may produce different whitespace (no space before AM/PM, no-break spaces, ...)
        static bool EqualsIgnoreSpace(string a, string b) => Regex.Replace(a, @"\s+", "") == Regex.Replace(b, @"\s+", "");


        [Fact]
        public void Feature_ViewModelDeserialization_PropertyNullAssignment()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_ViewModelDeserialization_PropertyNullAssignment);

                var value = browser.Single(".result");
                var buttons = browser.FindElements("input[type=button]");

                AssertUI.InnerTextEquals(value, "");

                buttons[0].Click();
                AssertUI.InnerText(value, t => EqualsIgnoreSpace(t, "1/2/2023 3:04:05 AM"));

                buttons[1].Click();
                AssertUI.InnerTextEquals(value, "");

                buttons[0].Click();
                AssertUI.InnerText(value, t => EqualsIgnoreSpace(t, "1/2/2023 3:04:05 AM"));

                buttons[2].Click();
                AssertUI.InnerTextEquals(value, "");
            });
        }

        public ViewModelDeserializationTests(ITestOutputHelper output) : base(output)
        {
        }
    }
}
