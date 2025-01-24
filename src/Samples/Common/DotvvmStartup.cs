using DotVVM.Framework.Compilation.Styles;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Routing;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.ViewModel;
using DotVVM.Framework.ViewModel.Serialization;
using DotVVM.Samples.BasicSamples.Controls;
using DotVVM.Samples.BasicSamples.ViewModels.FeatureSamples.Redirect;
using DotVVM.Samples.BasicSamples.ViewModels.FeatureSamples.Serialization;
using DotVVM.Samples.BasicSamples.ViewModels.FeatureSamples.StaticCommand;
using DotVVM.Samples.Common;
using DotVVM.Samples.Common.ViewModels.FeatureSamples.DependencyInjection;
using DotVVM.Samples.Common.ViewModels.FeatureSamples.ServerSideStyles;
using Microsoft.Extensions.DependencyInjection;
using DotVVM.Framework.Controls;
using System.Collections.Generic;
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using DotVVM.AutoUI.Controls;
using DotVVM.Samples.Common.Api.AspNetCore;
using DotVVM.Samples.Common.Api.Owin;
using DotVVM.Samples.Common.Controls;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Samples.Common.ViewModels.FeatureSamples.JavascriptTranslation;
using DotVVM.Samples.Common.Views.FeatureSamples.PostbackAbortSignal;
using DotVVM.Samples.Common.ViewModels.FeatureSamples.BindingVariables;
using DotVVM.Samples.Common.Views.ControlSamples.TemplateHost;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Samples.Common.Presenters;
using DotVVM.Samples.Common.ViewModels.FeatureSamples.CustomPrimitiveTypes;

namespace DotVVM.Samples.BasicSamples
{
    public class DotvvmStartup : IDotvvmStartup
    {
        public const string GitHubTokenEnvName = "GITHUB_TOKEN";
        public const string GitHubTokenConfigName = "githubApiToken";

        private bool IsInInvariantCultureMode()
        {
            // Makes the samples run even if only invariant culture is enabled
            // This is useful for testing older versions of .NET Core which rely on ICU which is no longer installed
            try
            {
                new System.Globalization.CultureInfo("en-US");
                return false;
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                return true;
            }
        }

        public void Configure(DotvvmConfiguration config, string applicationPath)
        {
            if (!IsInInvariantCultureMode())
            {
                config.DefaultCulture = "en-US";
            }

            AddControls(config);
            AddStyles(config);

            AddRedirections(config);
            AddRoutes(config);

            // configure serializer
            config.GetSerializationMapper()
                .Map(typeof(SerializationViewModel), map => {
                    map.Property(nameof(SerializationViewModel.Value)).Bind(Direction.ServerToClient);
                    map.Property(nameof(SerializationViewModel.Value2)).Bind(Direction.ClientToServer);
                    map.Property(nameof(SerializationViewModel.IgnoredProperty)).Ignore();
                });

            config.RegisterApiGroup(typeof(Common.Api.Owin.TestWebApiClientOwin), "http://localhost:61453/", "Scripts/TestWebApiClientOwin.js", "_apiOwin");
            config.RegisterApiClient(typeof(Common.Api.AspNetCore.TestWebApiClientAspNetCore), "http://localhost:50001/", "Scripts/TestWebApiClientAspNetCore.js", "_apiCore");

            config.RegisterApiClient(typeof(AzureFunctionsApi.Client), "https://dotvvmazurefunctionstest.azurewebsites.net/", "Scripts/AzureFunctionsApiClient.js", "_azureFuncApi");

            LoadSampleConfiguration(config, applicationPath);

            config.Resources.RegisterStylesheetFile("bulma", "node_modules/bulma/css/bulma.css");

            config.Runtime.CompressPostbacks.IncludeRoute("FeatureSamples_PostBack_RequestCompression");
            if (config.ExperimentalFeatures.LazyCsrfToken.Enabled)
                config.ExperimentalFeatures.LazyCsrfToken.ExcludeRoute("FeatureSamples_PostBack_RequestCompression");
            if (config.ExperimentalFeatures.ServerSideViewModelCache.Enabled)
                config.ExperimentalFeatures.ServerSideViewModelCache.ExcludeRoute("FeatureSamples_PostBack_RequestCompression");

            config.Markup.JavascriptTranslator.MethodCollection.AddMethodTranslator(typeof(JavascriptTranslationTestMethods),
                    nameof(JavascriptTranslationTestMethods.Unwrap),
                         new GenericMethodCompiler((a) =>
                            new JsIdentifierExpression("unwrap")
                                            .Invoke(a[1])
                                    ), allowGeneric: true, allowMultipleMethods: true);

            config.Diagnostics.CompilationPage.IsApiEnabled = true;
            config.Diagnostics.CompilationPage.IsEnabled = true;
            config.Diagnostics.CompilationPage.ShouldCompileAllOnLoad = false;

            config.AssertConfigurationIsValid();

            config.RouteTable.Add("Errors_Routing_NonExistingView", "Errors/Routing/NonExistingView", "Views/Errors/Routing/NonExistingView.dothml");

            config.Markup.JavascriptTranslator.MethodCollection
                .AddPropertyGetterTranslator(typeof(ITypeId), nameof(ITypeId.IdValue),
                    new GenericMethodCompiler(args => args[0])
                );
            config.Markup.JavascriptTranslator.MethodCollection
                .AddMethodTranslator(typeof(SampleId), nameof(ToString),
                    new GenericMethodCompiler(args => args[0])
                );
        }

