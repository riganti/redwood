using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DotVVM.Framework.Utils;
using System.Text;
using System.Linq;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using AngleSharp.Html;
using System.Globalization;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation.ViewCompiler;
using DotVVM.Framework.ResourceManagement;
using System.Security.Claims;
using DotVVM.Framework.ViewModel.Serialization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotVVM.Framework.Testing
{
    public class ControlTestHelper
    {
        public readonly DotvvmConfiguration Configuration;
        private readonly FakeMarkupFileLoader fileLoader;
        private readonly DotvvmPresenter presenter;

        IControlBuilderFactory controlBuilderFactory => GetService<IControlBuilderFactory>();

        public ControlTestHelper(bool debug = true, Action<DotvvmConfiguration>? config = null, Action<IDotvvmServiceCollection>? services = null)
        {
            fileLoader = new FakeMarkupFileLoader(null);
            this.Configuration = DotvvmTestHelper.CreateConfiguration(s => {
                s.AddSingleton<IMarkupFileLoader>(fileLoader);
                services?.Invoke(new DotvvmServiceCollection(s));
            });
            this.Configuration.Markup.AddCodeControls("tc", exampleControl: typeof(FakeHeadResourceLink));
            this.Configuration.ApplicationPhysicalPath = Path.GetTempPath();
            this.Configuration.Debug = debug;
            config?.Invoke(this.Configuration);
            presenter = (DotvvmPresenter)this.Configuration.ServiceProvider.GetRequiredService<IDotvvmPresenter>();
        }

        public T GetService<T>() where T: notnull => Configuration.ServiceProvider.GetRequiredService<T>();

        public (ControlBuilderDescriptor descriptor, Lazy<IControlBuilder> builder) CompilePage(
            string markup,
            string fileName,
            Dictionary<string, string>? markupFiles = null)
        {
            if (!fileLoader.MarkupFiles.TryAdd(fileName, markup))
            {
                if (fileLoader.MarkupFiles[fileName] != markup)
                    throw new Exception($"File {fileName} already exists");
            }

            if (markupFiles is object) foreach (var markupFile in markupFiles)
            {
                if (!fileLoader.MarkupFiles.TryAdd(markupFile.Key, markupFile.Value))
                    if (fileLoader.MarkupFiles[markupFile.Key] != markupFile.Value)
                        throw new Exception($"File {markupFile.Value} already exists");
            }

            return controlBuilderFactory.GetControlBuilder(fileName);
        }

        private TestDotvvmRequestContext PrepareRequest(
            string fileName,
            PostbackRequestModel? postback = null,
            ClaimsPrincipal? user = null,
            CultureInfo? culture = null
        )
        {
            CultureInfo.CurrentCulture = culture ?? new CultureInfo("en-US");
            CultureInfo.CurrentUICulture = culture ?? new CultureInfo("en-US");

            var context = DotvvmTestHelper.CreateContext(
                Configuration,
                route: new Framework.Routing.DotvvmRoute("testpage", fileName, "testpage", null, _ => throw new Exception(), Configuration),
                requestType: postback is object ? DotvvmRequestType.Command : DotvvmRequestType.Navigate
            );
            context.CsrfToken = null;
            var httpContext = (TestHttpContext)context.HttpContext;

            if (postback is object)
            {
                httpContext.Request.Method = "POST";
                httpContext.Request.Headers["X-DotVVM-PostBack"] = new[] { "true" };
                httpContext.Request.Body = new MemoryStream(
                    JsonSerializer.SerializeToUtf8Bytes(postback, DefaultSerializerSettingsProvider.Instance.SettingsHtmlUnsafe)
                );
            }

            if (user is {})
                httpContext.User = user;

            return context;
        }

        private TestDotvvmRequestContext PreparePage(
            string markup,
            Dictionary<string, string>? markupFiles,
            string? fileName,
            ClaimsPrincipal? user = null,
            CultureInfo? culture = null
        )
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture =
                culture ?? new CultureInfo("en-US");

            Configuration.Freeze();
            fileName = (fileName ?? "testpage") + ".dothtml";
            var (_, controlBuilder) = CompilePage(markup, fileName, markupFiles);
            return PrepareRequest(fileName, user: user, culture: culture);
        }

        public async Task<PageRunResult> RunPage(
            Type viewModel,
            string markup,
            Dictionary<string, string>? markupFiles = null,
            string directives = "",
            bool renderResources = false,
            [CallerMemberName] string? fileName = null,
            ClaimsPrincipal? user = null,
            CultureInfo? culture = null)
        {
            if (!markup.Contains("<body") && !markup.Contains("<dot:Content"))
            {
                markup = $"<body Validation.Enabled=false >\n{markup}\n{(renderResources ? "" : "<tc:FakeBodyResourceLink />")}\n</body>";
            }
            else if (!renderResources)
            {
                markup = "<tc:FakeBodyResourceLink />" + markup;
            }
            if (!markup.Contains("<head") && !markup.Contains("<dot:Content"))
            {
                markup = $"<head></head>\n{markup}";
            }
            if (!renderResources)
            {
                markup = "<tc:FakeHeadResourceLink />" + markup;
            }
            markup = $"@viewModel {viewModel.ToString().Replace("+", ".")}\n{directives}\n\n{markup}";
            var request = PreparePage(markup, markupFiles, fileName, user, culture);
            await presenter.ProcessRequest(request);
            return CreatePageResult(request);
        }

        public async Task<CommandRunResult> RunCommand(
            string filePath,
            PostbackRequestModel model,
            CultureInfo? culture = null)
        {
            var request = PrepareRequest(filePath, model, culture: culture);
            try
            {
                await presenter.ProcessRequest(request);
            }
            catch (DotvvmInterruptRequestExecutionException)
            {
            }
            return CreateCommandResult(request);
        }

        private CommandRunResult CreateCommandResult(TestDotvvmRequestContext request)
        {
            return new CommandRunResult(request);
        }

        private IEnumerable<(DotvvmControl, DotvvmProperty, ICommandBinding)> FindCommands(DotvvmControl view) =>
            from control in view.GetThisAndAllDescendants()
            from property in control.Properties
            let binding = property.Value as ICommandBinding
            where binding != null
            select (control, property.Key, binding);

        private PageRunResult CreatePageResult(TestDotvvmRequestContext context)
        {
            var htmlOutput = System.Text.Encoding.UTF8.GetString(context.HttpContext.CastTo<TestHttpContext>().Response.Body.ToArray());
            var commands = FindCommands(context.View).ToArray();
            var headResources = context.View.GetAllDescendants().OfType<FakeHeadResourceLink>().FirstOrDefault()?.CapturedHtml;
            var bodyResources = context.View.GetAllDescendants().OfType<FakeBodyResourceLink>().FirstOrDefault()?.CapturedHtml;

            var p = new HtmlParser();
            var htmlDocument = p.ParseDocument(htmlOutput);

            foreach (var el in htmlDocument.All)
            {
                // order attributes by name
                var attrs = el.Attributes.OrderBy(a => a.Name).ToArray();
                foreach (var attr in attrs) el.RemoveAttribute(attr.NamespaceUri!, attr.LocalName);
                foreach (var attr in attrs) el.SetAttribute(attr.NamespaceUri!, attr.LocalName, attr.Value);
            }
            var viewModel = context.Services.GetRequiredService<IViewModelSerializer>().SerializeViewModel(context);
            Console.WriteLine(viewModel);
            return new PageRunResult(
                this,
                context.Route.VirtualPath,
                JsonNode.Parse(viewModel)!.AsObject(),
                htmlOutput,
                headResources,
                bodyResources,
                htmlDocument,
                commands,
                context.View,
                context
            );
        }
    }

    public class CommandRunResult
    {
        public CommandRunResult(TestDotvvmRequestContext context)
        {
            context.HttpContext.Response.Body.Position = 0;
            using var sr = new StreamReader(context.HttpContext.Response.Body);
            this.ResultText = sr.ReadToEnd();
            if (context.HttpContext.Response.ContentType?.StartsWith("application/json") == true)
            {
                this.ResultJson = JsonNode.Parse(ResultText)!.AsObject();
            }
        }

        public string ResultText { get; }
        public JsonObject? ResultJson { get; }
        public JsonObject? ViewModelJson => ResultJson?["viewModel"] as JsonObject ?? ResultJson?["viewModelDiff"] as JsonObject;
    }

    public class PostbackRequestModel
    {
        public PostbackRequestModel(
            JsonObject viewModel,
            string[] currentPath,
            string command,
            string? controlUniqueId,
            object[] commandArgs,
            string? validationTargetPath
        )
        {
            ViewModel = viewModel;
            CurrentPath = currentPath;
            Command = command;
            ControlUniqueId = controlUniqueId;
            CommandArgs = commandArgs;
            ValidationTargetPath = validationTargetPath;
        }

        [JsonPropertyName("viewModel")]
        public JsonObject ViewModel { get; }
        [JsonPropertyName("currentPath")]
        public string[] CurrentPath { get; }
        [JsonPropertyName("command")]
        public string Command { get; }
        [JsonPropertyName("controlUniqueId")]
        public string? ControlUniqueId { get; }
        [JsonPropertyName("commandArgs")]
        public object[] CommandArgs { get; }
        [JsonPropertyName("validationTargetPath")]
        public string? ValidationTargetPath { get; }
    }

    public class PageRunResult
    {
        public PageRunResult(
            ControlTestHelper testHelper,
            string filePath,
            JsonObject resultJson,
            string outputString,
            string? headResources,
            string? bodyResources,
            IHtmlDocument html,
            (DotvvmControl, DotvvmProperty, ICommandBinding)[] commands,
            DotvvmView view,
            TestDotvvmRequestContext initialContext
        )
        {
            TestHelper = testHelper;
            FilePath = filePath;
            this.ResultJson = resultJson;
            this.OutputString = outputString;
            this.HeadResources = headResources;
            this.BodyResources = bodyResources;
            this.Html = html;
            this.Commands = commands;
            this.View = view;
            InitialContext = initialContext;
        }

        public ControlTestHelper TestHelper { get; }
        public string FilePath { get; }
        public JsonObject ResultJson { get; }
        public JsonObject ViewModelJson => (JsonObject)ResultJson["viewModel"].NotNull();
        public string OutputString { get; }
        public string? HeadResources { get; }
        public string? BodyResources { get; }
        public IHtmlDocument Html { get; }
        public (DotvvmControl control, DotvvmProperty property, ICommandBinding command)[] Commands { get; }
        public DotvvmView View { get; }
        public TestDotvvmRequestContext InitialContext { get; }

        public string FormattedHtml
        {
            get
            {
                var str = new StringWriter();
                Html.ToHtml(str, new PrettyMarkupFormatter() { Indentation = "\t", NewLine = "\n" });
                return str.ToString();
            }
        }

        public (DotvvmControl, DotvvmProperty, ICommandBinding) FindCommand(string text, Func<object?, bool>? viewModel = null)
        {
            viewModel ??= _ => true;
            var filtered =
                this.Commands
                    .Where(c => c.command.GetProperty<OriginalStringBindingProperty>(ErrorHandlingMode.ReturnNull)?.Code?.Trim() == text.Trim()
                             && (viewModel(c.control.DataContext)))
                    .ToArray();
            if (filtered.Length == 0)
                throw new Exception($"Command '{text}' was not found" + (viewModel is null ? "" : $" on viewModel={viewModel}"));
            if (filtered.Length > 1)
                throw new Exception($"Multiple commands '{text}' were found: " + string.Join(", ", filtered.Select(c => c.command)));

            return filtered.Single();
        }

        public async Task<CommandRunResult> RunCommand(CommandBindingExpression command, DotvvmBindableObject control, bool applyChanges = true, object[]? args = null, CultureInfo? culture = null)
        {
                var path = control
                    .GetAllAncestors(true)
                    .Select(a => a.GetDataContextPathFragment())
                    .Where(x => x != null)
                    .Reverse()
                    .ToArray();
                var viewModelJson = this.ViewModelJson; // TODO: process as on client-side
                var r = await this.TestHelper.RunCommand(this.FilePath, new PostbackRequestModel(
                    viewModelJson,
                    path!,
                    command.BindingId,
                    null,
                    args ?? new object[0],
                    KnockoutHelper.GetValidationTargetExpression(control)?.identificationExpression
                ), culture: culture);

                if (applyChanges)
                {
                    JsonUtils.Patch(
                        this.ResultJson["viewModel"]!.AsObject(),
                        r.ViewModelJson!
                    );
                }
                return r;
            }
        public async Task<CommandRunResult> RunCommand(string text, Func<object?, bool>? viewModel = null, bool applyChanges = true, object[]? args = null, CultureInfo? culture = null)
        {
            var (control, property, binding) = FindCommand(text, viewModel);
            if (binding is CommandBindingExpression command)
            {
                return await RunCommand(command, control, applyChanges, args, culture);
            }
            else
            {
                throw new NotSupportedException($"{binding} is not supported.");
            }
        }
    }

    public class FakeBodyResourceLink : BodyResourceLinks
    {
        public string? CapturedHtml { get; private set; }
        protected override void RenderControl(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            var resourceManager = context.ResourceManager;
            if (resourceManager.BodyRendered) return;
            resourceManager.BodyRendered = true;  // set the flag before the resources are rendered, so they can't add more resources to the list during the render

            ResourcesRenderer.RenderResources(context.ResourceManager,
                context.ResourceManager.GetNamedResourcesInOrder().Where(r => r.Resource is TemplateResource),
                writer, context, ResourceRenderPosition.Body);

            var str = new StringWriter();
            var fakeWriter = new HtmlWriter(str, context);
            base.RenderControl(fakeWriter, context);
            this.CapturedHtml = str.ToString();

        }
    }

    public class FakeHeadResourceLink : HeadResourceLinks
    {
        public string? CapturedHtml { get; private set; }
        protected override void RenderControl(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            var str = new StringWriter();
            var fakeWriter = new HtmlWriter(str, context);
            base.RenderControl(fakeWriter, context);
            this.CapturedHtml = str.ToString();
        }
    }
}
