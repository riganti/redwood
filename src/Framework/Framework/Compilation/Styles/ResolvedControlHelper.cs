using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Utils;
using System.Reflection;
using DotVVM.Framework.Compilation.ControlTree;
using System.Linq;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using System.Net;
using System.Diagnostics.CodeAnalysis;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Compilation.ViewCompiler;
using DotVVM.Framework.Runtime;
using FastExpressionCompiler;
using System.Threading;

namespace DotVVM.Framework.Compilation.Styles
{
    public static class ResolvedControlHelper
    {
        /// <summary> Creates a ResolvedControl from a DotvvmBindableObject. Used in control precompilation and server-side styles. If <paramref name="config" /> isn't specified, it won't be able to translate <see cref="MarkupControlContainer" />.  </summary>
        public static ResolvedControl FromRuntimeControl(
            DotvvmBindableObject obj,
            DataContextStack dataContext,
            DotvvmConfiguration? config)
        {
            var type = obj.GetType();

            dataContext = obj.GetDataContextType() ?? dataContext;

            if (obj is DotvvmMarkupControl)
            {
                throw new NotSupportedException($"Markup controls are not supported, you can use MarkupControlContainer instead.");
            }

            if (obj is MarkupControlContainer markupControl)
            {
                if (config is null)
                    throw new NotSupportedException("Can't translate MarkupControlContainer without access to DotvvmConfiguration.");
                var path = markupControl.GetMarkupPath(config);
                var controlBuilderFactory = config.ServiceProvider.GetRequiredService<IControlBuilderFactory>();
                var (descriptor, controlBuilder) = controlBuilderFactory.GetControlBuilder(path);
                var control = new ResolvedControl(new ControlResolverMetadata(new ControlType(descriptor.ControlType, path, descriptor.DataContextType)), null, new(), dataContext);
                if (markupControl.SetProperties is object)
                {
                    var templateControl = (DotvvmMarkupControl)Activator.CreateInstance(descriptor.ControlType)!;
                    markupControl.SetProperties(templateControl);
                    foreach (var p in templateControl.Properties)
                    {
                        var propertyDC = GetPropertyDataContext(obj, p.Key, dataContext);
                        control.SetProperty(
                            TranslateProperty(p.Key, p.Value, propertyDC, config),
                            replace: true
                        );
                    }
                }
                return control;
            }

            if (obj is LazyRuntimeControl wrapper)
            {
                return wrapper.ResolvedControl;
            }

            var content = (obj as DotvvmControl)?.Children.Select(c => FromRuntimeControl(c, dataContext, config)).ToList();
            var rc = new ResolvedControl(new ControlResolverMetadata(type), null, content, dataContext);

            if (obj is RawLiteral literal)
            {
                rc.ConstructorParameters = new object[] { literal.EncodedText, literal.UnencodedText, BoxingUtils.Box(literal.IsWhitespace) };
            }
            else if (type == typeof(HtmlGenericControl) && obj is HtmlGenericControl htmlControl)
            {
                rc.ConstructorParameters = new object[] { htmlControl.TagName! };
            }

            foreach (var p in obj.Properties)
            {
                var propertyDC = GetPropertyDataContext(obj, p.Key, dataContext);
                rc.SetProperty(
                    TranslateProperty(p.Key, p.Value, propertyDC, config),
                    replace: true
                );
            }

            DotvvmProperty.CheckAllPropertiesAreRegistered(type);
            DotvvmPropertyGroup.CheckAllPropertiesAreRegistered(type);

            return rc;
        }

        private static DataContextStack GetPropertyDataContext(DotvvmBindableObject obj, DotvvmProperty property, DataContextStack objDataContext)
        {
            var propDataContext = property.GetDataContextType(obj, objDataContext);
            if (propDataContext is {})
                return propDataContext;

            var propertyValue = obj.GetValue(property);
            if (propertyValue is not (ITemplate or IDotvvmObjectLike or IBinding))
            {
                // primitive value, the data context does not matter anyway
                return objDataContext;
            }

            throw new PropertyTranslationException(property) { RelatedControl = obj };
        }

        private static DataContextStack GetPropertyDataContext(ResolvedControl obj, DotvvmProperty property, DataContextStack objDataContext)
        {
            var propDataContext = property.GetDataContextType(obj, objDataContext);
            if (propDataContext is {})
                return propDataContext;

            var propertyValue = obj.GetValue(property);
            if (propertyValue is not (ITemplate or IDotvvmObjectLike or IBinding))
            {
                // primitive value, the data context does not matter anyway
                return objDataContext;
            }

            throw new PropertyTranslationException(property) { RelatedResolvedControl = obj };
        }

