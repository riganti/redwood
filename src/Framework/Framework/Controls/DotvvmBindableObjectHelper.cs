using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Utils;
using FastExpressionCompiler;

namespace DotVVM.Framework.Controls
{
    public static partial class DotvvmBindableObjectHelper
    {
        /// <summary> Gets the DotvvmProperty referenced by the lambda expression. </summary>
        public static DotvvmProperty GetDotvvmProperty<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop)
            where TControl : DotvvmBindableObject =>
            DotvvmPropertyUtils.GetDotvvmPropertyFromExpression(prop);
        /// <summary> Gets the DotvvmProperty with the specified name.  </summary>
        public static DotvvmProperty GetDotvvmProperty<TControl>(this TControl control, string propName)
            where TControl : DotvvmBindableObject
        {
            var type = control.GetType();
            return DotvvmProperty.ResolveProperty(type, propName) ?? throw new Exception($"Property '{type}.{propName}' is not a registered DotvvmProperty.");
        }

        /// <summary> Sets binding into the DotvvmProperty referenced in the lambda expression. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop, IBinding? binding)
            where TControl : DotvvmBindableObject
        {
            control.SetBinding(control.GetDotvvmProperty(prop), binding);
            return control;
        }

        /// <summary> Sets binding into the DotvvmProperty. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl>(this TControl control, DotvvmProperty prop, IBinding? binding)
            where TControl : DotvvmBindableObject
        {
            control.SetBinding(prop, binding);
            return control;
        }
        /// <summary> Sets a value or a binding into the DotvvmProperty with specified name. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl>(this TControl control, string propName, object? value)
            where TControl : DotvvmBindableObject
        {
            control.SetValue(control.GetDotvvmProperty(propName), value);
            return control;
        }
        /// <summary> Sets value or binding into the DotvvmProperty referenced in the lambda expression. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop, ValueOrBinding<TProperty> value)
            where TControl : DotvvmBindableObject
        {
            control.SetValue(control.GetDotvvmProperty(prop), value.UnwrapToObject());
            return control;
        }
        /// <summary> Sets value or binding into the DotvvmProperty referenced in the lambda expression. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop, ValueOrBinding<TProperty>? value)
            where TControl : DotvvmBindableObject
        {
            if (value.HasValue)
            {
                control.SetProperty(prop, value.Value);
            }
            return control;
        }
        /// <summary> Sets value or binding into the DotvvmProperty referenced in the lambda expression. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop, TProperty value)
            where TControl : DotvvmBindableObject
        {
            control.SetValue(control.GetDotvvmProperty(prop), value);
            return control;
        }
        /// <summary> Sets value or binding into the DotvvmProperty with specified name. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, string propName, ValueOrBinding<TProperty> valueOrBinding)
            where TControl : DotvvmBindableObject
        {
            control.SetProperty(control.GetDotvvmProperty(propName), valueOrBinding);
            return control;
        }

        /// <summary> Sets value or binding into the DotvvmProperty. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, DotvvmProperty property, ValueOrBinding<TProperty> valueOrBinding)
            where TControl: DotvvmBindableObject
        {
            if (valueOrBinding.BindingOrDefault == null)
                control.SetValue(property, valueOrBinding.BoxedValue);
            else
                control.SetBinding(property, valueOrBinding.BindingOrDefault);
            return control;
        }

        /// <summary> Sets value or binding into the DotvvmProperty. If the <paramref name="valueOrBinding"/> is null, the property is removed. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, DotvvmProperty property, ValueOrBinding<TProperty>? valueOrBinding)
            where TControl: DotvvmBindableObject
        {
            if (valueOrBinding.HasValue)
                control.SetProperty(property, valueOrBinding.GetValueOrDefault());
            else
                control.Properties.Remove(property);
            return control;
        }

        /// <summary> Sets value or binding into the DotvvmProperty. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl>(this TControl control, DotvvmProperty property, object? value)
            where TControl: DotvvmBindableObject
        {
            control.SetValue(property, value);
            return control;
        }

