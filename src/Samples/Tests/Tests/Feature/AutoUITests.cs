using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotVVM.Samples.Tests.Base;
using DotVVM.Testing.Abstractions;
using OpenQA.Selenium;
using Riganti.Selenium.Core;
using Xunit;
using Xunit.Abstractions;

namespace DotVVM.Samples.Tests.Feature
{
    public class AutoUITests : AppSeleniumTest
    {
        public AutoUITests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Feature_AutoUI_AutoEditor()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_AutoUI_AutoEditor);

                var editor = browser.Single("string", SelectByDataUi);
                AssertUI.TagName(editor, "input");
                AssertUI.Attribute(editor, "type", "text");

                editor = browser.Single("int", SelectByDataUi);
                AssertUI.TagName(editor, "input");
                AssertUI.Attribute(editor, "type", "number");

                editor = browser.Single("int-range", SelectByDataUi);
                AssertUI.TagName(editor, "input");
                AssertUI.Attribute(editor, "type", "number");
                AssertUI.Attribute(editor, "min", "0");
                AssertUI.Attribute(editor, "max", "10");

                editor = browser.Single("bool", SelectByDataUi);
                AssertUI.TagName(editor, "label");
                editor = editor.Single("input");
                AssertUI.Attribute(editor, "type", "checkbox");

                editor = browser.Single("datetime", SelectByDataUi);
                AssertUI.TagName(editor, "input");
                AssertUI.Attribute(editor, "type", "datetime-local");

                editor = browser.Single("product-id", SelectByDataUi);
                AssertUI.TagName(editor, "select");
                var options = editor.FindElements("option");
                options.ThrowIfDifferentCountThan(3);
                AssertUI.Attribute(options[0], "value", "00000000-0000-0000-0000-000000000001");
                AssertUI.InnerTextEquals(options[0], "First product");
                AssertUI.Attribute(options[1], "value", "00000000-0000-0000-0000-000000000002");
                AssertUI.InnerTextEquals(options[1], "Second product");
                AssertUI.Attribute(options[2], "value", "00000000-0000-0000-0000-000000000003");
                AssertUI.InnerTextEquals(options[2], "Third product");

                editor = browser.Single("service-type", SelectByDataUi);
                AssertUI.TagName(editor, "select");
                options = editor.FindElements("option");
                options.ThrowIfDifferentCountThan(2);
                AssertUI.Attribute(options[0], "value", "Development");
                AssertUI.InnerTextEquals(options[0], "Development work");
                AssertUI.Attribute(options[1], "value", "Support");
                AssertUI.InnerTextEquals(options[1], "Services & maintenance");