        public record PropertyTranslationException(DotvvmProperty property):
            DotvvmExceptionBase(RelatedProperty: property)
        {
            public override string Message => $"Unable to translate property {property.Name}, because the DataContext change attributes could not be evaluated.";
        }

        private static Type[] ImmutableContainers = new[] {
            typeof(ImmutableArray<>), typeof(ImmutableList<>), typeof(ImmutableHashSet<>), typeof(ImmutableQueue<>), typeof(ImmutableSortedSet<>), typeof(ImmutableStack<>)
        };
        
        public static bool IsAllowedPropertyValue([NotNullWhen(false)] object? value)
        {
            if (value is ValueOrBinding vob && IsAllowedPropertyValue(vob.UnwrapToObject()) ||
                value is HtmlGenericControl.AttributeList ||
                value is IBinding ||
                value is null)
            {
                return true;
            }

            var type = value.GetType();
            if (ReflectionUtils.IsPrimitiveType(type) ||
                type.GetCustomAttribute<HandleAsImmutableObjectInDotvvmPropertyAttribute>() is not null)
            {
                return true;
            }

            // technically we should not allow Array as it can be modified, but we use it already on many places
            if (value is Array ||
                (type.IsGenericType && ImmutableContainers.Contains(type.GetGenericTypeDefinition())))
            {
                return ((IEnumerable)value).OfType<object>().All(IsAllowedPropertyValue);
            }

            return false;
        }

