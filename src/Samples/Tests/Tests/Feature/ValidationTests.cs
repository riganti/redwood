using System;
using System.Globalization;
using System.Linq;
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
    public class ValidationTests : AppSeleniumTest
    {
        [Fact]
        public void Feature_Validation_ClientSideObservableUpdate()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ClientSideObservableUpdate);

                var switchTestsButton = browser.ElementAt("input[type=button]", 0);
                var postbackButton = browser.ElementAt("input[type=button]", 1);

                for (int i = 0; i < 2; i++)
                {
                    // load section 1 and validate it
                    switchTestsButton.Click();
                    browser.WaitForPostback();
                    AssertUI.TextEquals(browser.Single("*[data-id=validator1]"), "");

                    postbackButton.Click();
                    browser.WaitForPostback();
                    AssertUI.TextEquals(browser.Single("*[data-id=validator1]"), "The Text field is not a valid e-mail address.");

                    browser.Single("input[data-id=textbox1]").Clear();
                    postbackButton.Click();
                    browser.WaitForPostback();
                    AssertUI.TextEquals(browser.Single("*[data-id=validator1]"), "The Text field is required. The Text field is not a valid e-mail address.");

                    // load section 2 and validate it
                    switchTestsButton.Click();
                    browser.WaitForPostback();
                    AssertUI.TextEquals(browser.Single("*[data-id=validator2]"), "");

                    postbackButton.Click();
                    browser.WaitForPostback();
                    AssertUI.TextEquals(browser.Single("*[data-id=validator2]"), "The Text field is required. The Text field is not a valid e-mail address.");

                    browser.Single("input[data-id=textbox2]").SendKeys("t@t.tt");
                    postbackButton.Click();
                    browser.WaitForPostback();
                    AssertUI.TextEquals(browser.Single("*[data-id=validator2]"), "");
                }
            });
        }

        [Fact]
        public void Feature_Validation_InvalidCssClassNotDuplicated()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_InvalidCssClassNotDuplicated);

                var textbox = browser.Single("input[type=text]");
                var button = browser.Single("input[type=button]");
                var div = browser.Single("div[data-id=validated-div]");

                // empty - one error
                button.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.ClassAttribute(div, c => c == "form-group has-error abc");

                // invalid - two errors
                textbox.SendKeys("abcd");
                button.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(2);
                AssertUI.ClassAttribute(div, c => c == "form-group has-error abc");

                // valid
                textbox.Clear();
                textbox.SendKeys("123");
                button.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
                AssertUI.ClassAttribute(div, c => c == "form-group");
            });
        }

        [Fact]
        public void Feature_Validation_DateTimeValidation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_DateTimeValidation);

                var button = browser.First("input[type=button]");
                var textBoxes = browser.FindElements("input[type=text]").ThrowIfDifferentCountThan(5);
                var validators = browser.FindElements("span[data-control=validator]").ThrowIfDifferentCountThan(5);

                void testValue(string value)
                {
                    foreach (var textBox in textBoxes)
                    {
                        textBox.Clear().SendKeys(value);
                    }
                    button.Click();
                }
                void assertValidators(params string[] errors)
                {
                    if (errors.Length != textBoxes.Count)
                    {
                        throw new ArgumentException("errors");
                    }

                    for (int i = 0; i < textBoxes.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(errors[i]))
                        {
                            AssertUI.HasClass(textBoxes[i], "has-error");
                        }
                        else
                        {
                            AssertUI.HasNotClass(textBoxes[i], "has-error");
                            AssertUI.TextEquals(validators[i], errors[i]);
                        }
                    }
                }

                // empty field - Required validators should be triggered
                testValue("");
                assertValidators("", "", "The Value3 field is required.", "Cannot coerce 'null' to type 'DateTime'.", "The Value5 field is required.");

                // correct value - no error
                testValue("06/14/2017 8:10:35 AM");
                assertValidators("", "", "", "", "");

                // incorrect format - all fields should trigger errors except the one where DotvvmClientFormat is disabled
                testValue("06-14-2017");
                assertValidators("", "The field Value2 is invalid.", "The Value3 field is required. The field Value3 is invalid.", "The field Value4 is invalid.", "The field Value5 is invalid.");
            });
        }

        [Fact]
        public void Feature_Validation_DateTimeValidation_NullableDateTime()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_DateTimeValidation_NullableDateTime);
                var textBox1 = browser.ElementAt("input[type=text]", 0);
                var textBox2 = browser.ElementAt("input[type=text]", 1);
                var button = browser.Single("input[type=button]");
                var errorField = browser.First(".validation-error");

                // empty field - no error
                textBox1.Clear();
                button.Click();
                AssertUI.HasNotClass(textBox1, "has-error");
                AssertUI.HasNotClass(textBox2, "has-error");
                AssertUI.IsNotDisplayed(errorField);

                // invalid value - should report error
                textBox1.SendKeys("06-14-2017");
                button.Click();
                AssertUI.HasClass(textBox1, "has-error");
                AssertUI.HasClass(textBox2, "has-error");
                AssertUI.IsDisplayed(errorField);

                // valid value - no error
                textBox1.Clear();
                textBox1.SendKeys(DateTime.Now.ToString("dd/MM/yyyy H:mm:ss", CultureInfo.InvariantCulture));
                button.Click();
                AssertUI.HasNotClass(textBox1, "has-error");
                AssertUI.HasNotClass(textBox2, "has-error");
                AssertUI.IsNotDisplayed(errorField);
                AssertUI.Value(textBox1, textBox2.GetValue());

                // one textbox has invalid value and second gets valid - should have no error
                textBox1.Clear();
                textBox1.SendKeys("Invalid value");
                textBox2.SendKeys(DateTime.Now.ToString("dd/MM/yyyy H:mm:ss", CultureInfo.InvariantCulture));

                // this is a small hack to make it work with knockout defer updates
                // The real problem is that elemMetadata[0].elementValidationState is set to false from the first invalid textbox.
                // The textbox will reset the elementValidationState when it gets an `update` call, but it is too late
                // with deferUpdates (unless we explicitly call SendEnterKey).
                textBox2.SendEnterKey();
                browser.Wait(10);

                button.Click();
                AssertUI.HasNotClass(textBox1, "has-error");
                AssertUI.HasNotClass(textBox2, "has-error");
                AssertUI.IsNotDisplayed(errorField);
                AssertUI.Value(textBox1, textBox2.GetValue());
            });
        }

        [Fact]
        public void Feature_Validation_DynamicValidation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_DynamicValidation);

                // load the customer
                browser.Click("input[type=button]");

                // try to validate
                browser.Last("input[type=button]").Click();

                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerText(browser.First("li"), s => s.Contains("Email"));

                // fix the e-mail address
                browser.Last("input[type=text]").Clear();
                browser.Last("input[type=text]").SendKeys("test@mail.com");
                browser.Last("input[type=button]").Click();

                // try to validate
                AssertUI.InnerTextEquals(browser.Last("span"), "true");
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
            });
        }

        [Fact]
        public void Feature_Validation_EssentialTypeValidation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_EssentialTypeValidation);

                var addNestedBtn = browser.ElementAt("input[type=button]", 0);
                var withBtn = browser.ElementAt("input[type=button]", 1);
                var withOutBtn = browser.ElementAt("input[type=button]", 2);

                // withnout nested test
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
                withOutBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
                withBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerTextEquals(browser.First("li"), "The NullableIntegerProperty field is required.");
                withOutBtn.Click();                                         // should not remove the validation error
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                browser.First(".nullableInt input[type=text]").SendKeys("5");
                withOutBtn.Click();                                         // should not remove the validation error
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                withBtn.Click();                                            // should remove the validation error
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);

                // with nested test
                browser.First(".nullableInt input[type=text]").Clear();
                addNestedBtn.Click();
                browser.WaitForPostback();
                withOutBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
                withBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(4);
                browser.ElementAt(".nullableInt input[type=text]", 0).SendKeys("10");
                browser.ElementAt(".nullableInt input[type=text]", 2).SendKeys("10");
                withOutBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(4);
                withBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(2);

                // wrong value test
                browser.ElementAt(".nullableInt input[type=text]", 3).SendKeys("15");
                browser.First(".NaNTest input[type=text]").SendKeys("asd");
                withBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(2);

                // correct form test
                browser.First(".NaNTest input[type=text]").Clear();
                browser.First(".NaNTest input[type=text]").SendKeys("55");
                browser.ElementAt(".nullableInt input[type=text]", 1).SendKeys("15");
                withOutBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(2);
                withBtn.Click();
                browser.WaitForPostback();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
            });
        }

        [Fact]
        public void Feature_Validation_EnforceClientSideValidationDisabled()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_EnforceClientSideValidationDisabled);

                var withBtn = browser.ElementAt("input[type=button]", 0);
                var withOutBtn = browser.ElementAt("input[type=button]", 1);

                // withnout nested test
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
                withOutBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
                withBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);

                browser.First(".nullableInt input[type=text]").SendKeys("asd");
                browser.First(".int input[type=text]").SendKeys("asd");
                withBtn.Click();                                         // should not remove the validation error
                browser.FindElements("li").ThrowIfDifferentCountThan(1);

                withOutBtn.Click();                                            // should remove the validation error
                browser.FindElements("li").ThrowIfDifferentCountThan(1);

                browser.First(".int input[type=text]").Clear().SendKeys("220");
                withBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);

            });
        }
        [Fact]
        public void Feature_Validation_ModelStateErrors()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ModelStateErrors);

                //click first button - viewmodel error
                browser.ElementAt("input[type=button]", 0).Click();
                browser.WaitForPostback();
                browser.FindElements(".vmErrors li").ThrowIfDifferentCountThan(1);
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm1Error", 0));
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm2Error", 0));
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm2Error", 1));
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm2Error", 2));

                //click second button - nested viewmodel1 error
                browser.ElementAt("input[type=button]", 1).Click();
                browser.FindElements(".vmErrors li").ThrowIfDifferentCountThan(1);
                AssertUI.IsDisplayed(browser.ElementAt(".vm1Error", 0));
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm2Error", 0));
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm2Error", 1));
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm2Error", 2));

                //click third button - nested viewmodel2 two errors
                browser.ElementAt("input[type=button]", 2).Click();
                browser.WaitForPostback();
                browser.FindElements(".vmErrors li").ThrowIfDifferentCountThan(2);
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm1Error", 0));
                AssertUI.IsDisplayed(browser.ElementAt(".vm2Error", 0));
                AssertUI.IsNotDisplayed(browser.ElementAt(".vm2Error", 1));
                AssertUI.IsDisplayed(browser.ElementAt(".vm2Error", 2));
            });
        }

        [Fact]
        public void Feature_Validation_NestedValidation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_NestedValidation);

                // ensure validators not visible
                AssertUI.IsNotDisplayed(browser.ElementAt("span", 0));
                AssertUI.IsDisplayed(browser.ElementAt("span", 1));
                AssertUI.ClassAttribute(browser.ElementAt("span", 1), s => !s.Contains("validator"));
                AssertUI.IsNotDisplayed(browser.ElementAt("span", 2));

                browser.FindElements(".summary1 li").ThrowIfDifferentCountThan(0);
                browser.FindElements(".summary2 li").ThrowIfDifferentCountThan(0);

                // leave textbox empty and submit the form
                browser.Click("input[type=button]");

                // ensure validators visible
                AssertUI.IsDisplayed(browser.ElementAt("span", 0));
                AssertUI.IsDisplayed(browser.ElementAt("span", 1));
                AssertUI.ClassAttribute(browser.ElementAt("span", 1), s => s.Contains("invalid"));
                AssertUI.IsDisplayed(browser.ElementAt("span", 2));

                browser.FindElements(".summary1 li").ThrowIfDifferentCountThan(0);
                browser.FindElements(".summary2 li").ThrowIfDifferentCountThan(1);

                // submit once again and test the validation summary still holds one error
                browser.Click("input[type=button]");

                // ensure validators visible
                AssertUI.IsDisplayed(browser.ElementAt("span", 0));
                AssertUI.IsDisplayed(browser.ElementAt("span", 1));
                AssertUI.ClassAttribute(browser.ElementAt("span", 1), s => s.Contains("invalid"));
                AssertUI.IsDisplayed(browser.ElementAt("span", 2));

                browser.FindElements(".summary1 li").ThrowIfDifferentCountThan(0);
                browser.FindElements(".summary2 li").ThrowIfDifferentCountThan(1);

                // fill invalid value in the task title
                browser.SendKeys("input[type=text]", "test");
                browser.Click("input[type=button]");

                // ensure validators
                AssertUI.IsNotDisplayed(browser.ElementAt("span", 0));
                AssertUI.IsDisplayed(browser.ElementAt("span", 1));
                AssertUI.ClassAttribute(browser.ElementAt("span", 1), s => !s.Contains("validator"));
                AssertUI.IsNotDisplayed(browser.ElementAt("span", 2));

                browser.FindElements(".summary1 li").ThrowIfDifferentCountThan(0);
                browser.FindElements(".summary2 li").ThrowIfDifferentCountThan(0);

            });
        }

        [Fact]
        public void Feature_Validation_NullValidationTarget()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_NullValidationTarget);

                //get buttons
                var targetRootBtn = browser.ElementAt("input[type=button]", 0);
                var targetSomeBtn = browser.ElementAt("input[type=button]", 1);

                //test both fields empty
                targetRootBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(2);
                AssertUI.InnerTextEquals(browser.ElementAt("li", 0), "The NullObject field is required.");
                AssertUI.InnerTextEquals(browser.ElementAt("li", 1), "The Required field is required.");

                targetSomeBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerTextEquals(browser.First("li"), "The Required field is required.");

                //test invalid Email and empty Required
                browser.ElementAt("input[type=text]", 0).SendKeys("invalid");

                targetRootBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(2);
                AssertUI.InnerTextEquals(browser.ElementAt("li", 0), "The NullObject field is required.");
                AssertUI.InnerTextEquals(browser.ElementAt("li", 1), "The Required field is required.");

                // The invalid Email won't be reported because emails are checked only on the server
                targetSomeBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerTextEquals(browser.ElementAt("li", 0), "The Required field is required.");

                //test valid Email and empty Required
                browser.ElementAt("input[type=text]", 0).Clear();
                browser.ElementAt("input[type=text]", 0).SendKeys("valid@test.com");

                targetRootBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(2);
                AssertUI.InnerTextEquals(browser.ElementAt("li", 0), "The NullObject field is required.");
                AssertUI.InnerTextEquals(browser.ElementAt("li", 1), "The Required field is required.");

                targetSomeBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerTextEquals(browser.First("li"), "The Required field is required.");

                //test invalid Email and filled Required
                browser.ElementAt("input[type=text]", 0).Clear();
                browser.ElementAt("input[type=text]", 0).SendKeys("invalid");
                browser.ElementAt("input[type=text]", 1).SendKeys("filled");

                targetRootBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerTextEquals(browser.ElementAt("li", 0), "The NullObject field is required.");

                // The invalid email will be reported this time because now the check makes it to the server
                targetSomeBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerTextEquals(browser.ElementAt("li", 0), "The Email field is not a valid e-mail address.");

                //test valid Email and filled Required (valid form - expect for null object)
                browser.ElementAt("input[type=text]", 0).Clear();
                browser.ElementAt("input[type=text]", 0).SendKeys("valid@test.com");

                targetRootBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerTextEquals(browser.ElementAt("li", 0), "The NullObject field is required.");

                targetSomeBtn.Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
            });
        }

        [Fact]
        public void Feature_Validation_RegexValidation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_RegexValidation);

                browser.ElementAt("input", 0).SendKeys("25");
                browser.ElementAt("input[type=button]", 0).Click();

                AssertUI.IsNotDisplayed(browser.ElementAt("span", 0));
                AssertUI.InnerTextEquals(browser.ElementAt("span", 1), "25");

                browser.ElementAt("input", 0).SendKeys("a");
                browser.ElementAt("input[type=button]", 0).Click();

                AssertUI.IsDisplayed(browser.ElementAt("span", 0));
                AssertUI.InnerTextEquals(browser.ElementAt("span", 1), "25");
            });
        }

        [Fact]
        public void Feature_Validation_SimpleValidation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_SimpleValidation);

                // ensure validators not visible
                browser.WaitFor(() => {
                    browser.FindElements("li").ThrowIfDifferentCountThan(0);
                }, 1000, 30);



                AssertUI.IsNotDisplayed(browser.ElementAt("span", 0));
                AssertUI.IsDisplayed(browser.ElementAt("span", 1));
                AssertUI.ClassAttribute(browser.ElementAt("span", 1), s => !s.Contains("validator"));
                AssertUI.IsNotDisplayed(browser.ElementAt("span", 2));

                // leave textbox empty and submit the form
                browser.Click("input[type=button]");

                // ensure validators visible
                browser.FindElements("li").ThrowIfDifferentCountThan(1);

                AssertUI.IsDisplayed(browser.ElementAt("span", 0));
                AssertUI.IsDisplayed(browser.ElementAt("span", 1));
                AssertUI.ClassAttribute(browser.ElementAt("span", 1), s => s.Contains("validator"));
                AssertUI.IsDisplayed(browser.ElementAt("span", 2));

                // submit once again and test the validation summary still holds one error
                browser.Click("input[type=button]");
                browser.FindElements("li").ThrowIfDifferentCountThan(1);

                // fill invalid value in the task title
                browser.SendKeys("input[type=text]", "test");
                browser.Click("input[type=button]");

                // ensure validators visible
                browser.FindElements("li").ThrowIfDifferentCountThan(1);

                AssertUI.IsDisplayed(browser.ElementAt("span", 0));
                AssertUI.IsDisplayed(browser.ElementAt("span", 1));
                AssertUI.ClassAttribute(browser.ElementAt("span", 1), s => s.Contains("validator"));
                AssertUI.IsDisplayed(browser.ElementAt("span", 2));

                // fill valid value in the task title
                browser.ClearElementsContent("input[type=text]");
                browser.SendKeys("input[type=text]", "test@mail.com");
                browser.Click("input[type=button]");

                // ensure validators not visible
                browser.FindElements("li").ThrowIfDifferentCountThan(0);

                AssertUI.IsNotDisplayed(browser.ElementAt("span", 0));
                AssertUI.IsDisplayed(browser.ElementAt("span", 1));
                AssertUI.ClassAttribute(browser.ElementAt("span", 1), s => !s.Contains("validator"));
                AssertUI.IsNotDisplayed(browser.ElementAt("span", 2));

                // ensure the item was added
                browser.FindElements(".table tr").ThrowIfDifferentCountThan(4);
            });
        }

        /// <summary>
        /// Feature_s the validation rules load on postback.
        /// </summary>
        [Fact]
        public void Feature_Validation_ValidationRulesLoadOnPostback()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ValidationRulesLoadOnPostback);

                // load the customer
                browser.Click("input[type=button]");

                // try to validate
                browser.Last("input[type=button]").Click();
                browser.FindElements("li").ThrowIfDifferentCountThan(1);
                AssertUI.InnerText(browser.First("li"), s => s.Contains("Email"));

                // fix the e-mail address
                browser.Last("input[type=text]").Clear();
                browser.Last("input[type=text]").SendKeys("test@mail.com");
                browser.Last("input[type=button]").Click();

                // try to validate
                AssertUI.InnerTextEquals(browser.Last("span"), "true");
                browser.FindElements("li").ThrowIfDifferentCountThan(0);
            });
        }

        [Fact]
        public void Feature_Validation_ValidationScopes()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ValidationScopes);

                browser.First("input[type=button]").Click();

                AssertUI.InnerText(browser.First("li"), i => i.Contains("The Value field is required."));
            });
        }

        [Fact]
        public void Feature_Validation_ValidationScopes2()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ValidationScopes2);

                // we are testing the first button

                // don't fill required field, the client validation should appear
                AssertUI.TextEquals(browser.Single(".result"), "0");
                browser.ElementAt("input[type=button]", 0).Click();
                AssertUI.TextEquals(browser.Single(".result"), "0");
                AssertUI.HasClass(browser.ElementAt("input[type=text]", 0), "has-error");
                AssertUI.HasNotClass(browser.ElementAt("input[type=text]", 1), "has-error");

                // fill first required field and second field with a short string, the server validation should appear
                browser.ElementAt("input[type=text]", 0).SendKeys("aaa");
                browser.ElementAt("input[type=text]", 1).SendKeys("aaa");
                AssertUI.TextEquals(browser.Single(".result"), "0");
                AssertUI.IsClickable(browser.ElementAt("input[type=button]", 0));
                browser.ElementAt("input[type=button]", 0).Click();
                AssertUI.TextEquals(browser.Single(".result"), "0");
                AssertUI.HasNotClass(browser.ElementAt("input[type=text]", 0), "has-error");
                AssertUI.HasClass(browser.ElementAt("input[type=text]", 1), "has-error");

                // fill the second field so the validation passes
                browser.ElementAt("input[type=text]", 1).Clear().SendKeys("aaaaaa");
                AssertUI.TextEquals(browser.Single(".result"), "0");
                browser.ElementAt("input[type=button]", 0).Click();
                AssertUI.TextEquals(browser.Single(".result"), "1");
                AssertUI.HasNotClass(browser.ElementAt("input[type=text]", 0), "has-error");
                AssertUI.HasNotClass(browser.ElementAt("input[type=text]", 1), "has-error");

                // clear the fields
                browser.ElementAt("input[type=text]", 0).Clear();
                browser.ElementAt("input[type=text]", 1).Clear();

                // we are testing the second button

                // don't fill required field, the client validation should appear
                AssertUI.TextEquals(browser.Single(".result"), "1");
                browser.ElementAt("input[type=button]", 1).Click();
                AssertUI.TextEquals(browser.Single(".result"), "1");
                AssertUI.HasClass(browser.ElementAt("input[type=text]", 0), "has-error");
                AssertUI.HasNotClass(browser.ElementAt("input[type=text]", 1), "has-error");

                // fill first required field and second field with a short string, the server validation should appear
                browser.ElementAt("input[type=text]", 0).SendKeys("aaa");
                browser.ElementAt("input[type=text]", 1).SendKeys("aaa");
                AssertUI.TextEquals(browser.Single(".result"), "1");
                browser.ElementAt("input[type=button]", 1).Click();
                AssertUI.TextEquals(browser.Single(".result"), "1");
                AssertUI.HasNotClass(browser.ElementAt("input[type=text]", 0), "has-error");
                AssertUI.HasClass(browser.ElementAt("input[type=text]", 1), "has-error");

                // fill the second field so the validation passes
                browser.ElementAt("input[type=text]", 1).Clear().SendKeys("aaaaaa");
                AssertUI.TextEquals(browser.Single(".result"), "1");
                browser.ElementAt("input[type=button]", 1).Click();
                AssertUI.TextEquals(browser.Single(".result"), "2");
                AssertUI.HasNotClass(browser.ElementAt("input[type=text]", 0), "has-error");
                AssertUI.HasNotClass(browser.ElementAt("input[type=text]", 1), "has-error");

            });
        }

        [Fact]
        public void Feature_Validation_Localization()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_Localization);

                browser.ElementAt("button[type=submit]", 0).Click();
                AssertUI.TextEquals(browser.Single(".result-code"), "This comes from resource file!");
                AssertUI.TextEquals(browser.Single(".result-markup"), "This comes from resource file!");

                browser.ElementAt("a", 1).Click();
                browser.ElementAt("button[type=submit]", 0).Click();
                AssertUI.TextEquals(browser.Single(".result-code"), "Tohle pochází z resource souboru!");
                AssertUI.TextEquals(browser.Single(".result-markup"), "Tohle pochází z resource souboru!");

                browser.ElementAt("a", 0).Click();
                browser.ElementAt("button[type=submit]", 0).Click();
                AssertUI.TextEquals(browser.Single(".result-code"), "This comes from resource file!");
                AssertUI.TextEquals(browser.Single(".result-markup"), "This comes from resource file!");
            });
        }

        [Fact]
        public void Feature_Validation_CustomValidation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_CustomValidation);

                var submitButton = browser.First("[data-ui=submit-button]");
                var validationSummary = browser.First("[data-ui=validation-summary]");
                var textbox = browser.First("[data-ui=name-textbox]");

                submitButton.Click();
                Assert.Empty(validationSummary.Children);
                AssertUI.HasNotClass(textbox, "has-error");

                textbox.SendKeys("123");
                submitButton.Click();
                AssertUI.HasClass(textbox, "has-error");
                Assert.Single(validationSummary.Children);

                textbox.Clear();
                textbox.SendKeys("Ted");
                submitButton.Click();
                Assert.Empty(validationSummary.Children);
                AssertUI.HasNotClass(textbox, "has-error");

                textbox.SendKeys("123");
                browser.First("[data-ui=notation-checkbox]").Click();
                submitButton.Click();

                AssertUI.HasClass(textbox, "has-error");
                Assert.Single(validationSummary.Children);
            });
        }

        [Fact]
        public void Feature_Validation_CollectionAsValidationTarget_AutomaticValidation()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ValidationTargetIsCollection);

                var validateButton = browser.First("[data-ui=validation-button]");
                var validationSummary = browser.First("[data-ui=validation-summary]");

                Assert.Empty(validationSummary.Children);

                validateButton.Click();
                Assert.Equal(2, validationSummary.Children.Count);
            });
        }

        [Fact]
        public void Feature_Validation_EncryptedData()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_EncryptedData);

                var counterButton = browser.Single("[data-ui=counter-button]");
                var result = browser.First("[data-ui=result]");

                AssertUI.InnerTextEquals(result, "0");

                counterButton.Click();
                AssertUI.InnerTextEquals(result, "1");
                counterButton.Click();
                AssertUI.InnerTextEquals(result, "2");
            });
        }

        [Theory]
        [InlineData("butNo",8)]
        [InlineData("butVM", 8)]
        [InlineData("butData", 1, "innerProp")]
        [InlineData("butData2", 1, "dataContext")]
        [InlineData("butCol", 3, "repeater")]
        [InlineData("validationTarget-butNo", 8)]
        [InlineData("validationTarget-butVM", 2)]
        [InlineData("validationTarget-butData", 1)]
        public void Feature_Validation_ValidationPathResolving(string buttonSelector,int expectedCountOfValidationAsterisks, string sectionSelector=null)
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ValidationPropertyPathResolving);
                var button = browser.Single(buttonSelector, SelectByDataUi);

                if (sectionSelector!=null)
                {

                    var section = browser.Single(sectionSelector, SelectByDataUi);
                    var validationAsterisks = section.FindElements("span", By.TagName);
                    foreach (var asterisk in validationAsterisks) AssertUI.IsNotDisplayed(asterisk);
                    button.Click();
                    foreach (var asterisk in validationAsterisks) AssertUI.IsDisplayed(asterisk);
                }
                else
                {
                    button.Click();
                }



                var countOfDisplayedAsterisks = browser.FindElements("div > span",By.CssSelector).Where(t=>t.GetInnerText()=="*").Count(t => t.IsDisplayed());
                Assert.Equal(expectedCountOfValidationAsterisks, countOfDisplayedAsterisks);
            });
        }
        [Theory]
        [InlineData("/Text")]
        [InlineData("/Data/Text")]
        [InlineData("/Data2/Text")]
        [InlineData("/Col/0/Text")]
        [InlineData("/Col/1/Text")]
        [InlineData("/Col/2/Text")]
        public void Feature_Validation_ValidationPaths(string validationPath)
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ValidationPropertyPathResolving);

                var button = browser.Single("butNo", SelectByDataUi);
                var validationPathsList = browser.Single("ValidationErrors", SelectBy.Id);

                button.Click();

                browser.WaitFor(() => {

                    var validationPaths = validationPathsList.FindElements("li").Select(t => t.GetInnerText()).ToList();

                    bool hasCorrectValidationPath = validationPaths.Any(t => t == validationPath);
                    Assert.True(hasCorrectValidationPath, $"None of the errors has expected validation path ({validationPath}).");
                }, timeout: 2_000);
            });
        }

        [Fact]
        public void Feature_Validation_ComplexExpressionInValidatorValue()
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_ValidatorValueComplexExpressions);
                var textbox = browser.Single("textbox", SelectByDataUi);
                var grid = browser.Single("grid", SelectByDataUi);
                var button = browser.Single("button", SelectByDataUi);
                var validationSummary = browser.Single("summary", SelectByDataUi);

                // Clear DateTime which is marked as [Required]
                textbox.Clear();
                // Perform postback to trigger validation
                button.Click();

                AssertUI.HasClass(textbox, "error");
                Assert.Equal(2, validationSummary.Children.Count);
            });
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("1", 0)]
        [InlineData("0", 1)]
        [InlineData("1500", 1)]
        public void Feature_Validation_RangeClientSideValidation(string value, int errorCount)
        {
            RunInAllBrowsers(browser => {
                browser.NavigateToUrl(SamplesRouteUrls.FeatureSamples_Validation_RangeClientSideValidation);

                var textbox = browser.Single("textbox", SelectByDataUi);
                var validationSummary = browser.Single("summary", SelectByDataUi);
                var button = browser.Single("button", SelectByDataUi);
                var result = browser.Single("result", SelectByDataUi);

                textbox.Clear().SendKeys(value).SendKeys(Keys.Tab);
                button.Click();

                validationSummary.Children.ThrowIfDifferentCountThan(errorCount);
                AssertUI.TextEquals(result, errorCount == 0 ? "Valid" : "");
            });
        }


        public ValidationTests(ITestOutputHelper output) : base(output)
        {
        }
    }
}
