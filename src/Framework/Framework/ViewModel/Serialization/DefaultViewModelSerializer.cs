﻿using System;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Binding;
using System.Collections.Immutable;
using RecordExceptions;

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

        public bool SendDiff { get; set; } = true;

        public Formatting JsonFormatting { get; set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultViewModelSerializer"/> class.
        /// </summary>
        public DefaultViewModelSerializer(DotvvmConfiguration configuration, IViewModelProtector protector, IViewModelSerializationMapper serializationMapper, IViewModelServerCache viewModelServerCache, IViewModelTypeMetadataSerializer viewModelTypeMetadataSerializer)
        {
            this.viewModelProtector = protector;
            this.JsonFormatting = configuration.Debug ? Formatting.Indented : Formatting.None;
            this.viewModelMapper = serializationMapper;
            this.viewModelServerCache = viewModelServerCache;
            this.viewModelTypeMetadataSerializer = viewModelTypeMetadataSerializer;
        }

        /// <summary>
        /// Serializes the view model.
        /// </summary>
        public string SerializeViewModel(IDotvvmRequestContext context)
        {
            var timer = ValueStopwatch.StartNew();

            context.ViewModelJson ??= new JObject();
            if (SendDiff && context.ReceivedViewModelJson != null && context.ViewModelJson["viewModel"] != null)
            {
                context.ViewModelJson["viewModelDiff"] = JsonUtils.Diff((JObject)context.ReceivedViewModelJson["viewModel"], (JObject)context.ViewModelJson["viewModel"], false, i => ShouldIncludeProperty(i.TypeId, i.Property));
                context.ViewModelJson.Remove("viewModel");
            }
            var result = context.ViewModelJson.ToString(JsonFormatting);

            context.HttpContext.SetItem("dotvvm-viewmodel-size-bytes", result.Length); // for PerformanceWarningTracer
            var routeLabel = context.RouteLabel();
            var requestType = context.RequestTypeLabel();
            DotvvmMetrics.ViewModelStringificationTime.Record(timer.ElapsedSeconds, routeLabel, requestType);
            DotvvmMetrics.ViewModelSize.Record(result.Length, routeLabel, requestType);

            return result;
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

        /// <summary>
        /// Builds the view model for the client.
        /// </summary>
        public void BuildViewModel(IDotvvmRequestContext context, object? commandResult)
        {
            var timer = ValueStopwatch.StartNew();
            // serialize the ViewModel
            var serializer = CreateJsonSerializer();
            var viewModelConverter = new ViewModelJsonConverter(IsPostBack(context), viewModelMapper, context.Services);
            serializer.Converters.Add(viewModelConverter);
            var writer = new JTokenWriter();
            try
            {
                serializer.Serialize(writer, context.ViewModel);
            }
            catch (Exception ex)
            {
                throw new SerializationException(true, context.ViewModel!.GetType(), writer.Path, ex);
            }
            var viewModelToken = writer.Token;

            string? viewModelCacheId = null;
            if (context.Configuration.ExperimentalFeatures.ServerSideViewModelCache.IsEnabledForRoute(context.Route?.RouteName))
            {
                viewModelCacheId = viewModelServerCache.StoreViewModel(context, (JObject)viewModelToken);
            }

            // persist CSRF token
            if (context.CsrfToken is object)
                viewModelToken["$csrfToken"] = context.CsrfToken;

            // persist encrypted values
            if (viewModelConverter.EncryptedValues.Count > 0)
                viewModelToken["$encryptedValues"] = viewModelProtector.Protect(viewModelConverter.EncryptedValues.ToString(Formatting.None), context);

            // create result object
            var result = new JObject();
            result["viewModel"] = viewModelToken;
            if (viewModelCacheId != null)
            {
                result["viewModelCacheId"] = viewModelCacheId;
            }
            result["url"] = context.HttpContext?.Request?.Url?.PathAndQuery;
            result["virtualDirectory"] = context.HttpContext?.Request?.PathBase?.Value?.Trim('/') ?? "";
            if (context.ResultIdFragment != null)
            {
                result["resultIdFragment"] = context.ResultIdFragment;
            }

            if (context.RequestType is DotvvmRequestType.Command or DotvvmRequestType.SpaNavigate)
            {
                result["action"] = "successfulCommand";
            }
            else
            {
                result["renderedResources"] = JArray.FromObject(context.ResourceManager.GetNamedResourcesInOrder().Select(r => r.Name));
            }

            if (commandResult != null) result["commandResult"] = WriteCommandData(commandResult, serializer, "the command result");
            AddCustomPropertiesIfAny(context, serializer, result);

            result["typeMetadata"] = SerializeTypeMetadata(context, viewModelConverter);

            context.ViewModelJson = result;

            DotvvmMetrics.ViewModelSerializationTime.Record(timer.ElapsedSeconds, context.RouteLabel(), context.RequestTypeLabel());
        }

        private JObject SerializeTypeMetadata(IDotvvmRequestContext context, ViewModelJsonConverter viewModelJsonConverter)
        {
            var knownTypeIds = context.ReceivedViewModelJson?["knownTypeMetadata"]?.Values<string>().ToImmutableHashSet();
            return viewModelTypeMetadataSerializer.SerializeTypeMetadata(viewModelJsonConverter.UsedSerializationMaps, knownTypeIds);
        }

        public void AddNewResources(IDotvvmRequestContext context)
        {
            var renderedResources = new HashSet<string>(context.ReceivedViewModelJson?["renderedResources"]?.Values<string>() ?? new string[] { });
            var resourcesObject = BuildResourcesJson(context, rn => !renderedResources.Contains(rn));
            if (resourcesObject.Count > 0)
                context.ViewModelJson!["resources"] = resourcesObject;
        }

        public string BuildStaticCommandResponse(IDotvvmRequestContext context, object? result, string[]? knownTypeMetadata = null)
        {
            var timer = ValueStopwatch.StartNew();

            var serializer = CreateJsonSerializer();
            var viewModelConverter = new ViewModelJsonConverter(isPostback: true, viewModelMapper, context.Services);
            serializer.Converters.Add(viewModelConverter);
            var response = new JObject();
            response["result"] = WriteCommandData(result, serializer, "the static command result");

            var typeMetadata = viewModelTypeMetadataSerializer.SerializeTypeMetadata(viewModelConverter.UsedSerializationMaps, knownTypeMetadata?.ToHashSet());
            if (typeMetadata.Count > 0)
            {
                response["typeMetadata"] = typeMetadata;
            }
            AddCustomPropertiesIfAny(context, serializer, response);
            var resultJson = response.ToString(JsonFormatting);

            DotvvmMetrics.ViewModelSize.Record(resultJson.Length, context.RouteLabel(), context.RequestTypeLabel());
            DotvvmMetrics.ViewModelSerializationTime.Record(timer.ElapsedSeconds, context.RouteLabel(), context.RequestTypeLabel());
            return resultJson;
        }

        private static void AddCustomPropertiesIfAny(IDotvvmRequestContext context, JsonSerializer serializer, JObject response)
        {
            if (context.CustomResponseProperties.Properties.Count > 0)
            {
                var props = context.CustomResponseProperties.Properties
                                .Select(s => new JProperty(s.Key, WriteCommandData(s.Value, serializer, $"custom properties['{s.Key}']")))
                                .ToArray();
                response["customProperties"] = new JObject(props);
            }
            context.CustomResponseProperties.PropertiesSerialized = true;
        }

        private static JToken WriteCommandData(object? data, JsonSerializer serializer, string description)
        {
            var writer = new JTokenWriter();
            try
            {
                serializer.Serialize(writer, data);
            }
            catch (Exception ex)
            {
                throw new SerializationException(true, data?.GetType(), writer.Path, ex);
            }
            return writer.Token;
        }

        protected virtual JsonSerializer CreateJsonSerializer() => DefaultSerializerSettingsProvider.Instance.Settings.Apply(JsonSerializer.Create);

        public JObject BuildResourcesJson(IDotvvmRequestContext context, Func<string, bool> predicate)
        {
            var manager = context.ResourceManager;
            var resourceObj = new JObject();
            foreach (var resource in manager.GetNamedResourcesInOrder())
            {
                if (predicate(resource.Name))
                {
                    resourceObj[resource.Name] = JValue.CreateString(resource.GetRenderedTextCached(context));
                }
            }

            // propagate warnings to JS Console
            var warningScript = BodyResourceLinks.RenderWarnings(context);
            if (warningScript != "")
            {
                var name = "warnings" + Guid.NewGuid();
                var resource = new NamedResource(name, new InlineScriptResource(warningScript));

                resourceObj[resource.Name] = JValue.CreateString(resource.GetRenderedTextCached(context));
            }
            return resourceObj;
        }

        /// <summary>
        /// Serializes the redirect action.
        /// </summary>
        public static string GenerateRedirectActionResponse(string url, bool replace, bool allowSpa)
        {
            // create result object
            var result = new JObject();
            result["url"] = url;
            result["action"] = "redirect";
            if (replace) result["replace"] = true;
            if (allowSpa) result["allowSpa"] = true;
            return result.ToString(Formatting.None);
        }

        /// <summary>
        /// Serializes the missing cached viewmodel action.
        /// </summary>
        internal static string GenerateMissingCachedViewModelResponse()
        {
            // create result object
            var result = new JObject();
            result["action"] = "viewModelNotCached";
            return result.ToString(Formatting.None);
        }

        /// <summary>
        /// Serializes the validation errors in case the viewmodel was not valid.
        /// </summary>
        public string SerializeModelState(IDotvvmRequestContext context)
        {
            // create result object
            var result = new JObject();
            result["modelState"] = JArray.FromObject(context.ModelState.Errors);
            result["action"] = "validationErrors";
            return result.ToString(JsonFormatting);
        }


        /// <summary>
        /// Populates the view model from the data received from the request.
        /// </summary>
        /// <returns></returns>
        public void PopulateViewModel(IDotvvmRequestContext context, string serializedPostData)
        {
            // get properties
            var data = context.ReceivedViewModelJson = JObject.Parse(serializedPostData);
            JObject viewModelToken;
            if (data["viewModelCacheId"] != null)
            {
                if (!context.Configuration.ExperimentalFeatures.ServerSideViewModelCache.IsEnabledForRoute(context.Route?.RouteName))
                {
                    throw new InvalidOperationException("The server-side viewmodel caching is not enabled for the current route!");
                }

                viewModelToken = viewModelServerCache.TryRestoreViewModel(context, (string)data["viewModelCacheId"], (JObject)data["viewModelDiff"]);
                data["viewModel"] = viewModelToken;
            }
            else
            {
                viewModelToken = (JObject)data["viewModel"];
            }

            // load CSRF token
            context.CsrfToken = viewModelToken["$csrfToken"]?.Value<string>();

            ViewModelJsonConverter viewModelConverter;
            if (viewModelToken["$encryptedValues"] != null)
            {
                // load encrypted values
                var encryptedValuesString = viewModelToken["$encryptedValues"].Value<string>();
                viewModelConverter = new ViewModelJsonConverter(IsPostBack(context), viewModelMapper, context.Services, JObject.Parse(viewModelProtector.Unprotect(encryptedValuesString, context)));
            }
            else viewModelConverter = new ViewModelJsonConverter(IsPostBack(context), viewModelMapper, context.Services);

            // get validation path
            context.ModelState.ValidationTargetPath = (string)data["validationTargetPath"];

            // populate the ViewModel
            var serializer = CreateJsonSerializer();
            serializer.Converters.Add(viewModelConverter);
            var reader = viewModelToken.CreateReader();
            try
            {
                var newVM = viewModelConverter.Populate(reader, serializer, context.ViewModel!);
                if (newVM != context.ViewModel)
                {
                    context.ViewModel = newVM;
                    if (context.View is not null)
                        context.View.DataContext = newVM;
                }
            }
            catch (Exception ex)
            {
                throw new SerializationException(false, context.ViewModel?.GetType(), reader.Path, ex);
            }
        }

        /// <summary>
        /// Resolves the command for the specified post data.
        /// </summary>
        public ActionInfo? ResolveCommand(IDotvvmRequestContext context, DotvvmView view)
        {
            // get properties
            var data = context.ReceivedViewModelJson ?? throw new NotSupportedException("Could not find ReceivedViewModelJson in request context.");
            var path = data["currentPath"].Values<string>().ToArray();
            var command = data["command"].Value<string>();
            var controlUniqueId = data["controlUniqueId"]?.Value<string>();
            var args = data["commandArgs"] is JArray argsJson ?
                       argsJson.Select(a => (Func<Type, object>)(t => a.ToObject(t))).ToArray() :
                       new Func<Type, object>[0];

            // empty command
            if (string.IsNullOrEmpty(command)) return null;

            // find the command target
            if (!string.IsNullOrEmpty(controlUniqueId))
            {
                var target = view.FindControlByUniqueId(controlUniqueId);
                if (target == null)
                {
                    throw new Exception(string.Format("The control with ID '{0}' was not found!", controlUniqueId));
                }
                return commandResolver.GetFunction(target, view, context, path, command, args);
            }
            else
            {
                return commandResolver.GetFunction(view, context, path, command, args);
            }
        }

        /// <summary>
        /// Adds the post back updated controls.
        /// </summary>
        public void AddPostBackUpdatedControls(IDotvvmRequestContext context, IEnumerable<(string name, string html)> postBackUpdatedControls)
        {
            var result = new JObject();
            foreach (var (controlId, html) in postBackUpdatedControls)
            {
                result[controlId] = JValue.CreateString(html);
            }

            if (context.ViewModelJson == null)
            {
                context.ViewModelJson = new JObject();
            }

            context.ViewModelJson["updatedControls"] = result;
        }
    }
}
