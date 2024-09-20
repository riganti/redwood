#nullable enable
#pragma warning disable CS0618 // disable the warning about obsoletes

using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Testing;
using DotVVM.Framework.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DotVVM.Framework.Tests.Runtime
{
    [TestClass]
    public class DotvvmPropertyTests
    {
        public class MockComponent : DotvvmBindableObject
        {
            public object? Property
            {
                get { return (object?)GetValue(PropertyProperty); }
                set { SetValue(PropertyProperty, value); }
            }
            public static DotvvmProperty PropertyProperty
                = DotvvmProperty.Register<object, MockComponent>(t => t.Property);
        }

        DotvvmConfiguration config => DotvvmTestHelper.DefaultConfig;
        BindingCompilationService bindingService => config.ServiceProvider.GetRequiredService<BindingCompilationService>();

        [TestMethod]
        public void DotvvmProperty_PropertyRegisteredTwiceThrowException()
        {
            Assert.ThrowsException<DotvvmProperty.PropertyAlreadyExistsException>(() => {
                _ = MockComponent.PropertyProperty; // calls the static ctor
                DotvvmProperty.Register<bool, MockComponent>(t => t.Property);
            });
        }

        [TestMethod]
        public void HtmlGenericControl_DoesNotContainIdProperty()
        {
            // call the HtmlGenericControl..cctor
            var capability = HtmlGenericControl.HtmlCapabilityProperty;
            var prop = capability.PropertyMapping!.Value.First(x => x.dotvvmProperty.Name == "ID");
            var prop2 = DotvvmProperty.ResolveProperty(typeof(HtmlGenericControl), "ID");

            Assert.IsNotNull(prop2);
            Assert.AreEqual("ID", prop2.Name);
            Assert.AreEqual(typeof(DotvvmControl), prop2.DeclaringType);
            Assert.AreEqual(prop.dotvvmProperty, prop2);
        }

        public class TestObject : DotvvmBindableObject
        {
            [MarkupOptions(Required = true)]
            public string? Aliased
            {
                get => (string?)GetValue(AliasedProperty);
                set => SetValue(AliasedProperty, value);
            }
            public static DotvvmProperty AliasedProperty
                = DotvvmProperty.Register<int, TestObject>(c => c.Aliased);

            // NB: In actual use, IsError should be set to true.
            [Obsolete("Use 'Aliased' instead.", false)]
            [PropertyAlias(nameof(Aliased))]
            public string? Alias
            {
                get => (string?)GetValue(AliasedProperty);
                set => SetValue(AliasedProperty, value);
            }
            public static DotvvmProperty AliasProperty
                = DotvvmProperty.RegisterAlias<TestObject>(c => c.Alias);
        }

        [ContainsDotvvmProperties]
        public class AttachedOne
        {
            [AttachedProperty(typeof(string))]
            public static readonly DotvvmProperty OneProperty =
                DotvvmProperty.Register<string, AttachedOne>(() => OneProperty, string.Empty);
        }

        [ContainsDotvvmProperties]
        public class AttachedTwo
        {
            [AttachedProperty(typeof(string))]
            [PropertyAlias("One", typeof(AttachedOne))]
            [Obsolete("Use 'AttachedOne.One' instead.")]
            public static readonly DotvvmProperty TwoProperty =
                DotvvmProperty.RegisterAlias<AttachedTwo>(() => TwoProperty);
        }

        [TestInitialize]
        public void Init()
        {
            DotvvmPropertyAlias.Resolve((DotvvmPropertyAlias)TestObject.AliasProperty);
            _ = AttachedOne.OneProperty;
            DotvvmPropertyAlias.Resolve((DotvvmPropertyAlias)AttachedTwo.TwoProperty);
        }

        [TestMethod]
        public void DotvvmProperty_AliasRegistered()
        {
            var resolvedAlias = DotvvmProperty.ResolveProperty(typeof(TestObject), nameof(TestObject.Alias));
            Assert.IsTrue(resolvedAlias == TestObject.AliasProperty);
            Assert.IsTrue(((DotvvmPropertyAlias)TestObject.AliasProperty).Aliased == TestObject.AliasedProperty);
        }

        [TestMethod]
        public void DotvvmProperty_AliasUsageThrows()
        {
            var dummy = new HtmlGenericControl("div");
            Assert.ThrowsException<NotSupportedException>(() => TestObject.AliasProperty.GetValue(dummy));
            Assert.ThrowsException<NotSupportedException>(() => TestObject.AliasProperty.IsSet(dummy));
            Assert.ThrowsException<NotSupportedException>(() => TestObject.AliasProperty.SetValue(dummy, ""));
        }

        [TestMethod]
        public void DotvvmProperty_AttachedAlias()
        {
            Assert.IsTrue(((DotvvmPropertyAlias)AttachedTwo.TwoProperty).Aliased == AttachedOne.OneProperty);
        }

        [TestMethod]
        public void DotvvmProperty_ObsoleteAttribute()
        {
            Assert.IsTrue(TestObject.AliasProperty.ObsoleteAttribute is object);
            var obsolete = typeof(TestObject)
                .GetProperty(nameof(TestObject.Alias))
                ?.GetCustomAttribute<ObsoleteAttribute>();
            Assert.IsTrue(TestObject.AliasProperty.ObsoleteAttribute?.Message == obsolete?.Message);
        }

        [TestMethod]
        public void DotvvmProperty_CompileTimeAliasing()
        {
            var tree = DotvvmTestHelper.ParseResolvedTree(
@"@viewModel System.Object
<span AttachedTwo.Two=Test></span>");
            Assert.IsFalse(tree.Content.First().TryGetProperty(AttachedTwo.TwoProperty, out _));
            Assert.IsTrue(tree.Content.First().TryGetProperty(AttachedOne.OneProperty, out _));
        }

        [TestMethod]
        public void DotvvmProperty_ObsoleteWarning()
        {
            var tree = DotvvmTestHelper.ParseResolvedTree(
@"@viewModel System.Object
<span AttachedTwo.Two=Test></span>");

            // the property is aliased so we need to get the alias
            if (tree.Content.First().TryGetProperty(AttachedOne.OneProperty, out var setter))
            {
                Assert.IsTrue(setter.DothtmlNode!.NodeWarnings.Any());
            }
        }

        [TestMethod]
        public void DotvvmProperty_CompileTimeAliasingError()
        {
            var tree = DotvvmTestHelper.ParseResolvedTree(
@"@viewModel System.Object
<span AttachedOne.One=Test1 AttachedTwo.Two=Test2></span>");
            Assert.ThrowsException<DotvvmCompilationException>(() => DotvvmTestHelper.CheckForErrors(tree.DothtmlNode));
        }

        [TestMethod]
        public void DotvvmProperty_GetDataContextType_ItemTemplate_List()
        {
            DotvvmProperty_GetDataContextType_ItemTemplate_Helper<List<string>, string>();
        }
        [TestMethod]
        public void DotvvmProperty_GetDataContextType_ItemTemplate_Enumerable()
        {
            DotvvmProperty_GetDataContextType_ItemTemplate_Helper<System.Collections.ArrayList, object>();
        }
        [TestMethod]
        public void DotvvmProperty_GetDataContextType_ItemTemplate_Grid()
        {
            DotvvmProperty_GetDataContextType_ItemTemplate_Helper<IPageableGridViewDataSet, object>();
        }
        [TestMethod]
        public void DotvvmProperty_GetDataContextType_ItemTemplate_GenericGrid()
        {
            DotvvmProperty_GetDataContextType_ItemTemplate_Helper<IGridViewDataSet<int>, int>();
        }
        [TestMethod]
        public void DotvvmProperty_GetDataContextType_ItemTemplate_Array()
        {
            DotvvmProperty_GetDataContextType_ItemTemplate_Helper<Guid[], Guid>();
        }
        public void DotvvmProperty_GetDataContextType_ItemTemplate_Helper<T, TElement>()
        {
            var repeater = new Repeater();
            var parentContext = DataContextStack.Create(typeof(T));
            repeater.SetDataContextType(parentContext);
            repeater.DataSource = ValueBindingExpression.CreateThisBinding<T>(bindingService, parentContext);

            var dc = Repeater.ItemTemplateProperty.GetDataContextType(repeater)!;
            Assert.AreEqual(typeof(TElement), dc.DataContextType);
            Assert.AreEqual(parentContext, dc.Parent);

            // with data context missing
            repeater.properties.Remove(Internal.DataContextTypeProperty);
            Assert.AreEqual(null, Repeater.ItemTemplateProperty.GetDataContextType(repeater));

            var placeholder = new HtmlGenericControl("div");
            placeholder.SetDataContextType(parentContext);
            placeholder.Children.Add(repeater);

            dc = Repeater.ItemTemplateProperty.GetDataContextType(repeater)!;
            Assert.AreEqual(typeof(TElement), dc.DataContextType);
            Assert.AreEqual(parentContext, dc.Parent);

            // no change here
            dc = Repeater.SeparatorTemplateProperty.GetDataContextType(repeater);
            Assert.AreEqual(parentContext, dc);
        }

        public object?[] GetExampleValues(Type type)
        {
            if (type.IsNullable())
                return GetExampleValues(type.UnwrapNullableType()).Concat(new object? [] { null }).ToArray();
            if (type.IsEnum)
                return Enum.GetValues(type).Cast<object>().ToArray();
            if (type == typeof(bool))
                return new object[] { true, false };
            if (type == typeof(string))
                return new object[] { "a", "b" };
            if (type == typeof(string[]))
                return new object[] { new [] {"a", "b"}, new string[0], new string[] {"XXXXXX"} };
            if (type == typeof(object))
                return new object[] { "a", "b" };
            if (type.IsNumericType())
                return new object[] { 1, 2, 3 }.Select(n => Convert.ChangeType(n, type)).ToArray();
            if (type == typeof(UploadedFilesCollection))
                return new object?[] { new UploadedFilesCollection() };
            if (type == typeof(Command))
                return new object?[] { null, (Command)(() => Task.CompletedTask) };
            if (type == typeof(Action<string>))
                return new object?[] { null, (Action<string>)(_ => {}) };
            if (typeof(DotvvmBindableObject).IsAssignableFrom(type))
                return new object?[] { null, Activator.CreateInstance(type) };
            if (typeof(ITemplate).IsAssignableFrom(type))
                return new object?[] { null, new DelegateTemplate(_ => new HtmlGenericControl()), new CloneTemplate(new HtmlGenericControl()) };
            if (type.IsAssignableFrom(typeof(ValueBindingExpression<bool>)))
                return new object?[] { null, ValueBindingExpression.CreateBinding(bindingService, _ => true, new JsIdentifierExpression("a")) };
            if (type.IsAssignableFrom(typeof(List<int>)))
                return new object?[] { new List<int>(), new List<int> { 100 } };
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return new object?[] { Activator.CreateInstance(type) };

            return new object?[] { null };
            // throw new NotSupportedException(type.ToString());
        }

        IEnumerable<Type> allControls =
            from t in typeof(DotvvmControl).Assembly.GetTypes()
            where !t.IsAbstract
            where typeof(DotvvmBindableObject).IsAssignableFrom(t)
            where t.GetConstructor(Type.EmptyTypes) != null
            select t;

        // test that the getter and setter of the properties are behaving reasonably
        // it's kinda easy to copy paste a different property into get/set
        [TestMethod]
        public void DotvvmProperty_CorrectGetAndSet()
        {
            foreach (var control in allControls)
            {
                var instance = (DotvvmBindableObject)Activator.CreateInstance(control)!;

                var properties = DotvvmProperty.ResolveProperties(control);

                foreach (var p in properties)
                {
                    // there are some exception to the rules...
                    if (p == DotvvmControl.ClientIDProperty)
                        continue;
                    // capability properties behave slightly differently
                    if (p is DotvvmCapabilityProperty)
                        continue;

                    if (p.PropertyInfo == null)
                        continue;

                    Assert.IsNotNull(p.PropertyInfo.GetMethod, $"There is no getter for {p}");

                    if (p.PropertyInfo.PropertyType != p.PropertyType)
                        Console.WriteLine(p);

                    foreach (var example in GetExampleValues(p.PropertyType))
                    {
                        instance.SetValue(p, example);
                        Assert.AreEqual(example, p.PropertyInfo.GetValue(instance), $"Getter is broken in {p}");
                    }

                    if (p.PropertyInfo.SetMethod == null)
                        continue;

                    foreach (var example in GetExampleValues(p.PropertyType))
                    {
                        p.PropertyInfo.SetValue(instance, example);
                        Assert.AreEqual(example, p.PropertyInfo.GetValue(instance), $"Setter is broken in {p}");
                        Assert.AreEqual(example, instance.GetValue(p), $"Setter is broken in {p} (in a weird way)");
                    }
                }
            }
        }

        [TestMethod]
        public void DotvvmProperty_SanityChecks()
        {
            var properties = allControls.SelectMany(DotvvmProperty.ResolveProperties).ToArray();

            foreach (var p in properties)
            {
                if (p is not DotvvmCapabilityProperty)
                {
                    // default value is of the specified type
                    if (p.DefaultValue != null)
                        Assert.IsInstanceOfType(p.DefaultValue, p.PropertyType, $"{p}: {p.DefaultValue} is not instance of {p.PropertyType}");
                    else
                        Assert.IsFalse(p.PropertyType.IsValueType && !p.PropertyType.IsNullable(), $"{p}: default value is null, but property type is not nullable {p.PropertyType}");
                }

                if (p.PropertyInfo != null)
                    Assert.AreEqual(p.PropertyInfo.DeclaringType, p.DeclaringType);
            }
        }

        [TestMethod]
        public void DotvvmProperty_CheckCorrectValueInDataBinding()
        {
            foreach (var control in allControls)
            {
                var properties = DotvvmProperty.ResolveProperties(control);

                foreach (var p in properties)
                {
                    if (p.IsBindingProperty)
                    {
                        Assert.IsFalse(p.MarkupOptions.AllowHardCodedValue);
                    }
                }
            }
        }

        [TestMethod]
        public void DotvvmProperty_ManyItemsSetter()
        {
            var properties = Enumerable.Range(0, 1000).Select(i => HtmlGenericControl.AttributesGroupDescriptor.GetDotvvmProperty("data-" + i.ToString())).ToArray();

            var setter = PropertyImmutableHashtable.CreateBulkSetter(properties, Enumerable.Range(0, 1000).Select(i => (object?)i).ToArray());

            var control1 = new HtmlGenericControl("div");
            setter(control1);
            var control2 = new HtmlGenericControl("div");
            setter(control2);

            Assert.AreEqual(1000, control1.Properties.Count);
        }
    }
}
