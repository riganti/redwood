using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using DotVVM.Framework.Compilation.Javascript;
using FastExpressionCompiler;
using DotVVM.Framework.ViewModel;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// A control that represents plain HTML tag.
    /// </summary>
    public class HtmlGenericControl : DotvvmControl, IControlWithHtmlAttributes, IObjectWithCapability<HtmlCapability>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HtmlGenericControl"/> class.
        /// </summary>
#pragma warning disable CS8618 // TagName should not be null, but unfortunately is not initialized
        public HtmlGenericControl(bool allowImplicitLifecycleRequirements = true)
#pragma warning restore CS8618
        {
            if (allowImplicitLifecycleRequirements && GetType() == typeof(HtmlGenericControl))
            {
                LifecycleRequirements = ControlLifecycleRequirements.None;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HtmlGenericControl"/> class.
        /// </summary>
        public HtmlGenericControl(string? tagName, bool allowImplicitLifecycleRequirements = true) : this(allowImplicitLifecycleRequirements)
        {
            if (tagName is not null && string.IsNullOrWhiteSpace(tagName))
            {
                throw new DotvvmControlException("The tagName must not be empty!");
            }

            TagName = tagName;

            if (tagName == "head")
            {
                SetValue(RenderSettings.ModeProperty, RenderMode.Server);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HtmlGenericControl"/> class.
        /// </summary>
        public HtmlGenericControl(string? tagName, HtmlCapability? html, bool allowImplicitLifecycleRequirements = true) : this(tagName, allowImplicitLifecycleRequirements)
        {
            if (html is {})
                HtmlCapabilityProperty.SetValue(this, html);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HtmlGenericControl"/> class.
        /// </summary>
        public HtmlGenericControl(string? tagName, TextOrContentCapability? content, HtmlCapability? html = null)
        {
            if (GetType() != typeof(HtmlGenericControl))
                throw new("HtmlGenericControl can only use InnerText (and thus TextOrContentCapability) property when used directly, it cannot be inherited.");
            if (tagName?.Trim() == "")
            {
                throw new DotvvmControlException("The tagName must not be empty!");
            }

            TagName = tagName;

            if (tagName == "head")
            {
                SetValue(RenderSettings.ModeProperty, RenderMode.Server);
            }
            LifecycleRequirements = ControlLifecycleRequirements.None;

            content?.WriteToChildren(this, InnerTextProperty);

            if (html is {})
                HtmlCapabilityProperty.SetValue(this, html);
        }

        /// <summary> A dictionary of html attributes that are rendered on this control's html tag. </summary>
        [PropertyGroup(new[] { "", "html:" })]
        public VirtualPropertyGroupDictionary<object?> Attributes => new(this, AttributesGroupDescriptor);

        /// <summary> A dictionary of html attributes that are rendered on this control's html tag. </summary>
        [MarkupOptions(MappingMode = MappingMode.Attribute, AllowBinding = true, AllowHardCodedValue = true, AllowValueMerging = true, AttributeValueMerger = typeof(HtmlAttributeValueMerger), AllowAttributeWithoutValue = true)]
        public static DotvvmPropertyGroup AttributesGroupDescriptor =
            DotvvmPropertyGroup.Register<object, HtmlGenericControl>(new [] { "", "html:" }, nameof(Attributes));

        /// <summary> A dictionary of css classes. All classes whose value is `true` will be placed in the `class` attribute. </summary>
        [PropertyGroup("Class-", ValueType = typeof(bool))]
        public VirtualPropertyGroupDictionary<bool> CssClasses => new(this, CssClassesGroupDescriptor);

        /// <summary> A dictionary of css classes. All classes whose value is `true` will be placed in the `class` attribute. </summary>
        public static DotvvmPropertyGroup CssClassesGroupDescriptor =
            DotvvmPropertyGroup.Register<bool, HtmlGenericControl>("Class-", nameof(CssClasses));

        /// <summary> A dictionary of css styles which will be placed in the `style` attribute. </summary>
        [PropertyGroup("Style-")]
        public VirtualPropertyGroupDictionary<object> CssStyles => new(this, CssStylesGroupDescriptor);

        /// <summary> A dictionary of css styles which will be placed in the `style` attribute. </summary>
        public static DotvvmPropertyGroup CssStylesGroupDescriptor =
            DotvvmPropertyGroup.Register<object, HtmlGenericControl>("Style-", nameof(CssStyles));

        /// <summary>
        /// Gets or sets the inner text of the HTML element. Note that this property can only be used on HtmlGenericControl directly and when the control does not have any children.
        /// </summary>
        public string? InnerText
        {
            get { return (string?)GetValue(InnerTextProperty); }
            set { SetValue(InnerTextProperty, value); }
        }

        public static readonly DotvvmProperty InnerTextProperty =
            DotvvmProperty.Register<string?, HtmlGenericControl>(t => t.InnerText, null);

        /// <summary>
        /// Gets the tag name.
        /// </summary>
        [MarkupOptions(MappingMode = MappingMode.Exclude)]
        public string? TagName { get; protected set; }

        /// <summary>
        /// Gets or sets whether the control is visible. When set to false, `style="display: none"` will be added to this control.
        /// </summary>
        [MarkupOptions]
        public bool Visible
        {
            get { return (bool)GetValue(VisibleProperty)!; }
            set { SetValue(VisibleProperty, value); }
        }

        public static readonly DotvvmProperty VisibleProperty =
            DotvvmProperty.Register<bool, HtmlGenericControl>(t => t.Visible, true);


        public HtmlCapability HtmlCapability
        {
            get => (HtmlCapability)this.GetValue(HtmlCapabilityProperty)!;
            set => this.SetValue(HtmlCapabilityProperty, value);
        }
        public static readonly DotvvmCapabilityProperty HtmlCapabilityProperty =
            DotvvmCapabilityProperty.RegisterCapability<HtmlCapability, HtmlGenericControl>();

        /// <summary>
        /// Gets a value whether this control renders a HTML tag.
        /// </summary>
        protected virtual bool RendersHtmlTag => TagName is object;

        protected new struct RenderState
        {
            public object? Visible;
            public object? ClientId;
            public object? InnerText;
            public bool HasId;
            public bool HasClass;
            public bool HasStyle;
            public bool HasAttributes;
            public bool HasPostbackUpdate;
            public bool RendersHtmlTag;

            private byte _renderOnServer;

            public bool RenderOnServer(HtmlGenericControl @this)
            {
                if (_renderOnServer == 0)
                    _renderOnServer = @this.RenderOnServer ? (byte)1 : (byte)2;
                return _renderOnServer == 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool TouchProperty(DotvvmPropertyId prop, object? value, ref RenderState r)
        {
            if (prop == VisibleProperty.Id)
                r.Visible = value;
            else if (prop == ClientIDProperty.Id)
                r.ClientId = value;
            else if (prop == IDProperty.Id && value != null)
                r.HasId = true;
            else if (prop == InnerTextProperty.Id)
                r.InnerText = value;
            else if (prop == PostBack.UpdateProperty.Id)
                r.HasPostbackUpdate = (bool)this.EvalPropertyValue(prop, value)!;
            else if (prop.IsPropertyGroup)
            {
                if (prop.IsInPropertyGroup(CssClassesGroupDescriptor.Id))
                    r.HasClass = true;
                else if (prop.IsInPropertyGroup(CssStylesGroupDescriptor.Id))
                    r.HasStyle = true;
                else if (prop.IsInPropertyGroup(AttributesGroupDescriptor.Id))
                    r.HasAttributes = true;
                else return false;
            }
            else return false;
            return true;
        }

        /// <summary>
        /// Adds all attributes that should be added to the control begin tag.
        /// </summary>
        protected override void AddAttributesToRender(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            RenderState r = default;
            foreach (var (prop, val) in this.properties)
                TouchProperty(prop, val, ref r);
            r.RendersHtmlTag = this.RendersHtmlTag;

            AddAttributesCore(writer, ref r);

            base.AddAttributesToRender(writer, context);
        }

        protected void AddAttributesCore(IHtmlWriter writer, ref RenderState r)
        {
            CheckInnerTextUsage(in r);

            if (!r.RendersHtmlTag)
            {
                // the control renders no html tag and therefore if it has any attributes it should throw an exception
                EnsureNoAttributesSet(in r);
            }
            else
            {
                TryUseLiteralAsInnerText(ref r);

                if (r.HasClass)
                    AddCssClassesToRender(writer);
                if (r.HasStyle)
                    AddCssStylesToRender(writer);
                AddVisibleAttributeOrBinding(in r, writer);
                AddTextPropertyToRender(ref r, writer);
                AddHtmlAttributesToRender(ref r, writer);
            }
        }

        /// <summary>
        /// Adds the corresponding attribute or binding for the Visible property.
        /// </summary>
        protected virtual void AddVisibleAttributeOrBinding(in RenderState r, IHtmlWriter writer)
        {
            var v = r.Visible;
            var valueBinding = v as IValueBinding;
            if (valueBinding is {})
            {
                writer.AddKnockoutDataBind("visible", valueBinding.GetKnockoutBindingExpression(this));
            }

            if (false.Equals(KnockoutHelper.TryEvaluateValueBinding(this, v)))
            {
                writer.AddAttribute("style", "display:none");
            }
        }

        /// <summary>
        /// Verifies that the control hasn't any HTML attributes, css classes or Visible or DataContext bindings set.
        /// </summary>
        private void EnsureNoAttributesSet(in RenderState r)
        {
            if (r.HasAttributes || r.HasClass || r.HasStyle || (r.Visible != null && !true.Equals(r.Visible)) || r.HasPostbackUpdate)
            {
                throw new DotvvmControlException(this, "Cannot set HTML attributes, Visible, ID, Postback.Update, ... bindings on a control which does not render its own element!");
            }
        }

        /// <summary>
        /// Renders the control begin tag if <see cref="RendersHtmlTag" /> == true.
        /// </summary>
        protected override void RenderBeginTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            if (RendersHtmlTag)
            {
                writer.RenderBeginTag(TagName!);
            }
        }

        /// <summary>
        /// Renders the control end tag if <see cref="RendersHtmlTag" /> == true. Also renders required resource i before the end tag, if it is a `head` or `body` element
        /// </summary>
        protected override void RenderEndTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            // render resource link. If the Render is invoked multiple times the resources are rendered on the first invocation.
            if (TagName == "head")
                new HeadResourceLinks().Render(writer, context);
            else if (TagName == "body")
                new BodyResourceLinks().Render(writer, context);

            if (RendersHtmlTag)
            {
                writer.RenderEndTag();
            }
        }

        private void AddCssClassesToRender(IHtmlWriter writer)
        {
            KnockoutBindingGroup cssClassBindingGroup = new KnockoutBindingGroup();
            foreach (var (cssClass, rawValue) in CssClasses.RawValues)
            {
                if (rawValue is IValueBinding binding)
                {
                    cssClassBindingGroup.Add(cssClass, this, binding);
                }

                if (true.Equals(KnockoutHelper.TryEvaluateValueBinding(this, rawValue)))
                    writer.AddAttribute("class", cssClass, append: true, appendSeparator: " ");
            }

            if (!cssClassBindingGroup.IsEmpty) writer.AddKnockoutDataBind("css", cssClassBindingGroup);
        }

        private void AddCssStylesToRender(IHtmlWriter writer)
        {
            KnockoutBindingGroup? cssStylesBindingGroup = null;
            foreach (var (style, rawValue) in CssStyles.RawValues)
            {
                if (rawValue is IValueBinding binding)
                {
                    if (cssStylesBindingGroup == null) cssStylesBindingGroup = new KnockoutBindingGroup();
                    cssStylesBindingGroup.Add(style, this, binding);
                }

                var value = KnockoutHelper.TryEvaluateValueBinding(this, rawValue)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    writer.AddStyleAttribute(style, value!);
                }
            }

            if (cssStylesBindingGroup != null)
            {
                writer.AddKnockoutDataBind("style", cssStylesBindingGroup);
            }
        }

        private void AddHtmlAttribute(IHtmlWriter writer, string name, object? value)
        {
            if (value is null)
                writer.AddAttribute(name, null, append: true);
            if (value is string str)
            {
                writer.AddAttribute(name, str, append: true);
            }
            else if (value is AttributeList list)
            {
                for (var i = list; i is not null; i = i.Next)
                {
                    AddHtmlAttribute(writer, name, i.Value);
                }
            }
            else if (value is IStaticValueBinding binding)
            {
                var evaluatedBinding = binding.Evaluate(this);
                // while directly set null is written out as empty attribute, null returned
                // from a binding is skipped. This allows binding to conditionally add attributes.
                // * direct null behave this way for historical reasons
                // * binding null behaves this way because it's what knockout.js does client-side anyway
                if (evaluatedBinding is not null)
                {
                    AddHtmlAttribute(writer, name, evaluatedBinding);
                }
            }
            else if (value is bool boolValue)
            {
                if (boolValue)
                {
                    writer.AddAttribute(name, name);
                }
            }
            else
            {
                writer.AddAttribute(name, AttributeValueToString(value), append: true);
            }
        }

        private static string AttributeValueToString(object? value) =>
            value switch {
                null => "",
                string str => str,
                Enum enumValue => ReflectionUtils.ToEnumString(enumValue.GetType(), enumValue.ToString()),
                Guid guid => guid.ToString(),
                _ when ReflectionUtils.IsNumericType(value.GetType()) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
                IDotvvmPrimitiveType => value switch {
                    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                    _ => value.ToString() ?? ""
                },
                System.Collections.IEnumerable =>
                    throw new NotSupportedException($"Attribute value of type '{value.GetType().ToCode(stripNamespace: true)}' is not supported. Consider concatenating the values into a string or use the HtmlGenericControl.AttributeList if you need to pass multiple values."),
                _ =>

                    // DateTime and related are not supported here intentionally.
                    // It is not clear in which format it should be rendered - on some places, the HTML specs requires just yyyy-MM-dd,
                    // but in case of Web Components, the users may want to pass the whole date, or use a specific format

                    throw new NotSupportedException($"Attribute value of type '{value.GetType().ToCode(stripNamespace: true)}' is not supported. Please convert the value to string, e. g. by using ToString()")
            };

        private void AddHtmlAttributesToRender(ref RenderState r, IHtmlWriter writer)
        {
            KnockoutBindingGroup? attributeBindingGroup = null;

            if (r.HasAttributes) foreach (var (attributeName, valueRaw) in this.Attributes.RawValues)
            {
                var knockoutExpression = valueRaw switch {
                    AttributeList list => list.GetKnockoutBindingExpression(this, HtmlWriter.GetSeparatorForAttribute(attributeName)),
                    IValueBinding binding => binding.GetKnockoutBindingExpression(this),
                    _ => null
                };

                if (knockoutExpression is {})
                {
                    if (attributeName.Equals("class", StringComparison.OrdinalIgnoreCase))
                    {
                        writer.AddKnockoutDataBind("class", knockoutExpression);
                    }
                    else
                    {
                        attributeBindingGroup ??= new KnockoutBindingGroup();
                        attributeBindingGroup.Add(attributeName, knockoutExpression);
                    }
                }

                try
                {
                    AddHtmlAttribute(writer, attributeName, valueRaw);
                }
                catch (Exception) when (knockoutExpression is {})
                {
                    // suppress errors in value bindings
                }

                if (attributeName.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    // to avoid rendering the ID twice we set the HasId property to false, to ensure that the following block does not render the ID
                    r.HasId = false;
                }
            }

            if (r.HasId)
            {
                var clientId = r.ClientId ?? CreateClientId()?.UnwrapToObject();
                if (clientId is IValueBinding binding)
                {
                    if (attributeBindingGroup == null) attributeBindingGroup = new KnockoutBindingGroup();
                    attributeBindingGroup.Add("id", binding.GetKnockoutBindingExpression(this));
                }
                else
                {
                    // TODO: we currently don't support server-side rendering of value binding IDs
                    AddHtmlAttribute(writer, "id", clientId);
                }
            }

            if (attributeBindingGroup != null)
            {
                writer.AddKnockoutDataBind("attr", attributeBindingGroup);
            }
        }

        /// Tries to get Literal element from Children and set its value binding into r.InnerText
        /// This leads to less knockout comments being produced
        void TryUseLiteralAsInnerText(ref RenderState r)
        {
            if (r.InnerText != null || Children.Count != 1)
                return;
            if (Children[0] is not Literal { RendersHtmlTag: false, FormatString: null or "" } literal)
                return;

            var textBinding = literal.GetValueRaw(Literal.TextProperty) as IValueBinding;
            if (textBinding is null || Literal.NeedsFormatting(textBinding))
                return;

            Children.Clear();
            r.InnerText = textBinding;
        }

        private void AddTextPropertyToRender(ref RenderState r, IHtmlWriter writer)
        {
            if (r.InnerText == null) return;
            var expression = r.InnerText as IValueBinding;
            if (expression != null)
            {
                writer.AddKnockoutDataBind("text", expression.GetKnockoutBindingExpression(this));
            }

            if (expression == null || r.RenderOnServer(this))
            {
                var value = this.EvalPropertyValue(InnerTextProperty, r.InnerText)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    Children.Clear();
                    Children.Add(new Literal(value));
                }
            }
        }

        /// <summary>
        /// Checks the inner text property usage.
        /// </summary>
        private void CheckInnerTextUsage(in RenderState r)
        {
            if (r.InnerText != null && GetType() != typeof(HtmlGenericControl))
            {
                throw new DotvvmControlException(this, "The DotVVM controls do not support the 'InnerText' property. It can be only used on HTML elements.");
            }
        }


        /// <summary> Linked list of attribute values, used when at least one attribute value is a binding, so the values can't be concatenated. </summary>
        public sealed record AttributeList(object Value, AttributeList? Next)
        {
            /// <summary> Returns concatenation expression from all the list values. If the list contains no bindings, returns null. </summary>
            public string? GetKnockoutBindingExpression(DotvvmBindableObject c, string separator)
            {
                var separatorLiteral = KnockoutHelper.MakeStringLiteral(separator);
                var sb = new StringBuilder();

                var hasBinding = false;
                bool needsSeparator = false;
                for (var i = this; i != null; i = i.Next)
                {
                    var isLast = i.Next == null;
                    if (i.Value is IValueBinding binding)
                    {
                        var koExpression = binding.UnwrappedKnockoutExpression;
                        hasBinding = true;
                        if (needsSeparator)
                            sb.Append($"{separatorLiteral}+");

                        var needsParens = koExpression.OperatorPrecedence.NeedsParens(parentPrecedence: OperatorPrecedence.Addition);

                        if (needsParens)
                            sb.Append('(');

                        sb.Append(koExpression.FormatKnockoutScript(c, binding));
                        needsSeparator = true;

                        if (needsParens)
                            sb.Append(')');
                    }
                    else
                    {
                        var value = AttributeValueToString(
                            i.Value is IStaticValueBinding staticValue ? staticValue.Evaluate(c) : i.Value);
                        if (needsSeparator)
                            value = separator + value;
                        if (!isLast)
                            // prefer to join the separator with a constant value to avoid unnecessary string concatenation in the generated code
                            value = value + separator;

                        sb.Append(KnockoutHelper.MakeStringLiteral(value));
                    }

                    if (!isLast)
                        sb.Append("+");
                }

                if (!hasBinding) return null;
                else return sb.ToString();
            }

            public override string ToString()
            {
                var sb = new StringBuilder().Append("[ ");
                for (var i = this; i != null; i = i.Next)
                {
                    if (i != this)
                        sb.Append(", ");
                    sb.Append(i.Value);
                }
                return sb.Append(" ]").ToString();
            }
        }
    }


    [DotvvmControlCapability]
    public sealed record HtmlCapability
    {
        /// <summary> Gets or sets a dictionary of HTML attributes that are rendered on this control HTML tag. </summary>
        [PropertyGroup("", "html:")]
        [MarkupOptions(MappingMode = MappingMode.Attribute, AllowBinding = true, AllowHardCodedValue = true, AllowValueMerging = true, AttributeValueMerger = typeof(HtmlAttributeValueMerger), AllowAttributeWithoutValue = true)]
        public IDictionary<string, ValueOrBinding<object?>> Attributes { get; init; } = new Dictionary<string, ValueOrBinding<object?>>();

        /// <summary> Gets or sets a dictionary of CSS classes. All classes which value is `true` will be placed in the `class` attribute. </summary>
        [PropertyGroup("Class-")]
        public IDictionary<string, ValueOrBinding<bool>> CssClasses { get; init; } = new Dictionary<string, ValueOrBinding<bool>>();

        /// <summary> Gets or sets the ID of the control. Based on the `ClientIDMode` property, the value may be prefixed by DotVVM in order to be unique. </summary>
        public ValueOrBinding<string?> ID { get; init; }

        /// <summary> Returns true if all properties are set to default value </summary>
        public bool IsEmpty() =>
            Attributes.Count == 0 &&
            CssClasses.Count == 0 &&
            CssStyles.Count == 0 &&
            Visible.HasValue && Visible.ValueOrDefault == true &&
            ID.HasValue && ID.ValueOrDefault == null;

        /// <summary> Gets or sets a dictionary of CSS styles which will be placed in the `style` attribute. </summary>
        [PropertyGroup("Style-")]
        public IDictionary<string, ValueOrBinding<object?>> CssStyles { get; init; } = new Dictionary<string, ValueOrBinding<object?>>();

        /// <summary> Gets or sets whether the control is visible. When set to false, `style="display: none"` will be added to this control. </summary>
        public ValueOrBinding<bool> Visible { get; init; } = new(true);
    }

    [DotvvmControlCapability]
    public sealed record WrapperCapability
    {
        /// <summary>
        /// Gets or sets the name of the wrapper element.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string? WrapperTagName { get; init; }

        public HtmlCapability Html { get; init; } = new HtmlCapability();

        public DotvvmControl GetWrapper()
        {
            return string.IsNullOrEmpty(WrapperTagName)
                ? new PlaceHolder()
                : new HtmlGenericControl(WrapperTagName, Html);
        }
    }
}
