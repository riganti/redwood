using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime.Commands;
using DotVVM.Framework.Runtime.Filters;
using DotVVM.Framework.Security;
using DotVVM.Framework.Utils;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Binding;
using System.Collections.Immutable;
using RecordExceptions;
using System.Text.Json;
using System.Buffers;
using System.Text.Json.Nodes;
using System.Net.NetworkInformation;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotVVM.Framework.Runtime.Tracing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Encodings.Web;

namespace DotVVM.Framework.ViewModel.Serialization
{
    public class DefaultViewModelSerializer : IViewModelSerializer
    {
        private const string GeneralViewModelRecommendations = "Check out general viewModel recommendation at http://www.dotvvm.com/docs/tutorials/basics-viewmodels.";

        public record SerializationException(bool Serialize, Type? ViewModelType, string JsonPath, Exception InnerException): RecordException(InnerException)
        {
            public override string Message => $"Could not {(Serialize ? "" : "de")}serialize viewModel of type { ViewModelType?.Name ?? null }. Serialization failed at property { JsonPath }. {GeneralViewModelRecommendations}";
        }

        private CommandResolver commandResolver = new CommandResolver();

        private readonly IViewModelProtector viewModelProtector;
        private readonly IViewModelSerializationMapper viewModelMapper;
        private readonly IViewModelServerCache viewModelServerCache;
        private readonly IViewModelTypeMetadataSerializer viewModelTypeMetadataSerializer;
        private readonly IDotvvmJsonOptionsProvider jsonOptions;
        private readonly ILogger<DefaultViewModelSerializer>? logger;
        public bool SendDiff { get; set; } = true;


        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultViewModelSerializer"/> class.
        /// </summary>
        public DefaultViewModelSerializer(DotvvmConfiguration configuration, IViewModelProtector protector, IViewModelSerializationMapper serializationMapper, IViewModelServerCache viewModelServerCache, IViewModelTypeMetadataSerializer viewModelTypeMetadataSerializer, IDotvvmJsonOptionsProvider jsonOptions, ILogger<DefaultViewModelSerializer>? logger = null)
        {
            this.viewModelProtector = protector;
            this.viewModelMapper = serializationMapper;
            this.viewModelServerCache = viewModelServerCache;
            this.viewModelTypeMetadataSerializer = viewModelTypeMetadataSerializer;
            this.jsonOptions = jsonOptions;
            this.logger = logger;
        }

        /// <summary>
        /// Serializes the view model.
        /// </summary>
        public string SerializeViewModel(IDotvvmRequestContext context, object? commandResult = null, IEnumerable<(string name, string html)>? postbackUpdatedControls = null, bool serializeNewResources = false)
        {
            var timer = ValueStopwatch.StartNew();
            var utf8json = BuildViewModel(context, commandResult, postbackUpdatedControls, serializeNewResources);

            // context.ViewModelJson ??= new JObject();
            // if (SendDiff && context.ReceivedViewModelJson?["viewModel"] is JObject receivedVM && context.ViewModelJson["viewModel"] is JObject responseVM)
            // {
            //     TODO: revive diffs
            //     context.ViewModelJson["viewModelDiff"] = JsonUtils.Diff(receivedVM, responseVM, false, i => ShouldIncludeProperty(i.TypeId, i.Property));
            //     context.ViewModelJson.Remove("viewModel");
            // }
            var requestTracers = context.Services.GetService<IEnumerable<IRequestTracer>>();
            requestTracers?.TracingSerialized(context, (int)utf8json.Length, utf8json);
            var result = StringUtils.Utf8Decode(utf8json.ToSpan());

            var routeLabel = context.RouteLabel();
            var requestType = context.RequestTypeLabel();
            DotvvmMetrics.ViewModelSerializationTime.Record(timer.ElapsedSeconds, routeLabel, requestType);
            DotvvmMetrics.ViewModelSize.Record(utf8json.Length, routeLabel, requestType);

            return result; // TODO: write Utf-8 directly
        }

        private bool? ShouldIncludeProperty(string typeId, string property)
        {
            var options = viewModelMapper.GetMapByTypeId(typeId).PropertyByClientName(property);

            // IfInPostbackPath and ServerToClient items should be sent every time because we might not have received them from the client and we still remember their value so they look unchanged
            if (!options.TransferToServer || options.TransferToServerOnlyInPath)
            {
                return true;
            }

            // ServerToClientFirstRequest should be ignored
            if (!options.TransferAfterPostback)
            {
                return false;
            }

            return null;
        }