        public static ResolvedPropertySetter TranslateProperty(DotvvmProperty property, object? value, DataContextStack dataContext, DotvvmConfiguration? config)
        {
            if (value is ResolvedPropertyCapability resolvedCapability)
            {
                if (resolvedCapability.Property == property && resolvedCapability.GetDataContextStack(null) == dataContext)
                    return resolvedCapability;
                if (property is not DotvvmCapabilityProperty capabilityProperty)
                    throw new NotSupportedException($"Property {property.Name} must capability property.");
                if (resolvedCapability.Property.PropertyType != capabilityProperty.PropertyType)
                    throw new NotSupportedException($"Property {property.Name} have type {resolvedCapability.Property.PropertyType.ToCode()}.");

                value = resolvedCapability.ToCapabilityObject(config?.ServiceProvider);
            }
            else if (value is ResolvedPropertySetter resolvedSetter)
            {
                value = resolvedSetter.GetValue();
            }

            value = ValueOrBindingExtensions.UnwrapToObject(value);

            if (value is DotvvmBindableObject valueControl)
            {
                value = FromRuntimeControl(valueControl, dataContext, config);
            }
            else if (value is IEnumerable<DotvvmBindableObject> valueControls)
            {
                value = valueControls.Select(c => FromRuntimeControl(c, dataContext, config)).ToList();
            }

            if (value is IEnumerable<ResolvedControl> controlCollection && controlCollection.Count() == 1)
                value = controlCollection.First();

            if (value is ResolvedControl c)
            {
                var propType = property.PropertyType;
                var controlType = c.Metadata.Type;
                if (typeof(ITemplate).IsAssignableFrom(propType))
                    return new ResolvedPropertyTemplate(property, new List<ResolvedControl> { c });
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propType) &&
                            ReflectionUtils.GetEnumerableType(propType)!.IsAssignableFrom(controlType))
                    return new ResolvedPropertyControlCollection(property, new List<ResolvedControl> { c });
                else if (typeof(DotvvmBindableObject).IsAssignableFrom(propType) &&
                            propType.IsAssignableFrom(controlType))
                    return new ResolvedPropertyControl(property, c);
                else
                    throw new Exception($"Cannot set a control of type {controlType} to a property {property} of type {propType}.");
            }
            else if (value is IEnumerable<ResolvedControl> cs)
            {
                if (typeof(ITemplate).IsAssignableFrom(property.PropertyType))
                    return new ResolvedPropertyTemplate(property, cs.ToList());
                else
                    return new ResolvedPropertyControlCollection(property, cs.ToList());
            }
            else if (value is ITemplate template)
            {
                if (template is ResolvedControlTemplate resolvedTemplate)
                    return new ResolvedPropertyTemplate(property, resolvedTemplate.Controls.ToList());

                if (template is not CloneTemplate cloneTemplate)
                    throw new Exception($"Template of type {template.GetType().Name} are not supported in server side styles, use CloneTemplate instead.");
                return new ResolvedPropertyTemplate(property, cloneTemplate.Controls.Select(c => FromRuntimeControl(c, dataContext, config)).ToList());
            }
            else if (value is IBinding binding)
            {
                var resolvedBinding = binding.GetProperty<ResolvedBinding>(ErrorHandlingMode.ReturnNull);
                return new ResolvedPropertyBinding(property, resolvedBinding ?? new ResolvedBinding(binding));
            }
            else if (value is ResolvedBinding resolvedBinding)
            {
                return new ResolvedPropertyBinding(property, resolvedBinding);
            }
            else if (IsAllowedPropertyValue(value))
            {
                var convertedValue = ReflectionUtils.ConvertValue(value, property.PropertyType);
                return new ResolvedPropertyValue(property, convertedValue);
            }
            else
            {
                throw new NotSupportedException($"Value '{value}' of type {value.GetType()} in {property} cannot be compiled into a property.");
            }
        }

        public static void SetContent(ResolvedControl control, ResolvedControl[] innerControls, StyleOverrideOptions options)
        {
            if (innerControls.Length == 0)
            {
                if (options == StyleOverrideOptions.Overwrite)
                {
                    // remove the existing value
                    control.Content.Clear();
                    if (control.Metadata.DefaultContentProperty is {} dp)
                    {
                        control.RemoveProperty(dp);
                    }
                }
            }
            else if (control.Metadata.DefaultContentProperty is {} defaultProp)
            {
                var propertyDataContext = GetPropertyDataContext(control, defaultProp, control.DataContextTypeStack);
                var setter = ResolvedControlHelper.TranslateProperty(defaultProp, innerControls, propertyDataContext, null);

                if (!control.SetProperty(setter, options, out var err))
                    throw new DotvvmCompilationException(err);
            }
            else if (control.Metadata.IsContentAllowed)
            {
                foreach (var c in innerControls)
                    if (!typeof(DotvvmControl).IsAssignableFrom(c.Metadata.Type))
                        throw new DotvvmCompilationException($"Control {c.Metadata.Name} cannot be inserted into {control.Metadata.Name} since it does not inherit from DotvvmControl.");

                foreach (var ic in innerControls)
                    ic.Parent = control;

                switch (options)
                {
                    case StyleOverrideOptions.Append:
                        control.Content.AddRange(innerControls);
                        break;
                    case StyleOverrideOptions.Prepend:
                        control.Content.InsertRange(0, innerControls);
                        break;
                    case StyleOverrideOptions.Overwrite:
                        control.Content.Clear();
                        control.Content.AddRange(innerControls);
                        break;
                    case StyleOverrideOptions.Ignore:
                        if (control.HasOnlyWhiteSpaceContent())
                        {
                            control.Content.Clear();
                            control.Content.AddRange(innerControls);
                        }
                        break;
                }
            }
            else
            {
                throw new DotvvmCompilationException($"Could not set content on {control.Metadata.Type} as it does not allow children and does not have a DefaultContentProperty.");
            }
        }

        static DotvvmBindableObject ToLazyRuntimeControl(this ResolvedControl c, Type expectedType, IServiceProvider services)
        {
            if (expectedType == typeof(DotvvmControl))
                return new LazyRuntimeControl(c, services);
            else
                return ToRuntimeControl(c, services);
        }

        static IEnumerable<T> CreateEnumerable<T>(Type type, IEnumerable<T> items)
        {
            var elementType = ReflectionUtils.GetEnumerableType(type)!;
            if (type.IsArray)
            {
                var array = Array.CreateInstance(elementType, items.Count());
                foreach (var (item, i) in items.Select((item, i) => (item, i)))
                    array.SetValue(item, i);
                return (IEnumerable<T>)array;
            }
            else
            {
                if (type.IsInterface)
                {
                    // hope that List implements the interface
                    type = typeof(List<>).MakeGenericType(elementType);
                }
                var list = (System.Collections.IList)Activator.CreateInstance(type)!;
                foreach (var i in items)
                    list.Add(i);
                return (IEnumerable<T>)list;
            }
        }

        public static object? ToRuntimeValue(this ResolvedPropertySetter setter, IServiceProvider? services)
        {
            if (setter is ResolvedPropertyValue valueSetter)
                return valueSetter.Value;
            if (setter is ResolvedPropertyBinding bindingSetter)
                return bindingSetter.Binding.Binding;

            var expectedType = setter.Property.PropertyType;

            if (setter is ResolvedPropertyControl controlSetter)
            {
                if (services is null)
                    throw new ArgumentNullException(nameof(services), "Cannot convert a control to a runtime value without a service provider.");

                return controlSetter.Control?.ToLazyRuntimeControl(expectedType, services);
            }
            else if (setter is ResolvedPropertyControlCollection controlCollectionSetter)
            {
                if (services is null)
                    throw new ArgumentNullException(nameof(services), "Cannot convert a control to a runtime value without a service provider.");

                var expectedControlType = ReflectionUtils.GetEnumerableType(expectedType)!;
                return CreateEnumerable(
                    expectedType,
                    controlCollectionSetter.Controls.Select(c => c.ToLazyRuntimeControl(expectedControlType, services))
                );
            }
            else if (setter is ResolvedPropertyTemplate templateSetter)
            {
                return new ResolvedControlTemplate(templateSetter.Content.ToArray());
            }
            else if (setter is ResolvedPropertyCapability capability)
            {
                return capability.ToCapabilityObject(services);
            }
            else
                throw new NotSupportedException($"Property setter {setter.GetType().Name} is not supported.");
        }

        /// <summary> Creates a new instance of the dotvvm object represented by this ResolvedControl. This is a lightweight alternative to compiling the control using the <see cref="ViewCompilingVisitor" />. Children are not initialized immediately, all child components are replaced by <see cref="LazyRuntimeControl" /> </summary>
        public static DotvvmBindableObject ToRuntimeControl(this ResolvedControl c, IServiceProvider services)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            var control = (DotvvmBindableObject)ActivatorUtilities.CreateInstance(services, c.Metadata.Type, c.ConstructorParameters ?? Array.Empty<object>());

            foreach (var p in c.Properties.Values)
            {
                if (p.Property is CompileTimeOnlyDotvvmProperty)
                {
                    // preserve this property too, but we have to set it directly to the dictionary. Also, don't worry about type conversions too much.
                    control.properties.Set(p.Property, p.GetValue());
                    continue;
                }

                control.SetValueRaw(p.Property, p.ToRuntimeValue(services));
            }
            if (c.DataContextTypeStack is {} dataContext)
                control.SetValueRaw(Internal.DataContextTypeProperty, dataContext);

            foreach (var child in c.Content)
            {
                ((DotvvmControl)control).Children.Add((DotvvmControl)child.ToLazyRuntimeControl(typeof(DotvvmControl), services));
            }

            return control;
        }

        /// <summary> Lazily creates children from the ResolvedControl when OnInit or GetLogicalChildren is invoked. </summary>
        public sealed class LazyRuntimeControl: DotvvmControl
        {
            public ResolvedControl ResolvedControl { get; set; }
            readonly IServiceProvider services;

            private bool initialized = false;

            public LazyRuntimeControl(ResolvedControl resolvedControl, IServiceProvider services)
            {
                ResolvedControl = resolvedControl;
                LifecycleRequirements = ControlLifecycleRequirements.Init;
                this.services = services;
            }

            void InitializeChildren(IDotvvmRequestContext? context)
            {
                if (initialized) return;
                // lock just to be safe. Someone could quite reasonably expect that reading control contents in parallel is safe
                lock (this)
                {
                    if (initialized) return;

                    var services = context?.Services ?? this.services;

                    Children.Add((DotvvmControl)ResolvedControl.ToRuntimeControl(services));
                    Thread.MemoryBarrier(); // make sure write to Children is done before we let other threads read without lock
                    initialized = true;
                }
            }

            public override IEnumerable<DotvvmBindableObject> GetLogicalChildren()
            {
                InitializeChildren((IDotvvmRequestContext?)this.GetValue(Internal.RequestContextProperty));
                return base.GetLogicalChildren();
            }

            protected internal override void OnInit(IDotvvmRequestContext context)
            {
                InitializeChildren(context);
                base.OnInit(context);
            }
        }

        public sealed class ResolvedControlTemplate: ITemplate
        {
            public ResolvedControl[] Controls { get; set; }

            public ResolvedControlTemplate(ResolvedControl[] controls)
            {
                Controls = controls;
            }

            public void BuildContent(IDotvvmRequestContext context, DotvvmControl container)
            {
                foreach (var c in Controls)
                {
                    container.Children.Add((DotvvmControl)c.ToRuntimeControl(context.Services));
                }
            }
        }
    }
}
