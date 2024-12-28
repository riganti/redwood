using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using OpenQA.Selenium.Chrome;
using Riganti.Selenium.Core;
using Xunit;
using Xunit.Abstractions;

namespace DotVVM.Samples.Tests.Feature
{
    public class DateTimeTranslationTests : AppSeleniumTest
    {
        // different versions of localization libraries may produce different whitespace (no space before AM/PM, no-break spaces, ...)
        static bool EqualsIgnoreSpace(string a, string b) => Regex.Replace(a, @"\s+", "") == Regex.Replace(b, @"\s+", "");

        [Fact]
        public void Feature_DateTime_PropertyTranslations()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_DateTimeTranslations);

                var stringDateTime = "6/28/2021 3:28:31 PM";
                var localDateTime = "6/28/2021 5:28:31 PM";    // the offset is hard-coded to 120 minutes in the test
                var localDateTime2 = "5/10/2020 12:12:34 AM";
                var stringDateTime2 = "5/9/2020 10:12:34 PM";

                var textbox = browser.Single("input[data-ui=textbox]");
                textbox.Clear().SendKeys(stringDateTime).SendEnterKey();

                var spanYear = browser.Single("span[data-ui=year]");
                AssertUI.TextEquals(spanYear, "2021");
                var spanMonth = browser.Single("span[data-ui=month]");
                AssertUI.TextEquals(spanMonth, "6");
                var spanDay = browser.Single("span[data-ui=day]");
                AssertUI.TextEquals(spanDay, "28");
                var spanHour = browser.Single("span[data-ui=hour]");
                AssertUI.TextEquals(spanHour, "15");
                var spanMinute = browser.Single("span[data-ui=minute]");
                AssertUI.TextEquals(spanMinute, "28");
                var spanSecond = browser.Single("span[data-ui=second]");
                AssertUI.TextEquals(spanSecond, "31");
                var spanMillisecond = browser.Single("span[data-ui=millisecond]");
                AssertUI.TextEquals(spanMillisecond, "0");

                // try the conversion
                var localTextBox = browser.Single("input[data-ui=toBrowserLocalTime]");
                AssertUI.Text(localTextBox, t => EqualsIgnoreSpace(t, localDateTime));

                localTextBox.Clear().SendKeys(localDateTime2).SendEnterKey();
                AssertUI.Text(textbox, t => EqualsIgnoreSpace(t, stringDateTime2));

                // try the conversion on nullable
                var localTextBoxNullable = browser.Single("input[data-ui=toBrowserLocalTimeOnNullable]");
                var spanNullable = browser.Single("span[data-ui=toBrowserLocalTimeOnNullable]");
                AssertUI.TextEquals(localTextBoxNullable, "");

                localTextBoxNullable.Clear().SendKeys(localDateTime2).SendEnterKey();
                AssertUI.Text(spanNullable, t => EqualsIgnoreSpace(t, stringDateTime2));

                // try the null propagation
                var localTextBoxNullPropagation = browser.Single("input[data-ui=toBrowserLocalTimeNullPropagation]");
                var spanNullPropagation = browser.Single("span[data-ui=toBrowserLocalTimeNullPropagation]");
                AssertUI.TextEquals(localTextBoxNullPropagation, "");

                localTextBoxNullable.Clear().SendKeys(localDateTime2).SendEnterKey();
                AssertUI.TextEquals(spanNullPropagation, "");
            });
        }

        [Fact]
        public void Feature_DateOnly_PropertyTranslations()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_DateOnlyTranslations);

                var stringDateTime = "6/28/2021 3:28:31 PM";

                var textbox = browser.Single("input[data-ui=textbox]");
                textbox.Clear().SendKeys(stringDateTime).SendEnterKey();

                var str = browser.Single("span[data-ui=dateOnlyToString]");
                AssertUI.TextEquals(str, "Monday, June 28, 2021");
                var props = browser.Single("span[data-ui=dateOnlyProperties]");
                AssertUI.TextEquals(props, "28. 6. 2021");
            });
        }

        [Fact]
        public void Feature_TimeOnly_PropertyTranslations()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_JavascriptTranslation_TimeOnlyTranslations);

                var stringDateTime = "6/28/2021 3:28:31 PM";

                var textbox = browser.Single("input[data-ui=textbox]");
                textbox.Clear().SendKeys(stringDateTime).SendEnterKey();

                var str = browser.Single("span[data-ui=timeOnlyToString]");
                AssertUI.Text(str, t => EqualsIgnoreSpace(t, "3:28:31 PM"));
                var props = browser.Single("span[data-ui=timeOnlyProperties]");
                AssertUI.TextEquals(props, "15 hours 28 minues 31 seconds and 0 milliseconds");
            });
        }


        public DateTimeTranslationTests(ITestOutputHelper output) : base(output)
        {
        }
    }
}
