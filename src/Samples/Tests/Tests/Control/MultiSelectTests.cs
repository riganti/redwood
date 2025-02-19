using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using Riganti.Selenium.Core;
using Xunit;
using Xunit.Abstractions;

namespace DotVVM.Samples.Tests.Control
{
    public class MultiSelectTests : AppSeleniumTest
    {
        public MultiSelectTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Control_MultiSelect_Bound()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_MultiSelect_binded);

                var multiselect = browser.First("binded-multiselect", SelectByDataUi);
                var selectedValues = browser.First("selected-values", SelectByDataUi);

                AssertUI.IsDisplayed(multiselect.Select(0));
                AssertUI.InnerTextEquals(selectedValues, "Praha");

                // select second option from combobox
                multiselect.Select(1);
                AssertUI.InnerTextEquals(selectedValues, "Praha Brno");

                // select third option from combobox
                multiselect.Select(2);
                AssertUI.InnerTextEquals(selectedValues, "Praha Brno Napajedla");

                // select third option from combobox
                multiselect.Children[0].Click();
                multiselect.Children[1].Click();
                AssertUI.InnerTextEquals(selectedValues, "Napajedla");

                // select first two options
                multiselect.Children[0].Click();
                multiselect.Children[1].Click();
                multiselect.Children[2].Click();
                AssertUI.InnerTextEquals(selectedValues, "Praha Brno");

                // change selection from the server
                browser.First("change-from-server", SelectByDataUi).Click();
                AssertUI.IsNotSelected(multiselect.Children[0]);
                AssertUI.IsSelected(multiselect.Children[1]);
                AssertUI.IsSelected(multiselect.Children[2]);
                AssertUI.InnerTextEquals(selectedValues, "Brno Napajedla");
            });
        }

        [Fact]
        public void Control_MultiSelect_Hardcoded()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_MultiSelect_hardcoded);

                var multiselect = browser.First("hardcoded-multiselect", SelectByDataUi);
                var selectedValues = browser.First("selected-values", SelectByDataUi);

                AssertUI.IsDisplayed(multiselect.Select(0));
                AssertUI.InnerTextEquals(selectedValues, "1");

                // select second option from combobox
                multiselect.Select(1);
                AssertUI.InnerTextEquals(selectedValues, "1 2");

                // select third option from combobox
                multiselect.Select(2);
                AssertUI.InnerTextEquals(selectedValues, "1 2 3");

                // select third option from combobox
                multiselect.Children[0].Click();
                multiselect.Children[1].Click();
                AssertUI.InnerTextEquals(selectedValues, "3");

                // select first two options
                multiselect.Children[0].Click();
                multiselect.Children[1].Click();
                multiselect.Children[2].Click();
                AssertUI.InnerTextEquals(selectedValues, "1 2");

                // change selection from the server
                browser.First("change-from-server", SelectByDataUi).Click();
                AssertUI.IsNotSelected(multiselect.Children[0]);
                AssertUI.IsSelected(multiselect.Children[1]);
                AssertUI.IsSelected(multiselect.Children[2]);
                AssertUI.InnerTextEquals(selectedValues, "2 3");
            });
        }
    }
}
