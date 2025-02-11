using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.Runtime.Commands;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Hosting.ErrorPages
{
    public class ErrorFormatter
    {
        public ExceptionModel LoadException(Exception exception, StackFrameModel[]? existingTrace = null, Func<Exception, StackFrame[]?>? stackFrameGetter = null,
            Func<StackFrame, string?>? methodFormatter = null)
        {
            stackFrameGetter = stackFrameGetter ?? (ex => new StackTrace(ex, true).GetFrames());

            var frames = stackFrameGetter(exception) ?? new StackFrame[0];
            var stack = new List<StackFrameModel>();
            bool skipping = existingTrace != null;
            for (int i = frames.Length - 1; i >= 0; i--)
            {
                var f = frames[i];
                if (skipping && existingTrace!.Length > i && f.GetMethod() == existingTrace[i].Method) continue;
                skipping = false;

                stack.Add(AddMoreInfo(new StackFrameModel(
                    f.GetMethod(),
                    methodFormatter?.Invoke(f),
                    LoadSourcePiece(f.GetFileName(), f.GetFileLineNumber(),
                        errorColumn: f.GetFileColumnNumber()),
                    null
                )));

            }
            //Adding additional information to ExceptionModel from InfoLoaders and InfoCollectionLoader
            var additionalInfos = InfoLoaders.Select(info => info(exception))
                .Where(info => info != null && info.Objects != null).ToArray()
                .Union(InfoCollectionLoader.Select(infoCollection => infoCollection(exception))
                    .SelectMany(infoCollection => infoCollection ?? Enumerable.Empty<ExceptionAdditionalInfo>())
                    .Where(info => info != null && info.Objects != null).ToArray())
                .ToArray();

            stack.Reverse();

            var m = new ExceptionModel(
                exception.GetType().FullName ?? "Unknown exception",
                exception.Message,
                stack.ToArray(),
                exception,
                additionalInfos!
            );
            if (exception.InnerException != null) m.InnerException = LoadException(exception.InnerException, m.Stack, stackFrameGetter, methodFormatter);
            return m;
        }

        private StackFrameModel AddMoreInfo(StackFrameModel frame)
        {
            try
            {
                frame.MoreInfo = FrameInfoLoaders.Select(f => f(frame)).Where(f => f != null).ToArray()!;
            }
            catch
            {
                frame.MoreInfo = new IFrameMoreInfo[0];
            }

            return frame;
        }

        public List<Func<StackFrameModel, IFrameMoreInfo?>> FrameInfoLoaders =
            new List<Func<StackFrameModel, IFrameMoreInfo?>>()
            {
                CreateDotvvmDocsLink,
                CreateReferenceSourceLink,
                CreateGithubLink
            };

        protected static IFrameMoreInfo? CreateDotvvmDocsLink(StackFrameModel frame)
        {
            const string DotvvmThumb = "https://www.dotvvm.com/wwwroot/Images/favicons/favicon-16x16.png";
            var type = frame.Method?.DeclaringType;
            if (type == null) return null;
            while (type.DeclaringType != null) type = type.DeclaringType;
            if (type.Namespace == "DotVVM.Framework.Controls")
            {
                const string BuiltinControlsDocs = "https://dotvvm.com/docs/controls/builtin/";
                var url = BuiltinControlsDocs + type.Name;
                return FrameMoreInfo.CreateThumbLink(url, DotvvmThumb);
            }
            return null;
        }

        protected static IFrameMoreInfo? CreateGithubLink(StackFrameModel frame)
        {
            const string GithubUrl = @"https://github.com/riganti/dotvvm/blob/main/";
            const string Octocat = @"https://github.githubassets.com/favicons/favicon.png";
            if (frame.Method?.DeclaringType?.Assembly == typeof(ErrorFormatter).Assembly)
            {
                var fileName = frame.At?.FileName?.Replace('\\', '/').TrimStart('/');
                // dotvvm github
                if (!string.IsNullOrEmpty(fileName))
                {
                    var urlFileName =
                        fileName.Substring(
                            fileName.LastIndexOf("src/Framework", StringComparison.Ordinal));
                    var url = GithubUrl + urlFileName + "#L" + frame.At!.LineNumber;
                    return FrameMoreInfo.CreateThumbLink(url, Octocat);
                }
                else
                {
                    // guess by method name
                    var urlFileName = frame.Method.DeclaringType.FullName!.Replace("DotVVM.Framework", "")
                        .Replace('.', '/');
                    if (urlFileName.Contains("+"))
                        urlFileName = urlFileName.Remove(urlFileName.IndexOf('+')); // remove nested class
                    var url = GithubUrl + "src/Framework/Framework" + urlFileName + ".cs";
                    return FrameMoreInfo.CreateThumbLink(url, Octocat);
                }
            }
            return null;
        }

        static HashSet<string> ReferenceSourceAssemblies = new HashSet<string>
        {
            "mscorlib",
            "PresentationFramework",
            "System.Web",
            "System",
            "System.Windows.Forms",
            "PresentationCore",
            "System.ServiceModel",
            "System.Data",
            "System.Data.Entity",
            "System.Core",
            "System.Xml",
            "System.Activities",
            "WindowsBase",
            "System.Activities.Presentation",
            "System.Drawing",
            "Microsoft.VisualBasic",
            "System.IdentityModel",
            "System.Web.Extensions",
            "System.Runtime.Serialization",
            "System.Workflow.ComponentModel",
            "System.Data.SqlXml",
            "System.Data.Linq",
            "UIAutomationClientsideProviders",
            "PresentationBuildTasks",
            "System.Configuration",
            "System.Management",
            "System.Data.Services",
            "System.Workflow.Activities",
            "System.Management.Automation",
            "Microsoft.CSharp",
            "System.Web.Mobile",
            "System.Web.Services",
            "System.Security",
            "System.Data.Services.Client",
            "System.ServiceModel.Web",
            "System.ServiceModel.Activities",
            "System.Workflow.Runtime",
            "System.ComponentModel.DataAnnotations",
            "System.Activities.Core.Presentation",
            "System.Net.Http",
            "System.Design",
            "Microsoft.Build.Tasks.v4.0",
            "UIAutomationClient",
            "System.Runtime.Remoting",
            "Microsoft.JSc",
            "System.Private.CoreLib"
        };

        protected static IFrameMoreInfo? CreateReferenceSourceLink(StackFrameModel frame)
        {
            const string DotNetIcon = "http://referencesource.microsoft.com/favicon.ico";
            const string SourceUrl = "http://referencesource.microsoft.com/";
            if (frame.Method?.DeclaringType?.Assembly != null &&
                ReferenceSourceAssemblies.Contains(frame.Method.DeclaringType.Assembly.GetName().Name ?? ""))
            {
                if (!String.IsNullOrEmpty(frame.At?.FileName))
                {
                    throw new NotImplementedException();
                }
                else
                {
                    if (frame.Method.DeclaringType.IsGenericType)
                    {
                        var url = SourceUrl + "#q=" +
                                  WebUtility.HtmlEncode(
                                      GetGenericFullName(frame.Method.DeclaringType).Replace('+', '.'));
                        return FrameMoreInfo.CreateThumbLink(url, DotNetIcon);
                    }
                    else
                    {
                        var url = SourceUrl + "#q=" +
                                  WebUtility.HtmlEncode(frame.Method.DeclaringType.FullName!.Replace('+', '.') + "." +
                                                        frame.Method.Name);
                        return FrameMoreInfo.CreateThumbLink(url, DotNetIcon);
                    }
                }
            }
            return null;
        }

        protected static string GetGenericFullName(Type type)
        {
            var name = type.FullName ?? type.Name;
            if (!type.IsGenericType) return name;

            name = name.Remove(name.IndexOf("`", StringComparison.Ordinal));
            var typeInfo = type.GetTypeInfo();

            StringBuilder sb = new StringBuilder(name);
            sb.Append("<");
            for (int i = 0; i < typeInfo.GenericTypeParameters.Length; i++)
            {
                sb.Append(typeInfo.GenericTypeParameters[i].Name);
                if (i != typeInfo.GenericTypeParameters.Length - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(">");

            return sb.ToString();
        }

        public List<Func<Exception, IEnumerable<ExceptionAdditionalInfo>?>> InfoCollectionLoader = new();

        public List<Func<Exception, ExceptionAdditionalInfo?>> InfoLoaders = new();

        public void AddInfoLoader<T>(Func<T, ExceptionAdditionalInfo?> func)
        {
            InfoLoaders.Add(e => {
                if (e is T t) return func(t);
                else return null;
            });
        }

        /// <summary>
        /// Adds a function to InfoCollectionLoader that returns a collection of ExceptionAdditionalInfo
        /// </summary>
        /// <typeparam name="T">type of the exception</typeparam>
        /// <param name="func">function that returns a collection of ExceptionAdditionalInfo</param>
        public void AddInfoCollectionLoader<T>(Func<T, IEnumerable<ExceptionAdditionalInfo>?> func)
            where T : Exception
        {
            InfoCollectionLoader.Add(e => {
                if (e is T) return func((T)e);
                else return null;
            });
        }

        public static SourceModel LoadSourcePiece(string? fileName, int lineNumber,
            int additionalLineCount = 7,
            int errorColumn = 0,
            int errorLength = 0)
        {
            var result = new SourceModel {
                FileName = fileName,
                LineNumber = lineNumber,
                ErrorColumn = errorColumn,
                ErrorLength = errorLength
            };

            if (!string.IsNullOrEmpty(fileName))
            {
                try
                {
                    return SourcePieceFromSource(fileName, File.ReadAllText(fileName), lineNumber, additionalLineCount, errorColumn, errorLength);
                }
                catch
                {
                    result.LoadFailed = true;
                }
            }
            return result;
        }

        public static SourceModel LoadSourcePiece(MarkupFile? file, int lineNumber,
            int additionalLineCount = 7,
            int errorColumn = 0,
            int errorLength = 0)
        {
            var result = new SourceModel {
                FileName = file?.FileName,
                LineNumber = lineNumber,
                ErrorColumn = errorColumn,
                ErrorLength = errorLength
            };

            if (file != null)
            {
                try
                {
                    return SourcePieceFromSource(file.FileName, file.ReadContent(), lineNumber, additionalLineCount, errorColumn, errorLength);
                }
                catch
                {
                    result.LoadFailed = true;
                }
            }
            return result;
        }


        public static SourceModel SourcePieceFromSource(string? fileName, string sourceCode, int lineNumber,
            int additionalLineCount = 7,
            int errorColumn = 0,
            int errorLength = 0)
        {
            var result = new SourceModel {
                FileName = fileName,
                LineNumber = lineNumber,
                ErrorColumn = errorColumn,
                ErrorLength = errorLength
            };
            var lines = sourceCode.Split('\n');
            if (lineNumber >= 0)
            {
                result.CurrentLine = lines[Math.Max(0, Math.Min(lines.Length, lineNumber) - 1)];
                result.PreLines = lines.Skip(lineNumber - additionalLineCount)
                    .TakeWhile(l => l != result.CurrentLine).ToArray();
            }
            else additionalLineCount = 30;
            result.PostLines = lines.Skip(lineNumber).Take(additionalLineCount).ToArray();
            return result;
        }

        public List<Func<Exception, IHttpContext, IErrorSectionFormatter?>> Formatters = new();

        public string ErrorHtml(Exception exception, IHttpContext context)
        {
            var template = new ErrorPageTemplate(
                formatters: Formatters
                    .Select(f => f(exception, context))
                    .Concat(context.GetEnvironmentTabs().Select(o => new DictionarySection<string, object>(o.Item1, "env_" + o.Item1.GetHashCode(), o.Item2)))
                    .Where(t => t != null)
                    .ToArray()!,
                errorCode: context.Response.StatusCode,
                errorDescription: "Unhandled exception occurred",
                summary: exception.GetType().FullName + ": " + exception.Message.LimitLength(600),
                context: DotvvmRequestContext.TryGetCurrent(context),
                exception: exception);

            return template.TransformText();
        }

        static (string name, object? value) StripBindingProperty(string name, object value)
        {
            var t = value.GetType();
            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (props.Length != 1)
                return (name, value);

            return (name + "." + props[0].Name, props[0].GetValue(value));
        }

        private static ExceptionModel LoadDemystifiedException(ErrorFormatter formatter, Exception exception)
        {
            return formatter.LoadException(exception,
                stackFrameGetter: ex => {
                    var rawStackTrace = new StackTrace(ex, true).GetFrames();
                    if (rawStackTrace == null) return null; // demystifier throws in these cases
                    try
                    {
                        return new EnhancedStackTrace(ex).GetFrames();
                    }
                    catch
                    {
                        return rawStackTrace;
                    }
                },
                methodFormatter: f => (f as EnhancedStackFrame)?.MethodInfo?.ToString());
        }


        public static ErrorFormatter CreateDefault() => CreateDefault(null);

        public static ErrorFormatter CreateDefault(DotvvmConfiguration? config)
        {
            var f = new ErrorFormatter();
            f.Formatters.Add((e, o) => DotvvmMarkupErrorSection.Create(e));
            f.Formatters.Add((e, o) => {
                try
                {
                    return new ExceptionSectionFormatter(LoadDemystifiedException(f, e));
                }
                catch
                {
                    return null; // just ignore errors from the demystifier
                }
            });

            f.Formatters.Add((e, o) => new ExceptionSectionFormatter(f.LoadException(e), "Raw Stack Trace", "raw_stack_trace"));
            f.Formatters.Add((e, o) => {
                var b = e.AllInnerExceptions().OfType<IDotvvmException>().Select(a => a.RelatedBinding).OfType<ICloneableBinding>().FirstOrDefault();
                if (b == null) return null;
                return new DictionarySection<object, object?>("Binding", "binding",
                    new []{ new KeyValuePair<object, object?>("Type", b.GetType().FullName!) }
                    .Concat(
                        b.GetAllComputedProperties()
                        .Select(a => StripBindingProperty(a.GetType().Name, a))
                        .Select(a => new KeyValuePair<object, object?>(a.name, a.value))
                    ).ToArray());
            });
            f.Formatters.Add((e, o) => new CookiesSection(o.Request.Cookies));
            f.Formatters.Add((e, o) => new DictionarySection<string, string>(
                "Assemblies",
                "assemblies",
                AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name != null).OrderBy(a => a.GetName().Name).Select(a => {
                    var info = a.GetName();
                    var versionString = (info.Version != null) ? info.Version.ToString() : "unknown version";
                    var cultureString = (info.CultureInfo != null) ? info.CultureInfo.DisplayName : "unknown culture";
                    var publicKeyString = (info.GetPublicKeyToken() != null) ? info.GetPublicKeyToken()!
                        .Select(b => string.Format("{0:x2}", b)).StringJoin(string.Empty) : "unknown public key";

                    return new KeyValuePair<string, string>(info.Name!,
                        $"Version={versionString}, Culture={cultureString}, PublicKeyToken={publicKeyString}");
                })
            ));
            f.Formatters.Add((e, o) => new DictionarySection<string, string[]>(
                "Request Headers",
                "reqHeaders",
                o.Request.Headers.Select(h =>
                    h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ?
                        new ("Cookie", new [] {"<redacted, see Cookies tab or devtools>"}) :
                        h)
            ));
            f.AddInfoLoader<ReflectionTypeLoadException>(e => new ExceptionAdditionalInfo(
                "Loader Exceptions",
                e.LoaderExceptions.Select(lde => lde!.GetType().Name + ": " + lde.Message).ToArray(),
                ExceptionAdditionalInfo.DisplayMode.ToString
            ));
            f.AddInfoLoader<DotvvmCompilationException>(e => {
                object[]? objects = null;
                if (e.Tokens.Length > 0)
                {
                    objects = new object[]
                    {
                        $"Error in '{string.Concat(e.Tokens.Select(t => t.Text))}' at line {e.LineNumber} in {e.SystemFileName}"
                    };
                }
                return new ExceptionAdditionalInfo(
                    "DotVVM Compiler",
                    objects,
                    ExceptionAdditionalInfo.DisplayMode.ToString
                );
            });
            f.AddInfoLoader<IDotvvmException>(e => {
                var control = e.RelatedControl;
                if (control is null)
                    return null;
                return new ExceptionAdditionalInfo(
                    "Control Hierarchy",
                    control.GetAllAncestors(includingThis: true).Select(c => c.DebugString(config: config, useHtml: true, multiline: false)).ToArray(),
                    ExceptionAdditionalInfo.DisplayMode.ToHtmlListUnencoded
                );
            });

            f.AddInfoCollectionLoader<InvalidCommandInvocationException>(e => {
                if (e.AdditionData == null || !e.AdditionData.Any())
                {
                    return null;
                }
                var infos = new List<ExceptionAdditionalInfo>();
                foreach (var data in e.AdditionData)
                {
                    infos.Add(new ExceptionAdditionalInfo(
                        data.Key,
                        data.Value,
                        ExceptionAdditionalInfo.DisplayMode.ToHtmlList
                    ));
                }
                return infos;
            });

            return f;
        }
    }
}
