using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Hosting.ErrorPages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Testing;
using System.IO;
using DotVVM.Framework.Controls;
using CheckTestOutput;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using System.Reflection;
using System.Linq.Expressions;

namespace DotVVM.Framework.Tests.Runtime
{
    [TestClass]
    public class ErrorPageTests
    {
        static DotvvmConfiguration config = DotvvmTestHelper.DefaultConfig;
        OutputChecker check = new OutputChecker("testoutputs");
        ErrorFormatter formatter = CreateFormatter();
        BindingCompilationService bcs = config.ServiceProvider.GetService<BindingCompilationService>().WithoutInitialization();
        IDotvvmRequestContext context = DotvvmTestHelper.CreateContext(config);

        private static ErrorFormatter CreateFormatter()
        {
            var errorFormatter = ErrorFormatter.CreateDefault(config);
            return errorFormatter;
        }

        private static Exception ThrowAndCatch(Exception exception) =>
            CatchError(() => throw exception);

        private static Exception CatchError(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                return ex;
            }
            throw new Exception("Expected exception");
        }

        [TestMethod]
        public void InvalidBindingException()
        {
            var binding = new ValueBindingExpression(bcs, new object[] { });
            var exception = Assert.ThrowsException<BindingPropertyException>(() => binding.KnockoutExpression);
            var tt = formatter.ErrorHtml(exception, context.HttpContext);

            Assert.IsTrue(tt.Contains(exception.GetType().FullName));
            Assert.IsTrue(tt.Contains(exception.Message));

            // binding tab should contain the binding name
            Assert.IsTrue(tt.Contains(binding.GetType().FullName));

            // the exception contains the property name
            StringAssert.Contains(tt, "DotVVM.Framework.Binding.Properties.KnockoutExpressionBindingProperty");
        }

        [TestMethod]
        public void SerializationDotvvmProperties()
        {
            var obj = new {
                normal = DotvvmControl.IncludeInPageProperty,
                interfaceType = (IControlAttributeDescriptor)DotvvmControl.IncludeInPageProperty,
                alias = DotvvmPropertyTests.TestObject.AliasProperty,
                withFallback = Button.EnabledProperty,
                capability = Button.TextOrContentCapabilityProperty,
                group = HtmlGenericControl.AttributesGroupDescriptor,
                groupMember = HtmlGenericControl.AttributesGroupDescriptor.GetDotvvmProperty("class"),
            };
            check.CheckJsonObject(ErrorPageTemplate.SerializeObjectForBrowser(obj));
        }

        [TestMethod]
        public void SerializationReflectionType()
        {
            var obj = new {
                plainType = typeof(string),
                plainTypeObj = (object)typeof(string),
                plainTypeInterface = (ICustomAttributeProvider)typeof(string),
                nullableType = typeof(int?),
                genericType = typeof(List<string>),

                typeDescriptor = ResolvedTypeDescriptor.Create(typeof(string)),
                typeDescriptorInterface = (ITypeDescriptor)ResolvedTypeDescriptor.Create(typeof(string)),
                typeDescriptorObj = (object)ResolvedTypeDescriptor.Create(typeof(string)),
            };
            check.CheckJsonObject(ErrorPageTemplate.SerializeObjectForBrowser(obj));
        }

#if NETCOREAPP1_0_OR_GREATER
        [TestMethod]
        public void SerializationReflectionAssembly()
        {
            var obj = new {
                assembly = typeof(string).Assembly,
                assemblyObj = (object)typeof(string).Assembly,
                assemblyInterface = (ICustomAttributeProvider)typeof(string).Assembly,
            };
            check.CheckJsonObject(ErrorPageTemplate.SerializeObjectForBrowser(obj));
        }
#endif

        [TestMethod]
        public void SerializationDelegates()
        {
            var obj = new {
                func = new Func<int>(() => 42),
                funcObj = (object)new Func<int>(() => 42),
                dynamicMethod = Expression.Lambda<Func<int>>(Expression.Constant(42)).Compile(),
            };
            check.CheckJsonObject(ErrorPageTemplate.SerializeObjectForBrowser(obj));
        }

        [TestMethod]
        public void SerializationDotvvmControls()
        {
            var dataContext = DataContextStack.Create(typeof(string));
            var bindings = config.ServiceProvider.GetService<BindingCompilationService>();
            var control = new HtmlGenericControl("span")
                .SetAttribute("data-test", "value")
                .AddCssClass("my-class", bindings.Cache.CreateValueBinding<bool>("1 + 1 == 2", dataContext));
            var obj = new {
                control = control,
                controlBase = (DotvvmBindableObject)control,
                controlBase2 = (DotvvmControl)control,
                controlObj = (object)control,
                controlInterface = (IDotvvmObjectLike)control,
                controlInterface2 = (IControlWithHtmlAttributes)control,
                controlInterface3 = (IObjectWithCapability<HtmlCapability>)control,
            };
            check.CheckJsonObject(ErrorPageTemplate.SerializeObjectForBrowser(obj));
        }
    }
}
