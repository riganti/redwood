using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.ResourceManagement.ClientGlobalize;
using System.Globalization;
using DotVVM.Framework.Compilation.Parser;
using System.Reflection;
using DotVVM.Framework.Binding;
using Microsoft.Extensions.DependencyInjection;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.ControlTree;
using CheckTestOutput;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Testing;
using System.Threading.Tasks;
using DotVVM.Framework.Compilation.ControlTree.Resolved;

namespace DotVVM.Framework.Tests.Runtime
{
    static class CheckExtensions
    {
        static string FormatException(Exception exception)
        {
            var topFrame = new EnhancedStackTrace(exception).FirstOrDefault(m => !m.MethodInfo.DeclaringType.Namespace.StartsWith("DotVVM.Framework.Binding"))?.MethodInfo.ToString();
            var msg = $"{exception.GetType().Name} occurred: {exception.Message}";
            if (topFrame != null) msg += $"\n    at {topFrame}";
            if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 1)
            {
                var inner = aggregateException.InnerExceptions.Select(FormatException).StringJoin("\n\n");
                return $"{msg}\n\n{inner}";
            }
            else
            {
                var inner = exception.InnerException?.Apply(FormatException);

                if (inner == null)
                    return msg;
                else
                    return $"{msg}\n\n{inner}";
            }
        }

        public static async Task CheckExceptionAsync(this OutputChecker check, Func<Task> action, string checkName = null, string fileExtension = "txt", [CallerMemberName] string memberName = null, [CallerFilePath] string sourceFilePath = null)
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                var error = FormatException(exception);
                check.CheckString(error, checkName, fileExtension, memberName, sourceFilePath);
                return;
            }
            throw new Exception("Expected test to fail.");
        }

        public static void CheckException(this OutputChecker check, Action action, string checkName = null, string fileExtension = "txt", [CallerMemberName] string memberName = null, [CallerFilePath] string sourceFilePath = null)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                var error = FormatException(exception);
                check.CheckString(error, checkName, fileExtension, memberName, sourceFilePath);
                return;
            }
            throw new Exception("Expected test to fail.");
        }
    }

    [TestClass]
    public class RuntimeErrorTests
    {
        OutputChecker check = new OutputChecker("testoutputs");
        DotvvmConfiguration config = DotvvmTestHelper.DefaultConfig;
        BindingCompilationService bindingService => config.ServiceProvider.GetService<BindingCompilationService>();

        [TestMethod]
        public void NonExistentBindingProperty()
        {
            var binding = ValueBindingExpression.CreateBinding(bindingService, a => 12, DataContextStack.Create(typeof(string)));

            check.CheckException(() => binding.GetProperty<OriginalStringBindingProperty>());
        }

        [TestMethod]
        public void PropertyResolverFailure()
        {
            var binding = ValueBindingExpression.CreateBinding(bindingService.WithoutInitialization(), a => config.ToString(), DataContextStack.Create(typeof(string)));
            //                                                                ^ disables initialization check

            check.CheckException(() => binding.GetProperty<KnockoutExpressionBindingProperty>());
        }

        [TestMethod]
        public void InitResolverFailure()
        {

            check.CheckException(() => ValueBindingExpression.CreateBinding(bindingService, a => config.ToString(), DataContextStack.Create(typeof(string))));
        }

        [TestMethod]
        public void BindingErrorsShouldBeReasonable()
        {

            check.CheckException(() => new ValueBindingExpression(bindingService, new object[] {
                DataContextStack.Create(typeof(string)),
                new OriginalStringBindingProperty("_parent.LOL"),
                BindingParserOptions.Value
            }));
        }

        [TestMethod]
        public void DataContextStack_ToString()
        {
            var parent = DataContextStack.Create(typeof(string), DataContextStack.Create(typeof(RuntimeErrorTests)));
            var d = DataContextStack.Create(
                typeof(int),
                parent,
                imports: new [] { new NamespaceImport("System"), new NamespaceImport("System.Text", "Text") },
                extensionParameters: new [] { new CurrentCollectionIndexExtensionParameter() });

            check.CheckString(d.ToString());
        }

        private void TriggerDataContextMismatchError(DataContextStack type1, DataContextStack type2)
        {
            _ = HtmlGenericControl.VisibleProperty; // runtime hack for static construction
            var binding = ValueBindingExpression.CreateBinding(bindingService, a => false, type1);
            var control = new HtmlGenericControl("div");
            control.SetValue(Internal.DataContextTypeProperty, type2);
            control.SetProperty(c => c.Visible, binding);
            control.Visible.ToString();
        }

        [TestMethod]
        public void CantFindDataContextSpace()
        {
            check.CheckException(() => TriggerDataContextMismatchError(
                DataContextStack.Create(typeof(string)),
                DataContextStack.Create(typeof(string), DataContextStack.Create(typeof(int))))
            );
        }

        [TestMethod]
        public void CantFindDataContextSpace_NSDifference()
        {
            var root = DataContextStack.Create(typeof(Binding.TestViewModel3), extensionParameters: [ new CurrentMarkupControlExtensionParameter(new ResolvedTypeDescriptor(typeof(DotvvmMarkupControl))) ]);
            var type1 = DataContextStack.Create(typeof(Binding.TestViewModel), root);
            var type2 = DataContextStack.Create(typeof(Runtime.ControlTree.TestViewModel), DataContextStack.Create(typeof(string), root));
            check.CheckException(() => TriggerDataContextMismatchError(type1, type2));
        }
        [TestMethod]
        public void CantFindDataContextSpace_ParentChild()
        {
            var type1 = DataContextStack.Create(typeof(Binding.TestViewModel));
            var type2 = DataContextStack.CreateCollectionElement(typeof(int), type1);
            check.CheckException(() => TriggerDataContextMismatchError(type1, type2));
        }
        [TestMethod]
        public void CantFindDataContextSpace_MissingEP()
        {
            var root = DataContextStack.Create(typeof(Binding.TestViewModel));
            var type1 = DataContextStack.CreateCollectionElement(typeof(int), root, extensionParameters: [ new InjectedServiceExtensionParameter("services", new ResolvedTypeDescriptor(typeof(IServiceProvider))) ]);
            var type2 = DataContextStack.CreateCollectionElement(typeof(int), root);
            check.CheckException(() => TriggerDataContextMismatchError(type1, type2));
        }
    }
}