        private void LoadSampleConfiguration(DotvvmConfiguration config, string applicationPath)
        {
            var jsonText = File.ReadAllText(Path.Combine(applicationPath, "sampleConfig.json"));
            var json = JObject.Parse(jsonText);

            // find active profile
            var defaultProfile = json.Value<string>("defaultProfile");
            var activeProfile = Environment.GetEnvironmentVariable("DOTVVM_SAMPLES_CONFIG_PROFILE") switch {
                "" or null => defaultProfile,
                var p => p,
            };

            var profiles = json.Value<JArray>("profiles");
            var profile = profiles.Single(p => p.Value<string>("name") == activeProfile);

            JsonConvert.PopulateObject(profile.Value<JObject>("config").ToString(), config);

            var githubTokenEnv = Environment.GetEnvironmentVariable(GitHubTokenEnvName);
            if (githubTokenEnv is object)
            {
                json.Value<JObject>("appSettings")[GitHubTokenConfigName] = githubTokenEnv;
            }

            SampleConfiguration.Initialize(
                json.Value<JObject>("appSettings").Properties().ToDictionary(p => p.Name, p => p.Value.Value<string>())
            );
        }

        public static void AddStyles(DotvvmConfiguration config)
        {
            // HasViewInDirectory samples
            config.Styles.Register<ServerSideStylesControl>(c => c.HasViewInDirectory("Views/FeatureSamples/ServerSideStyles/DirectoryStyle/"))
                .SetAttribute("directory", "matching");
            config.Styles.Register("customTagName", c => c.HasViewInDirectory("Views/FeatureSamples/ServerSideStyles/DirectoryStyle/"))
                .SetAttribute("directory", "matching");

            // HasDataContext and HasRootDataContext samples
            config.Styles.Register("customDataContextTag", c => c.HasRootDataContext<ServerSideStylesMatchingViewModel>()).
                SetAttribute("rootDataContextCheck", "matching");
            config.Styles.Register("customDataContextTag", c => c.HasDataContext<ServerSideStylesMatchingViewModel.TestingObject>()).
                SetAttribute("dataContextCheck", "matching");

            // All style samples
            config.Styles.Register<ServerSideStylesControl>()
                .SetAttribute("value", "Text changed")
                .SetDotvvmProperty(ServerSideStylesControl.CustomProperty, "Custom property changed", StyleOverrideOptions.Ignore)
                .SetAttribute("class", "Class changed", StyleOverrideOptions.Overwrite);
            config.Styles.Register("customTagName")
                .SetAttribute("ignore", "Attribute ignored", StyleOverrideOptions.Ignore)
                .SetAttribute("overwrite", "Attribute changed", StyleOverrideOptions.Overwrite)
                .SetAttribute("append", "Attribute appended", StyleOverrideOptions.Append)
                .SetAttribute("class", "new-class", StyleOverrideOptions.Append);
            config.Styles.Register<ServerSideStylesControl>(c => c.HasProperty(ServerSideStylesControl.CustomProperty), false)
                .SetAttribute("derivedAttr", "Derived attribute");
            config.Styles.Register<ServerSideStylesControl>(c => c.HasProperty(ServerSideStylesControl.AddedProperty))
                .SetAttribute("addedAttr", "Added attribute");

            config.Styles.Register<Button>(c => c.HasHtmlAttribute("server-side-style-attribute"))
               .SetControlProperty(PostBack.HandlersProperty, new ConfirmPostBackHandler("ConfirmPostBackHandler Content"));

            // Bootstrap styles
            config.Styles.Register<BootstrapForm>(c => c.HasTag("Bootstrap3"))
                .SetProperty(c => c.FormGroupCssClass, "form-group")
                .SetProperty(c => c.LabelCssClass, "")
                .SetProperty(c => c.FormControlCssClass, "form-control")
                .SetProperty(c => c.FormSelectCssClass, "form-control")
                .SetProperty(c => c.FormCheckCssClass, "checkbox")
                .SetProperty(c => c.FormCheckLabelCssClass, "")
                .SetProperty(c => c.FormCheckInputCssClass, "")
                .SetProperty(c => c.WrapControlInDiv, true)
                .SetDotvvmProperty(Validator.InvalidCssClassProperty, "has-error");

            config.Styles.Register<BootstrapForm>(c => c.HasTag("Bootstrap4"))
                .SetProperty(c => c.FormGroupCssClass, "form-group")
                .SetProperty(c => c.LabelCssClass, "")
                .SetProperty(c => c.FormControlCssClass, "form-control")
                .SetProperty(c => c.FormSelectCssClass, "form-control")
                .SetProperty(c => c.FormCheckCssClass, "form-check")
                .SetProperty(c => c.FormCheckLabelCssClass, "form-check-label")
                .SetProperty(c => c.FormCheckInputCssClass, "form-check-input")
                .SetDotvvmProperty(Validator.InvalidCssClassProperty, "is-invalid");
        }