                editor = browser.Single("favorite-product-ids", SelectByDataUi);
                AssertUI.TagName(editor, "ul");
                options = editor.FindElements("li>label");
                options.ThrowIfDifferentCountThan(3);
                AssertUI.Attribute(options[0].Single("input"), "value", "00000000-0000-0000-0000-000000000001");
                AssertUI.InnerTextEquals(options[0].Single("span"), "First product");
                AssertUI.Attribute(options[1].Single("input"), "value", "00000000-0000-0000-0000-000000000002");
                AssertUI.InnerTextEquals(options[1].Single("span"), "Second product");
                AssertUI.Attribute(options[2].Single("input"), "value", "00000000-0000-0000-0000-000000000003");
                AssertUI.InnerTextEquals(options[2].Single("span"), "Third product");
            });
        }

        [Fact]
        public void Feature_AutoUI_AutoForm()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_AutoUI_AutoForm);

                // selection hiding and showing
                var stateField = browser.First("#State__input");
                AssertUI.IsDisplayed(stateField);

                var countryField = browser.First("#CountryId__input");
                countryField.Select("2");

                stateField = browser.First("#State__input");
                AssertUI.IsNotDisplayed(stateField);

                // validation
                var nameField = browser.First("#Name__input");
                var streetField = browser.First("#Name__input");

                AssertUI.IsNotDisplayed(nameField.ParentElement.ParentElement.First(".help"));
                AssertUI.IsNotDisplayed(streetField.ParentElement.ParentElement.First(".help"));

                var validateButton = browser.First("input[type=button]");
                validateButton.Click();

                AssertUI.IsDisplayed(nameField.ParentElement.ParentElement.First(".help"));
                AssertUI.IsDisplayed(streetField.ParentElement.ParentElement.First(".help"));
            });
        }

        [Fact]
        public void Feature_AutoUI_AutoGridViewColumns()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_AutoUI_AutoGridViewColumns);

                var headerCells = browser.FindElements("thead tr:first-child th")
                    .ThrowIfDifferentCountThan(4);
                AssertUI.TextEquals(headerCells[0], "Customer id");
                AssertUI.TextEquals(headerCells[1], "Person or company name");
                AssertUI.TextEquals(headerCells[2], "Birth date");
                AssertUI.TextEquals(headerCells[3], "Message received");

                var cells = browser.FindElements("tbody tr:first-child td")
                    .ThrowIfDifferentCountThan(4);
                AssertUI.TextEquals(cells[0].Single("span"), "1");
                AssertUI.TextEquals(cells[1].Single("h2"), "John Doe");
                AssertUI.Text(cells[2].Single("span"), t => Regex.Replace(t, "\\s+", "") == "4/1/197612:00:00AM");
                AssertUI.IsNotChecked(cells[3].Single("input[type=checkbox]"));
            });
        }

        [Fact]
        public void Feature_AutoUI_BootstrapForm3()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_AutoUI_BootstrapForm3);

                // ensure correct the structure
                var formGroups = browser.FindElements(".form-group").ThrowIfDifferentCountThan(4);

                formGroups[0].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[0].FindElements("div").ThrowIfDifferentCountThan(1);
                formGroups[0].FindElements("input.form-control").ThrowIfDifferentCountThan(1);

                formGroups[1].FindElements("label[for]").ThrowIfDifferentCountThan(0);
                formGroups[1].FindElements("div.checkbox").ThrowIfDifferentCountThan(1);
                formGroups[1].FindElements("label input[type=checkbox]").ThrowIfDifferentCountThan(1);

                formGroups[2].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[2].FindElements("div").ThrowIfDifferentCountThan(1);
                formGroups[2].FindElements("select.form-control").ThrowIfDifferentCountThan(1);

                formGroups[3].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("div.checkbox").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("ul").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("label input[type=checkbox]").ThrowIfDifferentCountThan(2);

                // test validation
                browser.Single("input[type=button]").Click();
                formGroups[0].FindElements("div.has-error").ThrowIfDifferentCountThan(1);
                formGroups[2].FindElements("div.has-error").ThrowIfDifferentCountThan(1);

                formGroups[0].Single("input").SendKeys("abc");
                formGroups[2].Single("select").Select(1);
                browser.Single("input[type=button]").Click();

                formGroups[0].FindElements("div.has-error").ThrowIfDifferentCountThan(0);
                formGroups[2].FindElements("div.has-error").ThrowIfDifferentCountThan(0);
                formGroups[1].FindElements("div.has-error").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("div.has-error").ThrowIfDifferentCountThan(1);
            });
        }

        [Fact]
        public void Feature_AutoUI_BootstrapForm4()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_AutoUI_BootstrapForm4);

                // ensure correct the structure
                var formGroups = browser.FindElements(".form-group").ThrowIfDifferentCountThan(4);

                formGroups[0].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[0].FindElements("div").ThrowIfDifferentCountThan(0);
                formGroups[0].FindElements("input.form-control").ThrowIfDifferentCountThan(1);

                formGroups[1].FindElements("label[for]").ThrowIfDifferentCountThan(0);
                formGroups[1].FindElements("div").ThrowIfDifferentCountThan(0);
                formGroups[1].FindElements("label.form-check.form-check-label input.form-check-input[type=checkbox]").ThrowIfDifferentCountThan(1);

                formGroups[2].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[2].FindElements("div").ThrowIfDifferentCountThan(0);
                formGroups[2].FindElements("select.form-control").ThrowIfDifferentCountThan(1);

                formGroups[3].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("div").ThrowIfDifferentCountThan(0);
                formGroups[3].FindElements("ul.form-check").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("label.form-check-label input.form-check-input[type=checkbox]").ThrowIfDifferentCountThan(2);

                // test validation
                browser.Single("input[type=button]").Click();
                formGroups[0].FindElements("input.is-invalid").ThrowIfDifferentCountThan(1);
                formGroups[2].FindElements("select.is-invalid").ThrowIfDifferentCountThan(1);

                formGroups[0].Single("input").SendKeys("abc");
                formGroups[2].Single("select").Select(1);
                browser.Single("input[type=button]").Click();

                formGroups[0].FindElements("input.is-invalid").ThrowIfDifferentCountThan(0);
                formGroups[2].FindElements("select.is-invalid").ThrowIfDifferentCountThan(0);
                formGroups[1].FindElements("label.is-invalid").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("ul.is-invalid").ThrowIfDifferentCountThan(1);
            });
        }

        [Fact]
        public void Feature_AutoUI_BootstrapForm5()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_AutoUI_BootstrapForm5);

                // ensure correct the structure
                var formGroups = browser.FindElements(".mb-3").ThrowIfDifferentCountThan(4);

                formGroups[0].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[0].FindElements("div").ThrowIfDifferentCountThan(0);
                formGroups[0].FindElements("input.form-control").ThrowIfDifferentCountThan(1);

                formGroups[1].FindElements("label[for]").ThrowIfDifferentCountThan(0);
                formGroups[1].FindElements("div").ThrowIfDifferentCountThan(0);
                formGroups[1].FindElements("label.form-check.form-check-label input.form-check-input[type=checkbox]").ThrowIfDifferentCountThan(1);

                formGroups[2].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[2].FindElements("div").ThrowIfDifferentCountThan(0);
                formGroups[2].FindElements("select.form-select").ThrowIfDifferentCountThan(1);

                formGroups[3].FindElements("label[for]").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("div").ThrowIfDifferentCountThan(0);
                formGroups[3].FindElements("ul.form-check").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("label.form-check-label input.form-check-input[type=checkbox]").ThrowIfDifferentCountThan(2);

                // test validation
                browser.Single("input[type=button]").Click();
                formGroups[0].FindElements("input.is-invalid").ThrowIfDifferentCountThan(1);
                formGroups[2].FindElements("select.is-invalid").ThrowIfDifferentCountThan(1);

                formGroups[0].Single("input").SendKeys("abc");
                formGroups[2].Single("select").Select(1);
                browser.Single("input[type=button]").Click();

                formGroups[0].FindElements("input.is-invalid").ThrowIfDifferentCountThan(0);
                formGroups[2].FindElements("select.is-invalid").ThrowIfDifferentCountThan(0);
                formGroups[1].FindElements("label.is-invalid").ThrowIfDifferentCountThan(1);
                formGroups[3].FindElements("ul.is-invalid").ThrowIfDifferentCountThan(1);
            });
        }
    }
}