        bool IsPostBack(IDotvvmRequestContext c) => c.RequestType is DotvvmRequestType.Command or DotvvmRequestType.StaticCommand;

        (int vmStart, int vmEnd) WriteViewModelJson(Utf8JsonWriter writer, IDotvvmRequestContext context, DotvvmSerializationState state)
        {
            var converter = jsonOptions.GetRootViewModelConverter(context.ViewModel!.GetType());

            writer.WriteStartObject();
            writer.Flush();
            var vmStart = (int)writer.BytesCommitted; // needed for server side VM cache - we only store the object body, without $csrfToken and $encryptedValues

            converter.WriteUntyped(writer, context.ViewModel, jsonOptions.ViewModelJsonOptions, state, wrapObject: false);

            writer.Flush();
            var vmEnd = (int)writer.BytesCommitted;

            // persist CSRF token
            if (context.CsrfToken is object)
                writer.WriteString("$csrfToken"u8, context.CsrfToken);

            // persist encrypted values
            if (state.WriteEncryptedValues is not null &&
                state.WriteEncryptedValues.ToSpan() is not [] and not [(byte)'{', (byte)'}'])
                writer.WriteBase64String("$encryptedValues"u8, viewModelProtector.Protect(state.WriteEncryptedValues.ToArray(), context));

            writer.WriteEndObject();

            return (vmStart, vmEnd);
        }

        private string? StoreViewModelCache(IDotvvmRequestContext context, MemoryStream buffer, (int, int) viewModelBodyPosition)
        {
            if (!context.Configuration.ExperimentalFeatures.ServerSideViewModelCache.IsEnabledForRoute(context.Route?.RouteName))
                return null;

            var vmBody = buffer.ToMemory()[viewModelBodyPosition.Item1..viewModelBodyPosition.Item2];

            return viewModelServerCache.StoreViewModel(context, new ReadOnlyMemoryStream(vmBody));
        }

        /// <summary>
        /// Builds the view model for the client.
        /// </summary>
        public MemoryStream BuildViewModel(IDotvvmRequestContext context, object? commandResult, IEnumerable<(string name, string html)>? postbackUpdatedControls = null, bool serializeNewResources = false)
        {
            (int, int) viewModelBodyPosition;

            var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions {
                Indented = jsonOptions.ViewModelJsonOptions.WriteIndented,
                Encoder = jsonOptions.ViewModelJsonOptions.Encoder,
                //SkipValidation = true, // for the hack with WriteRawValue
            }))
            {
                using var state = DotvvmSerializationState.Create(context.IsPostBack, context.Services);
                writer.WriteStartObject();

                writer.WritePropertyName("viewModel"u8);
                try
                {
                    viewModelBodyPosition = WriteViewModelJson(writer, context, state);
                }
                catch (Exception ex)
                {
                    writer.Flush();
                    var failurePath = SystemTextJsonUtils.GetFailurePath(buffer.ToSpan());
                    throw new SerializationException(true, context.ViewModel!.GetType(), string.Join("/", failurePath), ex);
                }

                if (StoreViewModelCache(context, buffer, viewModelBodyPosition) is {} viewModelCacheId)
                {
                    writer.WriteString("viewModelCacheId"u8, viewModelCacheId);
                }
                writer.WriteString("url"u8, context.HttpContext?.Request?.Url?.PathAndQuery);
                writer.WriteString("virtualDirectory"u8, context.HttpContext?.Request?.PathBase?.Value?.Trim('/') ?? "");
                if (context.ResultIdFragment != null)
                {
                    writer.WriteString("resultIdFragment"u8, context.ResultIdFragment);
                }

                if (context.RequestType is DotvvmRequestType.Command or DotvvmRequestType.SpaNavigate)
                {
                    writer.WriteString("action"u8, "successfulCommand"u8);
                }
                else
                {
                    writer.WriteStartArray("renderedResources"u8);
                    foreach (var resource in context.ResourceManager.GetNamedResourcesInOrder())
                        writer.WriteStringValue(resource.Name);
                    writer.WriteEndArray();
                }

                if (commandResult != null)
                {
                    writer.WritePropertyName("commandResult"u8);
                    WriteCommandData(commandResult, writer, buffer);
                }
                AddCustomPropertiesIfAny(context, writer, buffer);

                if (postbackUpdatedControls is not null)
                {
                    AddPostBackUpdatedControls(context, writer, postbackUpdatedControls);
                }

                if (serializeNewResources)
                {
                    AddNewResources(context, writer);
                }

                SerializeTypeMetadata(context, writer, state.UsedSerializationMaps);
                writer.WriteEndObject();
            }