        private static void AddRedirections(DotvvmConfiguration config)
        {
            config.RouteTable.AddUrlRedirection("Redirection1", "FeatureSamples/Redirect/RedirectionHelpers_PageA", (_) => "https://www.dotvvm.com");
            config.RouteTable.AddRouteRedirection("Redirection2", "FeatureSamples/Redirect/RedirectionHelpers_PageB/{Id}", (_) => "FeatureSamples_Redirect_RedirectionHelpers_PageC-PageDetail", new { Id = 66 }, urlSuffixProvider: c => "?test=aaa");
            config.RouteTable.AddRouteRedirection("Redirection3", "FeatureSamples/Redirect/RedirectionHelpers_PageD/{Id}", (_) => "FeatureSamples_Redirect_RedirectionHelpers_PageE-PageDetail", new { Id = 77 },
                parametersProvider: (context) => {
                    var newDict = new Dictionary<string, object>(context.Parameters);
                    newDict["Id"] = 1221;
                    return newDict;
                });
            
            config.RouteTable.AddRouteRedirection("ComplexSamples_SPA_redirect", "ComplexSamples/SPA/redirect", "ComplexSamples_SPA_test");
        }

        private static void AddRoutes(DotvvmConfiguration config)
        {
            config.RouteTable.Add("Default", "", "Views/Default.dothtml");

            config.RouteTable.Add("ComplexSamples_SPARedirect_home", "ComplexSamples/SPARedirect", "Views/ComplexSamples/SPARedirect/home.dothtml");

            config.RouteTable.Add("ControlSamples_SpaContentPlaceHolder_HistoryApi_PageA", "ControlSamples/SpaContentPlaceHolder_HistoryApi/PageA/{Id}", "Views/ControlSamples/SpaContentPlaceHolder_HistoryApi/PageA.dothtml", new { Id = 0 });
            config.RouteTable.Add("ControlSamples_SpaContentPlaceHolder_HistoryApi", "ControlSamples/SpaContentPlaceHolder_HistoryApi", "Views/ControlSamples/SpaContentPlaceHolder_HistoryApi/SpaMaster.dotmaster");
            config.RouteTable.Add("ControlSamples_TextBox_TextBox_Format", "ControlSamples/TextBox/TextBox_Format", "Views/ControlSamples/TextBox/TextBox_Format.dothtml", presenterFactory: LocalizablePresenter.BasedOnQuery("lang"));
            config.RouteTable.Add("ControlSamples_TextBox_TextBox_Format_Binding", "ControlSamples/TextBox/TextBox_Format_Binding", "Views/ControlSamples/TextBox/TextBox_Format_Binding.dothtml", presenterFactory: LocalizablePresenter.BasedOnQuery("lang"));
            config.RouteTable.Add("FeatureSamples_ParameterBinding_ParameterBinding", "FeatureSamples/ParameterBinding/ParameterBinding/{A}", "Views/FeatureSamples/ParameterBinding/ParameterBinding.dothtml", new { A = 123 });
            config.RouteTable.Add("FeatureSamples_Localization_Localization", "FeatureSamples/Localization/Localization", "Views/FeatureSamples/Localization/Localization.dothtml", presenterFactory: LocalizablePresenter.BasedOnQuery("lang"));
            config.RouteTable.Add("FeatureSamples_Localization_Localization_NestedPage_Type", "FeatureSamples/Localization/Localization_NestedPage_Type", "Views/FeatureSamples/Localization/Localization_NestedPage_Type.dothtml", presenterFactory: LocalizablePresenter.BasedOnQuery("lang"));
            config.RouteTable.Add("FeatureSamples_ParameterBinding_OptionalParameterBinding", "FeatureSamples/ParameterBinding/OptionalParameterBinding/{Id?}", "Views/FeatureSamples/ParameterBinding/OptionalParameterBinding.dothtml");
            config.RouteTable.Add("FeatureSamples_ParameterBinding_OptionalParameterBinding2", "FeatureSamples/ParameterBinding/OptionalParameterBinding2/{Id?}", "Views/FeatureSamples/ParameterBinding/OptionalParameterBinding.dothtml", new { Id = 300 });
            config.RouteTable.Add("FeatureSamples_Validation_Localization", "FeatureSamples/Validation/Localization", "Views/FeatureSamples/Validation/Localization.dothtml", presenterFactory: LocalizablePresenter.BasedOnQuery("lang"));
            config.RouteTable.Add("FeatureSamples_Localization_Globalize", "FeatureSamples/Localization/Globalize", "Views/FeatureSamples/Localization/Globalize.dothtml", presenterFactory: LocalizablePresenter.BasedOnQuery("lang"));
            config.RouteTable.Add("FeatureSamples_CustomPrimitiveTypes_Basic", "FeatureSamples/CustomPrimitiveTypes/Basic/{Id?}", "Views/FeatureSamples/CustomPrimitiveTypes/Basic.dothtml");

            config.RouteTable.Add("FeatureSamples_Localization_LocalizableRoute", "FeatureSamples/Localization/LocalizableRoute", "Views/FeatureSamples/Localization/LocalizableRoute.dothtml",
                localizedUrls: new LocalizedRouteUrl[] {
                        new("cs-CZ", "cs/FeatureSamples/Localization/lokalizovana-routa"),
                        new("de", "de/FeatureSamples/Localization/lokalisierte-route"),
                });
            config.RouteTable.AddPartialMatchHandler(new CanonicalRedirectPartialMatchRouteHandler());

            config.RouteTable.AutoDiscoverRoutes(new DefaultRouteStrategy(config));

            config.RouteTable.Add("ControlSamples_Repeater_RouteLinkQuery-PageDetail", "ControlSamples/Repeater/RouteLinkQuery/{Id}", "Views/ControlSamples/Repeater/RouteLinkQuery.dothtml", new { Id = 0 });
            config.RouteTable.Add("FeatureSamples_Redirect_RedirectionHelpers_PageB-PageDetail", "FeatureSamples/Redirect/RedirectionHelpers_PageB/{Id}", "Views/FeatureSamples/Redirect/RedirectionHelpers_PageB.dothtml", new { Id = 22 });
            config.RouteTable.Add("FeatureSamples_Redirect_RedirectionHelpers_PageC-PageDetail", "FeatureSamples/Redirect/RedirectionHelpers_PageC/{Id}", "Views/FeatureSamples/Redirect/RedirectionHelpers_PageC.dothtml", new { Id = 33 });
            config.RouteTable.Add("FeatureSamples_Redirect_RedirectionHelpers_PageD-PageDetail", "FeatureSamples/Redirect/RedirectionHelpers_PageD/{Id}", "Views/FeatureSamples/Redirect/RedirectionHelpers_PageD.dothtml", new { Id = 44 });
            config.RouteTable.Add("FeatureSamples_Redirect_RedirectionHelpers_PageE-PageDetail", "FeatureSamples/Redirect/RedirectionHelpers_PageE/{Id}", "Views/FeatureSamples/Redirect/RedirectionHelpers_PageE.dothtml", new { Id = 55 });
            config.RouteTable.Add("RepeaterRouteLink-PageDetail", "ControlSamples/Repeater/RouteLink/{Id}", "Views/ControlSamples/Repeater/RouteLink.dothtml", new { Id = 0 });
            config.RouteTable.Add("RepeaterRouteLink-PageDetail_IdOptional", "ControlSamples/Repeater/RouteLink/{Id?}", "Views/ControlSamples/Repeater/RouteLink.dothtml");
            config.RouteTable.Add("RepeaterRouteLink-PageDetail_IdOptionalPrefixed", "ControlSamples/Repeater/RouteLink/id-{Id?}", "Views/ControlSamples/Repeater/RouteLink.dothtml");
            config.RouteTable.Add("RepeaterRouteLink-PageDetail_IdOptionalAtStart", "id-{Id?}/ControlSamples/Repeater/RouteLink", "Views/ControlSamples/Repeater/RouteLink.dothtml");
            config.RouteTable.Add("RepeaterRouteLinkUrlSuffix-PageDetail", "ControlSamples/Repeater/RouteLinkUrlSuffix/{Id}", "Views/ControlSamples/Repeater/RouteLink.dothtml", new { Id = 0 });
            config.RouteTable.Add("FeatureSamples_Redirect_RedirectFromPresenter", "FeatureSamples/Redirect/RedirectFromPresenter", provider => new ViewModels.FeatureSamples.Redirect.RedirectingPresenter());
            config.RouteTable.Add("FeatureSamples_EmbeddedResourceControls_EmbeddedResourceView", "FeatureSamples/EmbeddedResourceControls/EmbeddedResourceView", "embedded://EmbeddedResourceControls/EmbeddedResourceView.dothtml");
            config.RouteTable.Add("FeatureSamples_PostBack_PostBackHandlers_Localized", "FeatureSamples/PostBack/PostBackHandlers_Localized", "Views/FeatureSamples/PostBack/ConfirmPostBackHandler.dothtml", presenterFactory: LocalizablePresenter.BasedOnQuery("lang"));

            config.RouteTable.Add("Errors_UndefinedRouteLinkParameters-PageDetail", "Erros/UndefinedRouteLinkParameters/{Id}", "Views/Errors/UndefinedRouteLinkParameters.dothtml", new { Id = 0 });

            config.RouteTable.Add("DumpExtensionsMethods", "dump-extension-methods", _ => new DumpExtensionMethodsPresenter());
        }

