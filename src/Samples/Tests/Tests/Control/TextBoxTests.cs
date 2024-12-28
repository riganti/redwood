using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using Riganti.Selenium.Core;
using Riganti.Selenium.Core.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotVVM.Samples.Tests.Control
{
    public class TextBoxTests : AppSeleniumTest
    {
        [Fact]
        public void Control_TextBox_TextBox_FormatDoubleProperty()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_TextBox_TextBox_FormatDoubleProperty);

                AssertUI.TextEquals(browser.Single("[data-ui='textBox']"), "0.00");
                browser.Single("[data-ui='button']").Click();

                AssertUI.TextEquals(browser.Single("[data-ui='textBox']"), "10.50");
            });
        }

        [Fact]
        public void Control_TextBox_IntBoundTextBox()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_TextBox_IntBoundTextBox);

                browser.ElementAt("input", 0).Clear();
                browser.ElementAt("input", 0).SendKeys("hello");
                browser.ElementAt("input[type=button]", 0).Click();

                AssertUI.Value(browser.ElementAt("input", 0), "hello");
                AssertUI.InnerTextEquals(browser.ElementAt("span", 0), "0");
                AssertUI.TextNotEmpty(browser.FirstOrDefault("#ValidatorMessage"));
            });
        }

        [Fact]
        public void Control_TextBox_SimpleDateBox()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_TextBox_SimpleDateBox);

                var now = DateTime.Now;

                var typeText = browser.Single("[data-ui='type-text']").GetText();
                var typeTextDateTime = DateTime.Parse(typeText, DateTimeFormatInfo.InvariantInfo);
                Assert.Equal(now.ToShortDateString(), typeTextDateTime.ToShortDateString());

                var customFormat = browser.Single("[data-ui='custom-format']").GetText();
                Assert.Equal(customFormat, now.ToString("dd-MM-yy"));

                browser.Single("[data-ui='fill-name-button']").Click();
                AssertUI.TextEquals(browser.Single("[data-ui='name-of-day']"), now.DayOfWeek.ToString());
            });
        }

        [Fact]
        public void Control_TextBox_TextBox()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_TextBox_TextBox);

                AssertUI.TagName(browser.First("#TextBox1"), "input");
                AssertUI.TagName(browser.First("#TextBox2"), "input");
                AssertUI.TagName(browser.First("#TextArea1"), "textarea");
                AssertUI.TagName(browser.First("#TextArea2"), "textarea");
            });
        }

        [Fact]
        public void Control_TextBox_SelectAllOnFocus()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_TextBox_SelectAllOnFocus);

                // window.getSelection() doesn't work in Firefox due to this bug https://bugzilla.mozilla.org/show_bug.cgi?id=85686
                // so custom implementation of getSelection is provided
                // by https://stackoverflow.com/a/20427804
                browser.GetJavaScriptExecutor().ExecuteScript(@"
window.getSelectionText = function (dataui) {
    if (window.getSelection) {
        try {
            var ta = document.querySelector('[data-ui=' + dataui + ']');
            return ta.value.substring(ta.selectionStart, ta.selectionEnd);
        } catch (e) {
            console.log('Cant get selection text')
        }
    }
    // For IE
    if (document.selection && document.selection.type != 'Control') {
        return document.selection.createRange().text;
            }
}");

                CheckSelectAllOnFocus(browser, "hardcoded");
                CheckSelectAllOnFocus(browser, "bound", false);
                browser.Single("button", this.SelectByDataUi).Click();
                CheckSelectAllOnFocus(browser, "bound", true);
            });
        }

        private void CheckSelectAllOnFocus(IBrowserWrapper browser, string textBoxDataUi, bool isSelectAllOnFocusTrue = true)
        {
            var textBox = browser.Single(textBoxDataUi, SelectByDataUi);
            textBox.Click();
            var selectedText = (string)browser.GetJavaScriptExecutor().ExecuteScript($"return window.getSelectionText('{textBoxDataUi}');");
            var expectedText = isSelectAllOnFocusTrue ? "Testing text" : "";
            Assert.Equal(expectedText, selectedText);
        }

        public static IEnumerable<object[]> TextBoxStringFormatChangedCommandData =>
            new object[][]
            {
                new object[] { "cs-CZ", SamplesRouteUrls.ControlSamples_TextBox_TextBox_Format_Binding, "#czech"},
                new object[] { "en-US", SamplesRouteUrls.ControlSamples_TextBox_TextBox_Format_Binding, "#english" },
                new object[] { "cs-CZ", SamplesRouteUrls.ControlSamples_TextBox_TextBox_Format, "#czech"},
                new object[] { "en-US", SamplesRouteUrls.ControlSamples_TextBox_TextBox_Format, "#english"},
            };
        
        // different versions of localization libraries may produce different whitespace (no space before AM/PM, no-break spaces, ...)
        static bool EqualsIgnoreSpace(string a, string b) => Regex.Replace(a, @"\s+", "") == Regex.Replace(b, @"\s+", "");

        [Theory]
        [MemberData(nameof(TextBoxStringFormatChangedCommandData))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_TextBox_TextBox_Format))]
        [SampleReference(nameof(SamplesRouteUrls.ControlSamples_TextBox_TextBox_Format_Binding))]
        public void Control_TextBox_StringFormat(string cultureName, string url, string linkSelector)
        {
            RunInAllBrowsers(browser => {
                var culture = new CultureInfo(cultureName);
                browser.NavigateToUrl(url);
                browser.First(linkSelector).Click();

                var dateResult1 = browser.First("#date-result1").GetText();
                var dateResult2 = browser.First("#date-result2").GetText();
                var dateResult3 = browser.First("#date-result3").GetText();

                var dateTextBox = browser.First("#dateTextbox");
                AssertUI.Attribute(dateTextBox, "value", dateResult1);

                var dateText = browser.First("#DateValueText");
                AssertUI.InnerText(dateText, t => EqualsIgnoreSpace(t, new DateTime(2015, 12, 27).ToString("G", culture)));

                var nullableDateTextBox = browser.First("#nullableDateTextbox");
                AssertUI.Attribute(nullableDateTextBox, "value", t => EqualsIgnoreSpace(t, new DateTime(2015, 12, 27).ToString("G", culture)));

                var nullableDateText = browser.First("#nullableDateValueText");
                AssertUI.InnerText(nullableDateText, t => EqualsIgnoreSpace(t, new DateTime(2015, 12, 27).ToString("G", culture)));

                var numberTextbox = browser.First("#numberTextbox");
                AssertUI.Attribute(numberTextbox, "value", 123.1235.ToString(culture));

                var numberValueText = browser.First("#numberValueText");
                AssertUI.InnerTextEquals(numberValueText, 123.123456789.ToString(culture));

                var nullableNumberTextbox = browser.First("#nullableNumberTextbox");
                AssertUI.Attribute(nullableNumberTextbox, "value", 123.123456789.ToString(culture));

                var nullableNumberValueText = browser.First("#nullableNumberValueText");
                AssertUI.InnerTextEquals(nullableNumberValueText, 123.123456789.ToString(culture));

                //write new valid values
                dateTextBox.Clear().SendKeys(dateResult2);
                numberTextbox.Clear().SendKeys(2000.ToString("n0", culture));
                dateTextBox.Click();

                //check new values
                AssertUI.InnerText(dateText, t => EqualsIgnoreSpace(t, new DateTime(2018, 12, 27).ToString("G", culture)));
                AssertUI.InnerTextEquals(numberValueText, 2000.ToString(culture));

                AssertUI.Attribute(numberTextbox, "value", 2000.ToString("n4", culture));
                AssertUI.Attribute(dateTextBox, "value", dateResult2);

                //write invalid values
                dateTextBox.Clear().SendKeys("dsasdasd");
                numberTextbox.Clear().SendKeys("000//a");
                dateTextBox.Click();

                //check displayed values (behavior change in 3.0 - previous values should stay there)
                AssertUI.InnerText(dateText, t => EqualsIgnoreSpace(t, new DateTime(2018, 12, 27).ToString("G", culture)));
                AssertUI.InnerTextEquals(numberValueText, 2000.ToString(culture));

                AssertUI.Attribute(numberTextbox, "value", "000//a");
                AssertUI.Attribute(dateTextBox, "value", "dsasdasd");

                //write new valid values
                dateTextBox.Clear().SendKeys(new DateTime(2018, 1, 1).ToString("d", culture));
                numberTextbox.Clear().SendKeys(1000.550277.ToString(culture));
                dateTextBox.Click();

                //check new values
                AssertUI.InnerText(dateText, t => EqualsIgnoreSpace(t, new DateTime(2018, 1, 1).ToString("G", culture)));
                AssertUI.InnerTextEquals(numberValueText, 1000.550277.ToString(culture));

                AssertUI.Attribute(numberTextbox, "value", 1000.550277.ToString("n4", culture));
                AssertUI.Attribute(dateTextBox, "value", dateResult3);

                // try to supply different date formats
                dateTextBox.Clear().SendKeys(cultureName switch { "en-US" => "2/16/2020 12:00:00 AM", "cs-CZ" => "16.02.2020 0:00:00", _ => "" }).SendKeys(Keys.Tab);
                AssertUI.Attribute(dateTextBox, "value", t => EqualsIgnoreSpace(t, new DateTime(2020, 2, 16).ToString("d", culture)));
                AssertUI.InnerText(dateText, t => EqualsIgnoreSpace(t, new DateTime(2020, 2, 16).ToString("G", culture)));

                nullableDateTextBox.Clear().SendKeys(new DateTime(2020, 4, 2).ToString("d", culture)).SendKeys(Keys.Tab);
                AssertUI.Attribute(nullableDateTextBox, "value", t => EqualsIgnoreSpace(t, new DateTime(2020, 4, 2).ToString("G", culture)));
                AssertUI.InnerText(nullableDateText, t => EqualsIgnoreSpace(t, new DateTime(2020, 4, 2).ToString("G", culture)));
            });
        }

        [Theory]
        [MemberData(nameof(TextBoxStringFormatChangedCommandData))]
        private void Control_TextBox_StringFormat_ChangedCommandBinding(string cultureName, string url, string linkSelector)
        {
            RunInAllBrowsers(browser => {
                void ClearInput(IElementWrapper element)
                {
                    // There is special threatment for TextBox with Changed Command
                    // When Clear() method is used, changed command is invoked and default value '0.00' appear
                    while (element.GetText() != "")
                    {
                        element.WebElement.SendKeys(Keys.Backspace);
                    }
                }

                // Set focus to different element to drop focus on input and invoke onchange element (for IE)
                void LoseFocus() => browser.Single("body").SetFocus();

                var culture = new CultureInfo(cultureName);
                browser.NavigateToUrl(url);
                browser.First(linkSelector).Click();

                IElementWrapper numberTextbox = null;
                IElementWrapper numberValueText = null;
                numberTextbox = browser.First("#bindingNumberFormatTextbox");
                Func<string> referenceFormat = () => browser.First("#bindingNumberValueNString").GetText().Trim();
                AssertUI.Attribute(numberTextbox, "value", referenceFormat());

                numberValueText = browser.First("#resultNumberValueText");
                AssertUI.InnerTextEquals(numberValueText, 0.ToString(culture));

                // send new values
                ClearInput(numberTextbox);
                numberTextbox.SendKeys("42")
                    .SendEnterKey();
                LoseFocus();

                // check new values
                AssertUI.InnerTextEquals(numberValueText, 42.ToString(culture));
                AssertUI.Attribute(numberTextbox, "value", referenceFormat());

                // send new values
                ClearInput(numberTextbox);
                numberTextbox.SendKeys(123.456789.ToString(culture))
                    .SendEnterKey();
                LoseFocus();

                // check new values
                AssertUI.InnerTextEquals(numberValueText, 123.456789.ToString(culture));
                AssertUI.Attribute(numberTextbox, "value", referenceFormat());
            });
        }

        [Theory]
        [InlineData("cs-CZ")]
        [InlineData("en-US")]
        public void Control_TextBox_TextBox_Types(string localizationId)
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.ControlSamples_TextBox_TextBox_Types + "?lang=" + localizationId);

                AssertUI.Value(browser.Single("input[data-ui='date-textbox']"), "2017-01-01");
                AssertUI.Value(browser.Single("input[data-ui='nullable-date-textbox']"), "2017-01-01");

                AssertUI.Value(browser.Single("input[data-ui='number-textbox']"), "42.42");
                AssertUI.Value(browser.Single("input[data-ui='nullable-number-textbox']"), "42.42");

                AssertUI.Value(browser.Single("input[data-ui='time-textbox']"), "08:08");
                AssertUI.Value(browser.Single("input[data-ui='nullable-time-textbox']"), "20:10");

                AssertUI.Value(browser.Single("input[data-ui='month-textbox']"), "2017-01");
                AssertUI.Value(browser.Single("input[data-ui='nullable-month-textbox']"), "2017-01");

                AssertUI.Value(browser.Single("input[data-ui='datetime-textbox']"), "2017-01-01T08:08");
                AssertUI.Value(browser.Single("input[data-ui='nullable-datetime-textbox']"), "2017-01-01T20:10");

                var intTextBox = browser.Single("input[data-ui='int-textbox']");
                AssertUI.Value(intTextBox, "0");
                intTextBox.SetFocus();
                intTextBox.SendKeys(Keys.ArrowUp);
                AssertUI.Value(intTextBox, "1");
                intTextBox.SendKeys(Keys.ArrowDown);
                AssertUI.Value(intTextBox, "0");
                intTextBox.SendKeys(Keys.ArrowDown);
                AssertUI.Value(intTextBox, "-1");

                var nullableIntTextBox = browser.Single("input[data-ui='nullable-int-textbox']");
                AssertUI.Value(nullableIntTextBox, "");
                nullableIntTextBox.SetFocus();
                nullableIntTextBox.SendKeys(Keys.ArrowUp);
                AssertUI.Value(nullableIntTextBox, "1");
                nullableIntTextBox.SendKeys(Keys.ArrowDown);
                AssertUI.Value(nullableIntTextBox, "0");
                nullableIntTextBox.SendKeys(Keys.ArrowDown);
                AssertUI.Value(nullableIntTextBox, "-1");
            });
        }

        public TextBoxTests(ITestOutputHelper output) : base(output)
        {
        }
    }
}