        [Obsolete("Please prefer the SetProperty method with the same signature")]
        public static TControl SetBinding<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop, IBinding? binding)
            where TControl : DotvvmBindableObject =>
            control.SetProperty(prop, binding);
        [Obsolete("Please prefer the SetProperty method with the same signature")]
        public static TControl SetValue<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop, TProperty value)
            where TControl : DotvvmBindableObject =>
            control.SetProperty(prop, value);

        /// <summary> Sets a binding into member of the specified property group. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, Func<TControl, VirtualPropertyGroupDictionary<TProperty>> prop, string key, IBinding? binding)
            where TControl : DotvvmBindableObject
        {
            var d = prop(control);
            d.AddBinding(key, binding);
            return control;
        }

        /// <summary> Sets a value into member of the specified property group. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, Func<TControl, VirtualPropertyGroupDictionary<TProperty>> prop, string key, TProperty value)
            where TControl : DotvvmBindableObject
        {
            var d = prop(control);
            d.Add(key, value);
            return control;
        }

        /// <summary> Sets a value or a binding into member of the specified property group. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetProperty<TControl, TProperty>(this TControl control, Func<TControl, VirtualPropertyGroupDictionary<TProperty>> prop, string key, ValueOrBinding<TProperty> value)
            where TControl : DotvvmBindableObject
        {
            var d = prop(control);
            d.Add(key, value);
            return control;
        }

        /// <summary> Sets a value (or a binding) into the specified html attribute. If the value is null, the attribute is removed. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetAttribute<TControl>(this TControl control, string attribute, object? value)
            where TControl : IControlWithHtmlAttributes
        {
            value = ValueOrBindingExtensions.UnwrapToObject(value);

            if (value is not null)
            {
                control.Attributes.Set(attribute, value);
            }
            else
            {
                control.Attributes.Remove(attribute);
            }
            return control;
        }

        /// <summary> Sets a value or a binding into the specified html attribute. If the value is null, the attribute is removed. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetAttribute<TControl, TValue>(this TControl control, string attribute, ValueOrBinding<TValue> value)
            where TControl : IControlWithHtmlAttributes
        {
            return SetAttribute(control, attribute, value.UnwrapToObject());
        }

        /// <summary> Sets a value or a binding into the specified html attribute. If the value is null, the attribute is removed. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetAttribute<TControl, TValue>(this TControl control, string attribute, ValueOrBinding<TValue>? value)
            where TControl : IControlWithHtmlAttributes
        {
            return SetAttribute(control, attribute, value?.UnwrapToObject());
        }

        /// <summary> Appends a value into the specified html attribute. If the attribute already exists, the old and new values are merged. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddAttribute<TControl>(this TControl control, string attribute, object? value)
            where TControl : IControlWithHtmlAttributes
        {
            if (value is not null)
                control.Attributes.Add(attribute, ValueOrBindingExtensions.UnwrapToObject(value));
            return control;
        }
        /// <summary> Appends a value into the specified html attribute. If the attribute already exists, the old and new values are merged. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddAttribute<TControl, TValue>(this TControl control, string attribute, ValueOrBinding<TValue>? value)
            where TControl : IControlWithHtmlAttributes
        {
            return AddAttribute(control, attribute, value?.UnwrapToObject());
        }
        /// <summary> Appends a value into the specified html attribute. If the attribute already exists, the old and new values are merged. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddAttribute<TControl, TValue>(this TControl control, string attribute, ValueOrBinding<TValue> value)
            where TControl : IControlWithHtmlAttributes
        {
            return AddAttribute(control, attribute, value.UnwrapToObject());
        }

        /// <summary> Appends a list of css attributes to the control. If the attributes already exist, the old and new values are merged. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddAttributes<TControl, TValue>(this TControl control, IEnumerable<KeyValuePair<string, TValue>> attributes)
            where TControl : IControlWithHtmlAttributes
        {
            foreach (var a in attributes)
                AddAttribute(control, a.Key, a.Value);
            return control;
        }
        
        /// <summary> Appends a list of css attributes to the control. If the attributes already exist, the old and new values are merged. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddAttributes<TControl, TValue>(this TControl control, VirtualPropertyGroupDictionary<TValue> attributes)
            where TControl : IControlWithHtmlAttributes
        {
            foreach (var a in attributes.RawValues)
                AddAttribute(control, a.Key, a.Value);
            return control;
        }

        /// <summary> Appends a css class to this control. Note that it is currently not supported if multiple bindings would have to be joined together. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddCssClass<TControl>(this TControl control, ValueOrBinding<string?> className)
            where TControl : IControlWithHtmlAttributes
        {
            var classNameObj = className.UnwrapToObject();
            if (classNameObj is null or "") return control;
            return AddAttribute(control, "class", classNameObj);
        }

        /// <summary> Appends a css class to this control. Note that it is currently not supported if multiple bindings would have to be joined together. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddCssClass<TControl>(this TControl control, string? className)
            where TControl : IControlWithHtmlAttributes
        {
            if (className is null or "") return control;

            return AddAttribute(control, "class", className);
        }

        /// <summary> Appends a list of css classes to this control. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddCssClasses<TControl>(this TControl control, params string?[]? classes)
            where TControl : IControlWithHtmlAttributes
        {
            if (classes is null || classes.Length == 0)
                return control;
            return AddCssClass(control, string.Join(" ", classes.Where(c => !String.IsNullOrWhiteSpace(c))));
        }

        /// <summary> Appends a css class to this control if the <paramref name="condition"/> is true. </summary>
        public static TControl AddCssClass<TControl>(this TControl control, string className, bool condition)
            where TControl : IControlWithHtmlAttributes
        {
            if (condition)
                return AddCssClass(control, className);
            return control;
        }

        /// <summary> Appends a css class to this control if the <paramref name="condition"/> is true. </summary>
        public static TControl AddCssClass<TControl>(this TControl control, string className, ValueOrBinding<bool> condition)
            where TControl : IObjectWithCapability<HtmlCapability>
        {
            if (condition.HasValue)
            {
                AddCssClass(control.AsObjectWithHtmlAttributes(), className, condition.ValueOrDefault);
            }
            else
            {
                var p = control.GetCssClassesDictionary();
                p.Set(className, condition);
            }
            return control;
        }
        /// <summary> Appends a css class to this control if the <paramref name="condition"/> is true. </summary>
        public static TControl AddCssClass<TControl>(this TControl control, string className, ValueOrBinding<bool>? condition)
            where TControl : IObjectWithCapability<HtmlCapability> =>
            condition is null ? control : AddCssClass(control, className, condition.Value);

        /// <summary> Appends a css class to this control if the <paramref name="condition"/> is true. </summary>
        public static TControl AddCssClass<TControl>(this TControl control, string className, IStaticValueBinding<bool>? condition)
            where TControl : IObjectWithCapability<HtmlCapability>
        {
            if (condition is {})
            {
                var p = control.GetCssClassesDictionary();
                p.SetBinding(className, condition);
            }
            return control;
        }

        /// <summary> Adds a css inline style - the `style` attribute. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddCssStyle<TControl>(this TControl control, string name, string? styleValue)
            where TControl : IControlWithHtmlAttributes
        {
            if (styleValue is null)
                return control;
            return AddAttribute(control, "style", name + ":" + styleValue);
        }

        /// <summary> Adds a css inline style - the `style` attribute. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddCssStyle<TControl, T>(this TControl control, string name, ValueOrBinding<T>? styleValue)
            where TControl : IObjectWithCapability<HtmlCapability>
        {
            if (styleValue is null)
                return control;

            if (styleValue.Value.HasValue)
            {
                var value = styleValue.Value.ValueOrDefault;
                // this may happen due to implicit conversions to object
                if (value is ValueOrBinding nestedVOB)
                    return AddCssStyle<TControl, object?>(control, name, ValueOrBinding<object?>.UpCast(nestedVOB));

                AddCssStyle(control.AsObjectWithHtmlAttributes(), name, value?.ToString());
            }
            else
            {
                var p = control.GetCssStylesDictionary();
                p.Set(name, styleValue.Value.UnwrapToObject());
            }
            return control;
        }

        /// <summary> Adds a css inline style - the `style` attribute. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl AddCssStyle<TControl, T>(this TControl control, string name, IStaticValueBinding<T>? styleValue)
            where TControl : IObjectWithCapability<HtmlCapability>
        {
            if (styleValue is {})
            {
                var p = control.GetCssStylesDictionary();
                p.SetBinding(name, styleValue);
            }
            return control;
        }


        /// <summary> Sets all properties from the capability into this control. If the control does not support the capability, exception is thrown. Returns <paramref name="control"/> for fluent API usage. </summary>
        public static TControl SetCapability<TControl, TCapability>(this TControl control, [AllowNull] TCapability capability, string prefix = "")
            where TControl: DotvvmBindableObject
        {
            if (capability != null)
            {
                var c = DotvvmCapabilityProperty.Find(typeof(TControl), typeof(TCapability), prefix);
                if (c is null)
                    throw new Exception($"Capability {prefix}{typeof(TCapability)} is not defined on {typeof(TControl)}");
                c.SetValue(control, capability);
            }
            return control;
        }

        /// <summary> Adds all <paramref name="children"/> into control.Children (nulls are skipped). Returns <paramref name="control"/> for fluent API usage. </summary>
        public static T AppendChildren<T>(this T control, params DotvvmControl?[]? children)
            where T : DotvvmControl
        {
            if (children != null)
            {
                foreach (var child in children)
                {
                    if (child is not null)
                        control.Children.Add(child);
                }
            }

            return control;

        }

        /// <summary> Adds all <paramref name="children"/> into control.Children (nulls are skipped). Returns <paramref name="control"/> for fluent API usage. </summary>
        public static T AppendChildren<T>(this T control, IEnumerable<DotvvmControl?>? children)
            where T : DotvvmControl 
        {
            if (children != null)
            {
                foreach (var child in children)
                {
                    if (child is not null)
                        control.Children.Add(child);
                }
            }

            return control;
        }

        /// <summary>
        /// Gets the value binding set to the DotvvmProperty referenced in the lambda expression. Returns null if the property is not a binding, throws if the binding some kind of command.
        /// </summary>
        public static IValueBinding<TProperty>? GetValueBinding<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop)
            where TControl : DotvvmBindableObject
            => (IValueBinding<TProperty>?)control.GetValueBinding(control.GetDotvvmProperty(prop));

        /// <summary>
        /// Gets the command binding set to the DotvvmProperty referenced in the lambda expression. Returns null if the property is not a binding, throws if the binding is not command, controlCommand or staticCommand.
        /// </summary>
        public static ICommandBinding<TProperty>? GetCommandBinding<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop)
            where TControl : DotvvmBindableObject
            => (ICommandBinding<TProperty>?)control.GetCommandBinding(control.GetDotvvmProperty(prop));

        /// <summary> Returns the value of the DotvvmProperty referenced in the lambda expression. If the property contains a binding, it is evaluted. </summary>
        [return: MaybeNull]
        public static TProperty GetValue<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop)
            where TControl : DotvvmBindableObject
            => control.GetValue<TProperty>(control.GetDotvvmProperty(prop));

        /// <summary> Gets the value of the DotvvmProperty referenced in the lambda expression. Bindings are always returned, not evaluated. </summary>
        public static ValueOrBinding<TProperty> GetValueOrBinding<TControl, TProperty>(this TControl control, Expression<Func<TControl, TProperty>> prop)
            where TControl : DotvvmBindableObject
            => control.GetValueOrBinding<TProperty>(control.GetDotvvmProperty(prop));

        /// <summary> Returns the value of dotvvm with the specified name. If the property contains a binding, it is evaluted. </summary>
        [return: MaybeNull]
        public static TProperty GetValue<TProperty>(this DotvvmBindableObject control, string propName)
            => control.GetValue<TProperty>(control.GetDotvvmProperty(propName));

        /// <summary> Returns the value of dotvvm with the specified name. If the property contains a binding, it is evaluted. </summary>
        [return: MaybeNull]
        public static ValueOrBinding<TProperty> GetValueOrBinding<TProperty>(this DotvvmBindableObject control, string propName)
            => control.GetValueOrBinding<TProperty>(control.GetDotvvmProperty(propName));
            
        /// <summary>
        /// Gets the value binding set to dotvvm property of the specified <paramref name="propName" />. Returns null if the property is not a binding, throws if the binding some kind of command.
        /// </summary>
        public static IValueBinding<TProperty>? GetValueBinding<TProperty>(this DotvvmBindableObject control, string propName)
            => (IValueBinding<TProperty>?)control.GetValueBinding(control.GetDotvvmProperty(propName));

        /// <summary>
        /// Gets the command binding set to the dotvvm property of the specified <paramref name="propName" />. Returns null if the property is not a binding, throws if the binding is not command, controlCommand or staticCommand.
        /// </summary>
        public static ICommandBinding<TProperty>? GetCommandBinding<TProperty>(this DotvvmBindableObject control, string propName)
            => (ICommandBinding<TProperty>?)control.GetCommandBinding(control.GetDotvvmProperty(propName));

        /// <summary> Gets the specified control capability - reads all the properties in the capability at once. Throws if this control does not support the capability. </summary>
        public static TCapability GetCapability<TCapability>(this DotvvmBindableObject control)
        {
            var c = DotvvmCapabilityProperty.Find(control.GetType(), typeof(TCapability));
            if (c is null)
                throw new Exception($"Capability {typeof(TCapability)} is not defined on {control.GetType()}, or it's not uniquely determined");
            return (TCapability)c.GetValue(control)!;
        }

        /// <summary> Gets the specified control capability - reads all the properties in the capability at once. Throws if this control does not support the capability. </summary>
        public static TCapability GetCapability<TCapability>(this DotvvmBindableObject control, string prefix)
        {
            var c = DotvvmCapabilityProperty.Find(control.GetType(), typeof(TCapability), prefix);
            if (c is null)
                throw new Exception($"Capability {prefix}{typeof(TCapability)} is not defined on {control.GetType()}");
            return (TCapability)c.GetValue(control)!;
        }

        internal static object? TryGetValue(this DotvvmBindableObject control, DotvvmProperty property)
        {
            try
            {
                return control.GetValue(property);
            }
            catch
            {
                return property.DefaultValue;
            }
        }

        static string ValueDebugString(object? value)
        {
            if (value == null)
                return "<null>";
            if (value is ITemplate) 
                return "<a template>";
            if (value is DotvvmBindableObject)
                return $"<control {value.GetType().ToCode(stripNamespace: true)}>";
            if (value is IEnumerable<DotvvmBindableObject> controlCollection)
                return $"<{controlCollection.Count()} controls>";
            if (value is string valueStr)
                return valueStr;
            if (value is System.Collections.IDictionary dict)
            {
                var dictStr = dict.Keys.OfType<object>().Select(k => $"{ValueDebugString(k)}: {ValueDebugString(dict[k])}").StringJoin(", ");
                return $"{{ {dictStr} }}";
            }
            if (value is System.Collections.IEnumerable collection)
                return "[" + string.Join(", ", collection.OfType<object>().ToArray()) + "]";

            var toStringed = value.ToString() ?? "<man, please don't return null from ToString...>";
            var type = value.GetType();
            if (!type.IsPrimitive && toStringed == type.FullName)
                return value.GetType().ToCode(stripNamespace: true);

            return toStringed;
        }
        internal static (string? prefix, string tagName) FormatControlName(DotvvmBindableObject control, DotvvmConfiguration? config)
        {
            var type = control.GetType();
            if (type == typeof(HtmlGenericControl))
                return (null, ((HtmlGenericControl)control).TagName!);

            if (control is DotvvmMarkupControl && control.GetValue(Internal.MarkupFileNameProperty, inherit: false) is string markupFileName)
                return FormatMarkupControlName(markupFileName, config);
            return FormatControlName(type, config);
        }
        internal static (string? prefix, string tagName) FormatMarkupControlName(string fileName, DotvvmConfiguration? config)
        {
            var reg = config?.Markup.Controls.FirstOrDefault(c => c.Src?.Replace('\\', '/') == fileName.Replace('\\', '/'));
            if (reg is { TagName.Length: >0 })
                return (reg.TagPrefix, reg.TagName);
            else
                return (null, Path.GetFileNameWithoutExtension(fileName));
        }
        internal static (string? prefix, string tagName) FormatControlName(Type type, DotvvmConfiguration? config)
        {
            var reg = config?.Markup.Controls.FirstOrDefault(c => c.Namespace == type.Namespace && Type.GetType(c.Namespace + "." + type.Name + ", " + c.Assembly) == type) ??
                      config?.Markup.Controls.FirstOrDefault(c => c.Namespace == type.Namespace) ??
                      config?.Markup.Controls.FirstOrDefault(c => c.Assembly == type.Assembly.GetName().Name);
            var ns = reg?.TagPrefix ?? type.Namespace switch {
                null => "_",
                "DotVVM.Framework.Controls" => "dot",
                "DotVVM.AutoUI.Controls" => "auto",
                "DotVVM.BusinessPack.Controls" or "DotVVM.BusinessPack.PostBackHandlers" => "bp",
                "DotVVM.BusinessPack.Controls.FilterOperators" => "op",
                "DotVVM.BusinessPack.Controls.FilterBuilderFields" => "fp",
                var x when x.StartsWith("DotVVM.Contrib.") => "dc",
                _ => "_"
            };
            var optionsAttribute = type.GetCustomAttribute<ControlMarkupOptionsAttribute>();
            return (ns, optionsAttribute?.PrimaryName ?? type.Name);
        }

        /// <summary> Returns somewhat readable string representing this dotvvm control. </summary>
        public static string DebugString(this DotvvmBindableObject control, DotvvmConfiguration? config = null, bool multiline = true, bool useHtml = false)
        {
            if (control == null) return "null";

            config = config ?? (control.TryGetValue(Internal.RequestContextProperty) as IDotvvmRequestContext)?.Configuration;

            var type = control.GetType();
            var properties = (from kvp in control.Properties
                              let p = kvp.Key
                              let rawValue = kvp.Value
                              where p.DeclaringType != typeof(Internal)
                              let isAttached = !p.DeclaringType.IsAssignableFrom(type)
                              orderby !isAttached, p.Name
                              let name = p is GroupedDotvvmProperty gp ? (propName: gp.PropertyGroup.Prefixes.First(), memberName: gp.GroupMemberName) : (p.Name, null)
                              let className = isAttached ? p.DeclaringType.Name : null
                              let value = ValueDebugString(rawValue)
                              let croppedValue = value.Length > 41 ? value.Substring(0, 40) + "…" : value
                              select new { p, className, propName = name.propName, memberName = name.memberName, croppedValue, value, isAttached }
                             ).ToArray();

            var location = (
                file: control.TryGetValue(Internal.MarkupFileNameProperty) as string,
                line: control.TryGetValue(Internal.MarkupLineNumberProperty) as int? ?? -1,
                nearestControlInMarkup: (string?)null
            );
            if (location.line < 0 && location.file is {})
            {
                // line is not normally inherited, but we can find it manually in an ancestor control
                var ancestor = control.GetAllAncestors().FirstOrDefault(c => c.TryGetValue(Internal.MarkupLineNumberProperty) is int line && line >= 0);
                if (ancestor is {} && location.file.Equals(ancestor.TryGetValue(Internal.MarkupFileNameProperty)))
                {
                    location.line = (int)ancestor.TryGetValue(Internal.MarkupLineNumberProperty)!;
                    var ancestorName = FormatControlName(ancestor, config);
                    location.nearestControlInMarkup = ancestorName.prefix is null ? ancestorName.tagName : $"{ancestorName.prefix}:{ancestorName.tagName}";
                }
            }

            var cname = FormatControlName(control, config);
            string dothtmlString;
            if (useHtml)
            {
                var tagName = cname.prefix is null ?
                    $"<span class='tag-name'>{WebUtility.HtmlEncode(cname.tagName)}</span>" :
                    $"<span class='tag-prefix'>{WebUtility.HtmlEncode(cname.prefix)}</span>:<span class='control-name'>{WebUtility.HtmlEncode(cname.tagName)}</span>";
                dothtmlString = $"&lt;<span class='tag' title='{WebUtility.HtmlEncode(type.ToCode())}'>{tagName}</span> ";
                var prefixLength = dothtmlString.Length;

                foreach (var p in properties)
                {
                    if (p != properties[0])
                    {
                        if (multiline)
                            dothtmlString += "<br />" + new string(' ', prefixLength);
                        else
                            dothtmlString += " ";
                    }
                    var name = (p.className is null ? "" : $"<span class='class-name'>{WebUtility.HtmlEncode(p.className)}</span>" + ".")
                        + $"<span class='property-name'>{WebUtility.HtmlEncode(p.propName)}</span>"
                        + (p.memberName is null ? "" : $"<span class='attribute-name'>{WebUtility.HtmlEncode(p.memberName)}</span>");

                    dothtmlString += $"{name}=<span class='attribute-value' title='{WebUtility.HtmlEncode(p.value)}'>{WebUtility.HtmlEncode(p.croppedValue)}</span>";
                }
                dothtmlString += " /&gt;";
            }
            else
            {
                var tagName = cname.prefix is null ? cname.tagName : cname.prefix + ":" + cname.tagName;
                dothtmlString = $"<{tagName} ";
                var prefixLength = dothtmlString.Length;

                foreach (var p in properties)
                {
                    if (p != properties[0])
                    {
                        if (multiline)
                            dothtmlString += "\n" + new string(' ', prefixLength);
                        else
                            dothtmlString += " ";
                    }
                    var name = (p.className is null ? "" : p.className + ".") + p.propName + p.memberName;
                    dothtmlString += $"{name}={p.croppedValue}";
                }
                dothtmlString += " />";
            }
            
            var fileLocation = (location.file)
                     + (location.line >= 0 ? ":" + location.line : "")
                     + (location.nearestControlInMarkup is null || !multiline ? "" : $" (nearest dothtml control is <{location.nearestControlInMarkup}>)");

            if (useHtml)
            {
                dothtmlString = $"<code class='element'>{dothtmlString}</code>";

                if (!string.IsNullOrWhiteSpace(fileLocation))
                {
                    fileLocation = $"<code class='location'>{WebUtility.HtmlEncode(fileLocation)}</code>";
                }
            }


            if (!string.IsNullOrWhiteSpace(fileLocation))
            {
                var endOfLine = useHtml ? "<br />" : Environment.NewLine;
                return dothtmlString + (multiline ? endOfLine : " ") + "from " + fileLocation;
            }
            else
            {
                return dothtmlString;
            }
        }
    }
}