            return buffer;
        }

        static ReadOnlySpan<byte> TrimStart(ReadOnlySpan<byte> json)
        {
            while (json.Length > 0 && char.IsWhiteSpace((char)json[0]))
                json = json.Slice(1);
            return json;
        }

        static ReadOnlySpan<byte> TrimEnd(ReadOnlySpan<byte> json)
        {
            while (json.Length > 0 && char.IsWhiteSpace((char)json[json.Length - 1]))
                json = json.Slice(0, json.Length - 1);
            return json;
        }


        private void SerializeTypeMetadata(IDotvvmRequestContext context, Utf8JsonWriter writer, IEnumerable<ViewModelSerializationMap> usedSerializationMaps)
        {
            var knownTypeIds = context.ReceivedViewModelJson?.RootElement.GetPropertyOrNull("knownTypeMetadata"u8)?.EnumerateArray().Select(e => e.GetString()).WhereNotNull().ToHashSet(StringComparer.OrdinalIgnoreCase);
            viewModelTypeMetadataSerializer.SerializeTypeMetadata(usedSerializationMaps, writer, "typeMetadata"u8, knownTypeIds);
        }

        private void AddNewResources(IDotvvmRequestContext context, Utf8JsonWriter writer)
        {
            var renderedResources =
                context.ReceivedViewModelJson
                ?.RootElement.GetPropertyOrNull("renderedResources"u8)
                ?.EnumerateArray().Select(e => e.GetString())
                .WhereNotNull()
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            var newResources = SerializeResources(context, rn => !renderedResources.Contains(rn));
            if (newResources.Count == 0)
                return;

            writer.WriteStartObject("resources"u8);
            foreach (var resource in newResources)
            {
                writer.WriteString(resource.Name, resource.GetRenderedTextCached(context));
            }
            writer.WriteEndObject();
        }

        public ReadOnlyMemory<byte> BuildStaticCommandResponse(IDotvvmRequestContext context, object? result, string[]? knownTypeMetadata = null)
        {
            var timer = ValueStopwatch.StartNew();

            using var state = DotvvmSerializationState.Create(isPostback: true, context.Services);
            var outputBuffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(outputBuffer, new JsonWriterOptions { Indented = jsonOptions.ViewModelJsonOptions.WriteIndented, Encoder = jsonOptions.ViewModelJsonOptions.Encoder }))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("result"u8);
                WriteCommandData(result, writer, outputBuffer);

                viewModelTypeMetadataSerializer.SerializeTypeMetadata(state.UsedSerializationMaps, writer, "typeMetadata"u8, knownTypeMetadata?.ToHashSet());
                AddCustomPropertiesIfAny(context, writer, outputBuffer);
                writer.WriteEndObject();
            }

            DotvvmMetrics.ViewModelSize.Record(outputBuffer.Length, context.RouteLabel(), context.RequestTypeLabel());
            DotvvmMetrics.ViewModelSerializationTime.Record(timer.ElapsedSeconds, context.RouteLabel(), context.RequestTypeLabel());
            return outputBuffer.ToMemory();
        }

        private void AddCustomPropertiesIfAny(IDotvvmRequestContext context, Utf8JsonWriter writer, MemoryStream outputBuffer)
        {
            if (context.CustomResponseProperties.Properties.Count > 0)
            {
                writer.WriteStartObject("customProperties"u8);
                foreach (var prop in context.CustomResponseProperties.Properties)
                {
                    writer.WritePropertyName(prop.Key);
                    WriteCommandData(prop.Value, writer, outputBuffer);
                }
                writer.WriteEndObject();
            }
            context.CustomResponseProperties.PropertiesSerialized = true;
        }

        private void WriteCommandData(object? data, Utf8JsonWriter writer, MemoryStream outputBuffer)
        {
            Debug.Assert(DotvvmSerializationState.Current is {});
            try
            {
                JsonSerializer.Serialize(writer, data, jsonOptions.ViewModelJsonOptions);
            }
            catch (Exception ex)
            {
                writer.Flush();
                var path = SystemTextJsonUtils.GetFailurePath(outputBuffer.ToSpan());
                throw new SerializationException(true, data?.GetType(), string.Join("/", path), ex);
            }
        }

        private List<NamedResource> SerializeResources(IDotvvmRequestContext context, Func<string, bool> predicate)
        {
            var resources = new List<NamedResource>();
            var manager = context.ResourceManager;
            foreach (var resource in manager.GetNamedResourcesInOrder())
            {
                if (predicate(resource.Name))
                {
                    resources.Add(resource);
                }
            }

            // propagate warnings to JS Console
            var warningScript = BodyResourceLinks.RenderWarnings(context);
            if (warningScript != "")
            {
                var name = "warnings" + Guid.NewGuid();
                resources.Add(new NamedResource(name, new InlineScriptResource(warningScript)));
            }
            return resources;
        }

        /// <summary>
        /// Serializes the redirect action.
        /// </summary>
        /// <return>UTF-8 encoded JSON response</return>
        public static byte[] GenerateRedirectActionResponse(string url, bool replace, bool allowSpa, string? downloadName)
        {
            ThrowHelpers.ArgumentNull(url);
            // create result object
            var result = new MemoryStream();
            using (var w = new Utf8JsonWriter(result, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                w.WriteStartObject();
                w.WriteString("url"u8, url);
                w.WriteString("action"u8, "redirect"u8);
                if (replace)
                    w.WriteBoolean("replace"u8, true);
                if (allowSpa)
                    w.WriteBoolean("allowSpa"u8, true);
                if (downloadName is not null)
                    w.WriteString("download"u8, downloadName);
                w.WriteEndObject();
            }
            return result.ToArray();
        }

        /// <summary>
        /// Serializes the missing cached viewmodel action.
        /// </summary>
        internal static string GenerateMissingCachedViewModelResponse()
        {
            return """{"action":"viewModelNotCached"}""";
        }

        /// <summary>
        /// Serializes the validation errors in case the viewmodel was not valid.
        /// </summary>
        public byte[] SerializeModelState(IDotvvmRequestContext context)
        {
            // create result object
            using var state = DotvvmSerializationState.Create(isPostback: true, context.Services);
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                modelState = context.ModelState.Errors,
                action = "validationErrors"
            }, jsonOptions.PlainJsonOptions);
        }


        /// <summary>
        /// Populates the view model from the data received from the request.
        /// </summary>
        /// <returns></returns>
        public void PopulateViewModel(IDotvvmRequestContext context, ReadOnlyMemory<byte> serializedPostData)
        {
            // get properties
            var vmDocument = context.ReceivedViewModelJson = JsonDocument.Parse(serializedPostData);
            var root = vmDocument.RootElement;
            JsonElement viewModelElement;
            ReadOnlyMemory<byte>? cachedViewModel = null;
            if (root.GetPropertyOrNull("viewModelCacheId"u8)?.GetString() is {} viewModelCacheId)
            {
                if (!context.Configuration.ExperimentalFeatures.ServerSideViewModelCache.IsEnabledForRoute(context.Route?.RouteName))
                {
                    throw new InvalidOperationException("The server-side viewmodel caching is not enabled for the current route!");
                }

                viewModelElement = root.GetProperty("viewModelDiff"u8);
                cachedViewModel = viewModelServerCache.TryRestoreViewModel(context, viewModelCacheId, viewModelElement);
            }
            else
            {
                viewModelElement = root.GetProperty("viewModel"u8);
            }

            // load CSRF token
            context.CsrfToken = viewModelElement.GetPropertyOrNull("$csrfToken"u8)?.GetString();

            JsonObject readEncryptedValues;
            if (viewModelElement.TryGetProperty("$encryptedValues"u8, out var evJson) && evJson.GetBytesFromBase64() is {} encryptedValuesBytes)
            {
                // load encrypted values
                readEncryptedValues = JsonNode.Parse(viewModelProtector.Unprotect(encryptedValuesBytes, context))!.AsObject();
                readEncryptedValues = new JsonObject([ new("0", readEncryptedValues) ]);
            }
            else
            {
                readEncryptedValues = new JsonObject();
            }

            using var state = DotvvmSerializationState.Create(isPostback: true, context.Services, readEncryptedValues: readEncryptedValues);

            // get validation path
            context.ModelState.ValidationTargetPath = root.GetPropertyOrNull("validationTargetPath"u8)?.GetString();

            // populate the ViewModel
            var reader = new Utf8JsonReader((cachedViewModel ?? serializedPostData).Span);
            reader.Read();
            try
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new Exception("The JSON must start with an object.");

                if (cachedViewModel is null)
                {
                    // skip to the "viewModel" property
                    reader.Read();
                    while (reader.TokenType == JsonTokenType.PropertyName && !reader.ValueTextEquals("viewModel"u8))
                    {
                        reader.Skip();
                        reader.Read();
                    }
                    Debug.Assert(reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.EndObject);
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new Exception("The JSON must contain a property \"viewModel\".");
                    reader.Read();
                    if (reader.TokenType != JsonTokenType.StartObject)
                        throw new Exception("Property \"viewModel\" must be an object.");
                }

                Debug.Assert(context.ViewModel is not null);

                var converter = jsonOptions.GetRootViewModelConverter(context.ViewModel.GetType());
                var newVM = converter.PopulateUntyped(ref reader, context.ViewModel.GetType(), context.ViewModel, jsonOptions.ViewModelJsonOptions, state);

                if (newVM != context.ViewModel)
                {
                    logger?.LogInformation("Instance of root view model {ViewModelType} was replaced during deserialization.", context.ViewModel!.GetType());
                    context.ViewModel = newVM;
                    if (context.View is not null)
                        context.View.DataContext = newVM;
                }
            }
            catch (Exception ex)
            {
                var documentSlice = (cachedViewModel ?? serializedPostData).Span.Slice(0, (int)reader.BytesConsumed);
                var path = SystemTextJsonUtils.GetFailurePath(documentSlice);
                throw new SerializationException(false, context.ViewModel?.GetType(), string.Join("/", path), ex);
            }
        }

        /// <summary>
        /// Resolves the command for the specified post data.
        /// </summary>
        public ActionInfo ResolveCommand(IDotvvmRequestContext context, DotvvmView view)
        {
            // get properties
            var data = context.ReceivedViewModelJson?.RootElement ?? throw new NotSupportedException("Could not find ReceivedViewModelJson in request context.");
            var path = data.GetProperty("currentPath"u8).EnumerateArray().Select(e => e.GetString().NotNull()).ToArray();
            var command = data.GetProperty("command"u8).GetString();
            var controlUniqueId = data.GetPropertyOrNull("controlUniqueId"u8)?.GetString();
            var args = data.TryGetProperty("commandArgs"u8, out var argsJson) ?
                       argsJson.EnumerateArray().Select(a => (Func<Type, object?>)(t => {
                          using var state = DotvvmSerializationState.Create(isPostback: true, context.Services, readEncryptedValues: new JsonObject());
                          return JsonSerializer.Deserialize(a, t, jsonOptions.ViewModelJsonOptions);
                       })).ToArray() :
                       new Func<Type, object?>[0];

            // empty command
            if (string.IsNullOrEmpty(command))
                throw new Exception("Command is not specified!");

            // find the command target
            if (!string.IsNullOrEmpty(controlUniqueId))
            {
                var target = view.FindControlByUniqueId(controlUniqueId);
                if (target == null)
                {
                    var markupControls =
                        view.GetAllDescendants()
                            .OfType<DotvvmMarkupControl>()
                            .Where(c => c.GetAllDescendants(cc => cc is not DotvvmMarkupControl)
                                         .Any(cc => cc.Properties.Values
                                                      .Any(value => value is Binding.Expressions.CommandBindingExpression cb && cb.BindingId == command)));
                    throw new Exception($"The control with ID '{controlUniqueId}' was not found! Existing markup controls with this command are: {string.Join(", ", markupControls.Select(c => c.GetDotvvmUniqueId().ToString()).OrderBy(s => s, StringComparer.Ordinal))}");
                }
                return commandResolver.GetFunction(target, view, context, path!, command, args);
            }
            else
            {
                return commandResolver.GetFunction(view, context, path!, command, args);
            }
        }

        /// <summary>
        /// Adds the post back updated controls.
        /// </summary>
        private void AddPostBackUpdatedControls(IDotvvmRequestContext context, Utf8JsonWriter writer, IEnumerable<(string name, string html)> postBackUpdatedControls)
        {
            var first = true;
            foreach (var (controlId, html) in postBackUpdatedControls)
            {
                if (first)
                {
                    writer.WriteStartObject("updatedControls"u8);
                    first = false;
                }
                writer.WriteString(controlId, html);
            }

            if (!first)
            {
                writer.WriteEndObject();
            }
        }
    }
}