        private static void AddControls(DotvvmConfiguration config)
        {
            config.Markup.AddCodeControls("cc", typeof(Controls.ServerSideStylesControl));
            config.Markup.AddCodeControls("cc", typeof(Controls.TextRepeater));
            config.Markup.AddCodeControls("cc", typeof(DerivedControlUsageValidationTestControl));
            config.Markup.AddCodeControls("PropertyUpdate", typeof(Controls.ServerRenderedLabel));
            config.Markup.AddMarkupControl("IdGeneration", "Control", "Views/FeatureSamples/IdGeneration/IdGeneration_control.dotcontrol");
            config.Markup.AddMarkupControl("FileUploadInRepeater", "FileUploadWrapper", "Views/ComplexSamples/FileUploadInRepeater/FileUploadWrapper.dotcontrol");
            config.Markup.AddMarkupControl("cc", "RecursiveTextRepeater", "Views/FeatureSamples/PostBack/RecursiveTextRepeater.dotcontrol");
            config.Markup.AddMarkupControl("cc", "RecursiveTextRepeater2", "Views/FeatureSamples/PostBack/RecursiveTextRepeater2.dotcontrol");
            config.Markup.AddMarkupControl("cc", "ModuleControl", "Views/FeatureSamples/ViewModules/ModuleControl.dotcontrol");
            config.Markup.AddMarkupControl("cc", "LinkModuleControl", "Views/FeatureSamples/ViewModules/LinkModuleControl.dotcontrol");
            config.Markup.AddMarkupControl("cc", "Incrementer", "Views/FeatureSamples/ViewModules/Incrementer.dotcontrol");
            config.Markup.AddMarkupControl("cc", "StateIncrementer", "Views/FeatureSamples/ViewModules/StateIncrementer.dotcontrol");
            config.Markup.AddMarkupControl("cc", "TemplatedListControl", "Views/ControlSamples/TemplateHost/TemplatedListControl.dotcontrol");
            config.Markup.AddMarkupControl("cc", "TemplatedMarkupControl", "Views/ControlSamples/TemplateHost/TemplatedMarkupControl.dotcontrol");
            config.Markup.AddCodeControls("cc", typeof(CompositeControlWithTemplate));
            config.Markup.AddCodeControls("cc", typeof(Loader));
            config.Markup.AddMarkupControl("sample", "EmbeddedResourceControls_Button", "embedded://EmbeddedResourceControls/Button.dotcontrol");
            config.Markup.AddMarkupControl("cc", "NodeControl", "Views/ControlSamples/HierarchyRepeater/NodeControl.dotcontrol");
            config.Markup.AddMarkupControl("cc", "CommandInsideWhereControl", "Views/FeatureSamples/JavascriptTranslation/CommandInsideWhereControl.dotcontrol");
            config.Markup.AutoDiscoverControls(new DefaultControlRegistrationStrategy(config, "sample", "Views/"));

            if (config.Markup.Controls.FirstOrDefault(c => c.Src is not null && Path.IsPathRooted(c.Src)) is {} invalidControl)
                throw new Exception($"Some controls have absolute paths! ({invalidControl.TagPrefix}:{invalidControl.TagName} - {invalidControl.Src})");
        }

    }
}
