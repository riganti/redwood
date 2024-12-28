using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DotVVM.Framework.Compilation.Javascript;
using System.Runtime.CompilerServices;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls
{
    [Flags]
    public enum ControlLifecycleRequirements : short
    {
        None = 0,
        RealtimePreInit = 1 << 0,
        RealtimeInit = 1 << 1,
        RealtimeLoad = 1 << 2,
        RealtimePreRender = 1 << 3,
        RealtimePreRenderComplete = 1 << 4,
        InvokeMissingPreInit = 1 << 5,
        InvokeMissingInit = 1 << 6,
        InvokeMissingLoad = 1 << 7,
        InvokeMissingPreRender = 1 << 8,
        InvokeMissingPreRenderComplete = 1 << 9,

        OnlyRealtime = RealtimePreInit | RealtimeInit | RealtimeLoad | RealtimePreRender | RealtimePreRenderComplete,
        OnlyMissing = InvokeMissingPreInit | InvokeMissingInit | InvokeMissingLoad | InvokeMissingPreRender | InvokeMissingPreRenderComplete,
        All = OnlyRealtime | OnlyMissing,

        PreInit = RealtimePreInit | InvokeMissingPreInit,
        Init = RealtimeInit | InvokeMissingInit,
        Load = RealtimeLoad | InvokeMissingLoad,
        PreRender = RealtimePreRender | InvokeMissingPreRender,
        PreRenderComplete = RealtimePreRenderComplete | InvokeMissingPreRenderComplete,
    }

    /// <summary>
    /// Represents a base class for all DotVVM controls.
    /// </summary>
    public abstract class DotvvmControl : DotvvmBindableObject, IDotvvmControl
    {
        /// <summary>
        /// Gets the child controls.
        /// </summary>
        [MarkupOptions(MappingMode = MappingMode.Exclude)]
        public DotvvmControlCollection Children { get; private set; }

        // automatically assign requirements
        public ControlLifecycleRequirements LifecycleRequirements = ControlLifecycleRequirements.Init | ControlLifecycleRequirements.Load | ControlLifecycleRequirements.PreRender;

        /// <summary>
        /// Gets or sets the control client ID within its naming container.
        /// </summary>
        [MarkupOptions]
        public string? ID
        {
            get { return (string?)GetValue(IDProperty); }
            set { SetValue(IDProperty, value); }
        }

        public static readonly DotvvmProperty IDProperty =
            DotvvmProperty.Register<string?, DotvvmControl>(c => c.ID, isValueInherited: false);

        /// <summary>
        /// Gets the calculated client ID of the control that will be rendered in the 'id' attribute. Returns null if the ID property is not set.
        /// </summary>
        [MarkupOptions(MappingMode = MappingMode.Exclude)]
        public ValueOrBinding<string>? ClientID => EnsureClientId();
        public static readonly DotvvmProperty ClientIDProperty
            = DotvvmProperty.Register<string?, DotvvmControl>(c => c.ClientID, null);

        ValueOrBinding<string>? EnsureClientId()
        {
            if (!IsPropertySet(IDProperty))
            {
                return null;
            }
            if (IsPropertySet(ClientIDProperty))
            {
                return GetValueOrBinding<string>(ClientIDProperty);
            }

            var id = CreateClientId();
            SetValue(ClientIDProperty, id?.UnwrapToObject());
            return id;
        }


        /// <summary>
        /// Gets or sets the client ID generation algorithm.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public ClientIDMode ClientIDMode
        {
            get { return (ClientIDMode)GetValue(ClientIDModeProperty)!; }
            set { SetValue(ClientIDModeProperty, value); }
        }

        public static readonly DotvvmProperty ClientIDModeProperty =
            DotvvmProperty.Register<ClientIDMode, DotvvmControl>(c => c.ClientIDMode, ClientIDMode.Static, isValueInherited: true);

        /// <summary>
        /// Gets or sets whether the control is included in the DOM of the page.
        /// </summary>
        /// <remarks>
        /// Essentially wraps Knockout's 'if' binding.
        /// </remarks>
        [MarkupOptions]
        public bool IncludeInPage
        {
            get { return (bool)GetValue(IncludeInPageProperty)!; }
            set { SetValue(IncludeInPageProperty, value); }
        }

        DotvvmControlCollection IDotvvmControl.Children => throw new NotImplementedException();

        ClientIDMode IDotvvmControl.ClientIDMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        string IDotvvmControl.ID { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        DotvvmBindableObject? IDotvvmControl.Parent { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public static readonly DotvvmProperty IncludeInPageProperty =
            DotvvmProperty.Register<bool, DotvvmControl>(t => t.IncludeInPage, true);

        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmControl"/> class.
        /// </summary>
        public DotvvmControl()
        {
            Children = new DotvvmControlCollection(this);
        }

        /// <summary>
        /// Gets this control and all of its descendants.
        /// </summary>
        public IEnumerable<DotvvmControl> GetThisAndAllDescendants(Func<DotvvmControl, bool>? enumerateChildrenCondition = null)
        {
            // PERF: non-linear complexity
            yield return this;
            if (enumerateChildrenCondition == null || enumerateChildrenCondition(this))
            {
                foreach (var descendant in GetAllDescendants(enumerateChildrenCondition))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Gets all descendant controls of this control.
        /// </summary>
        public IEnumerable<DotvvmControl> GetAllDescendants(Func<DotvvmControl, bool>? enumerateChildrenCondition = null)
        {
            // PERF: non-linear complexity
            foreach (var child in Children)
            {
                yield return child;

                if (enumerateChildrenCondition == null || enumerateChildrenCondition(child))
                {
                    foreach (var grandChild in child.GetAllDescendants(enumerateChildrenCondition))
                    {
                        yield return grandChild;
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the control has only white space content.
        /// </summary>
        public bool HasOnlyWhiteSpaceContent() =>
            Children.HasOnlyWhiteSpaceContent();

        /// <summary>
        /// Renders the control into the specified writer.
        /// </summary>
        public virtual void Render(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            this.Children.ValidateParentsLifecycleEvents(); // debug check

            writer.SetErrorContext(this);

            if (properties.Contains(PostBack.UpdateProperty))
            {
                AddDotvvmUniqueIdAttribute();
            }

            try
            {
                RenderControl(writer, context);
            }
            catch (Exception e)
            {
                if (e is IDotvvmException { RelatedControl: not null })
                    throw;
                if (e is DotvvmExceptionBase dotvvmException)
                {
                    dotvvmException.RelatedControl = this;
                    throw;
                }
                throw new DotvvmControlException(this, "Error occurred in Render method", e);
            }
        }

        /// <summary>
        /// Adds 'data-dotvvm-id' attribute with generated unique id to the control. You can find control by this id using FindControlByUniqueId method.
        /// </summary>
        protected void AddDotvvmUniqueIdAttribute()
        {
            var htmlAttributes = this as IControlWithHtmlAttributes;
            if (htmlAttributes == null)
            {
                throw new DotvvmControlException(this, "Postback.Update cannot be set on property which don't render html attributes.");
            }
            htmlAttributes.Attributes.Set("data-dotvvm-id", GetDotvvmUniqueId().UnwrapToObject());
        }

        protected struct RenderState
        {
            internal object? IncludeInPage;
            internal IValueBinding? DataContext;
            internal bool HasActives;
            internal bool HasActiveGroups;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool TouchProperty(DotvvmPropertyId property, object? val, ref RenderState r)
        {
            if (property == DotvvmControl.IncludeInPageProperty.Id)
                r.IncludeInPage = val;
            else if (property == DotvvmControl.DataContextProperty.Id)
                r.DataContext = val as IValueBinding;
            else if (DotvvmPropertyIdAssignment.IsActive(property))
            {
                if (property.IsPropertyGroup)
                    r.HasActiveGroups = true;
                else
                    r.HasActives = true;
            }
            else return false;
            return true;
        }
        /// <returns>true means that rendering of the rest of this control should be skipped</returns>
        protected bool RenderBeforeControl(in RenderState r, IHtmlWriter writer, IDotvvmRequestContext context)
        {
            if (r.IncludeInPage != null && !(r.IncludeInPage is IValueBinding) && false.Equals(this.GetValue(IncludeInPageProperty)))
                return true;

            if (r.DataContext != null)
            {
                var parent = Parent ?? throw new DotvvmControlException(this, "Cannot set DataContext binding on the root control");
                writer.WriteKnockoutWithComment(r.DataContext.GetKnockoutBindingExpression(parent));
            }

            if (r.IncludeInPage != null && r.IncludeInPage is IValueBinding binding)
            {
                writer.WriteKnockoutDataBindComment("if", binding.GetKnockoutBindingExpression(this));
            }

            if (r.HasActives) foreach (var item in properties)
            {
                if (!item.Key.IsPropertyGroup && DotvvmPropertyIdAssignment.IsActive(item.Key))
                {
                    ((ActiveDotvvmProperty)item.Key.PropertyInstance).AddAttributesToRender(writer, context, this);
                }
            }

            if (r.HasActiveGroups)
            {
                var groups = properties
                    .Where(p => p.Key.PropertyGroupInstance is ActiveDotvvmPropertyGroup)
                    .GroupBy(p => (ActiveDotvvmPropertyGroup)p.Key.PropertyGroupInstance!);
                foreach (var item in groups)
                {
                    item.Key.AddAttributesToRender(writer, context, this, item.Select(i => i.Key.PropertyInstance));
                }
            }

            return false;
        }

        protected void RenderAfterControl(in RenderState r, IHtmlWriter writer)
        {
            if (r.DataContext != null)
            {
                writer.WriteKnockoutDataBindEndComment();
            }

            if (r.IncludeInPage != null && r.IncludeInPage is IValueBinding binding)
            {
                writer.WriteKnockoutDataBindEndComment();
            }
        }


        /// <summary>
        /// Renders the control into the specified writer.
        /// </summary>
        protected virtual void RenderControl(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            RenderState r = default;
            foreach (var (prop, value) in properties)
                TouchProperty(prop, value, ref r);
            if (RenderBeforeControl(in r, writer, context))
                return;

            AddAttributesToRender(writer, context);
            RenderBeginTag(writer, context);
            RenderContents(writer, context);
            RenderEndTag(writer, context);

            RenderAfterControl(in r, writer);
        }

        /// <summary>
        /// Adds all attributes that should be added to the control begin tag.
        /// </summary>
        protected virtual void AddAttributesToRender(IHtmlWriter writer, IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Renders the control begin tag.
        /// </summary>
        protected virtual void RenderBeginTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Renders the contents inside the control begin and end tags.
        /// </summary>
        protected virtual void RenderContents(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            RenderChildren(writer, context);
        }

        /// <summary>
        /// Renders the control end tag.
        /// </summary>
        protected virtual void RenderEndTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Renders the children.
        /// </summary>
        protected void RenderChildren(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            foreach (var child in Children)
            {
                child.Render(writer, context);
            }
        }

        [Obsolete("Use FindControlInContainer instead. Or FindControlByClientId if you want to be limited only to this container.")]
        public DotvvmControl? FindControl(string id, bool throwIfNotFound = false) => FindControlInContainer(id, throwIfNotFound);
        [Obsolete("Use FindControlInContainer instead. Or FindControlByClientId if you want to be limited only to this container.")]
        public T FindControl<T>(string id, bool throwIfNotFound = false) where T : DotvvmControl => FindControlInContainer<T>(id, throwIfNotFound);

        /// <summary>
        /// Finds a control by its ID coded in markup. Does not recurse into naming containers. Returns null if the <paramref name="throwIfNotFound" /> is false and the control is not found.
        /// </summary>
        public DotvvmControl? FindControlInContainer(string id, bool throwIfNotFound = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            var control = GetAllDescendants(c => !IsNamingContainer(c)).SingleOrDefault(c => c.ID == id);
            if (control == null && throwIfNotFound)
            {
                throw new Exception(string.Format("The control with ID '{0}' was not found.", id));
            }
            return control;
        }

        /// <summary>
        /// Finds a control by its ID coded in markup. Does not recurse into naming containers.
        /// </summary>
        public T FindControlInContainer<T>(string id, bool throwIfNotFound = false) where T : DotvvmControl
        {
            var control = FindControlInContainer(id, throwIfNotFound);
            if (!(control is T)) // TODO: this does not work
            {
                throw new DotvvmControlException(this, $"The control with ID '{id}' was found, however it is not an instance of the desired type '{typeof(T)}'.");
            }
            return (T)control;
        }

        /// <summary>
        /// Finds a control by its ClientId - the id rendered to output html.
        /// </summary>
        public DotvvmControl? FindControlByClientId(string id, bool throwIfNotFound = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            var control = GetAllDescendants().SingleOrDefault(c => c.ClientID?.ValueOrDefault == id);
            if (control == null && throwIfNotFound)
            {
                throw new Exception(string.Format("The control with ID '{0}' was not found.", id));
            }
            return control;
        }

        /// <summary>
        /// Finds a control by its ClientId - the id rendered to output html.
        /// </summary>
        public T FindControlByClientId<T>(string id, bool throwIfNotFound = false) where T : DotvvmControl
        {
            var control = FindControlByClientId(id, throwIfNotFound);
            if (!(control is T))
            {
                throw new DotvvmControlException(this, $"The control with ID '{id}' was found, however it is not an instance of the desired type '{typeof(T)}'.");
            }
            return (T)control;
        }

        /// <summary>
        /// Finds a control by its unique ID. Returns null if the control is not found.
        /// </summary>
        public DotvvmControl? FindControlByUniqueId(string controlUniqueId)
        {
            var parts = controlUniqueId.Split('_');
            DotvvmControl? result = this;
            for (var i = 0; i < parts.Length; i++)
            {
                var results = result.GetAllDescendants(c => !IsNamingContainer(c))
                    .Where(c => c.GetValue(Internal.UniqueIDProperty) as string == parts[i]).ToArray();
                if (results.Length == 0)
                {
                    return null;
                }
                if (results.Length > 1)
                {
                    throw new DotvvmControlException(results[0], $"Multiple controls with the same UniqueID '{string.Join("_", parts.Take(i + 1))}' were found:" +
                        string.Concat(results.Take(20).Select(c => "\n * " + c.DebugString(multiline: false))));
                }

                result = results[0];
            }
            return result;
        }

        /// <summary>
        /// Gets the naming container of the current control.
        /// </summary>
        public DotvvmControl GetNamingContainer()
        {
            var control = this;
            while (!IsNamingContainer(control) && control.Parent is DotvvmControl parent)
            {
                control = parent;
            }
            return control;
        }

        /// <summary>
        /// Determines whether the specified control is a naming container.
        /// </summary>
        public static bool IsNamingContainer(DotvvmBindableObject control)
        {
            return (bool)Internal.IsNamingContainerProperty.GetValue(control)!;
        }

        /// <summary>
        /// Occurs after the viewmodel tree is complete.
        /// </summary>
        internal virtual void OnPreInit(IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Called right before the rendering shall occur.
        /// </summary>
        internal virtual void OnPreRenderComplete(IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Occurs before the viewmodel is applied to the page.
        /// </summary>
        protected internal virtual void OnInit(IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Occurs after the viewmodel is applied to the page IHtmlWriter writer and before the commands are executed.
        /// </summary>
        protected internal virtual void OnLoad(IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Occurs after the page commands are executed.
        /// </summary>
        protected internal virtual void OnPreRender(IDotvvmRequestContext context)
        {
        }

        /// <summary>
        /// Gets the internal unique ID of the control. Returns either string or IValueBinding.
        /// </summary>
        public ValueOrBinding<string> GetDotvvmUniqueId(ValueOrBinding<string?> prefix = default, ValueOrBinding<string?> suffix = default) =>
            // build the client ID
            JoinValuesOrBindings(GetUniqueIdFragments(), prefix, suffix);

        private ValueOrBinding<string> JoinValuesOrBindings(IReadOnlyList<object?> fragments, ValueOrBinding<string?> prefix, ValueOrBinding<string?> suffix)
        {
            if (fragments.Count == 1 && prefix.ValueIsNullOrEmpty() && suffix.ValueIsNullOrEmpty())
            {
                return ValueOrBinding<string>.FromBoxedValue(fragments[0] ?? "");
            }
            else if (fragments.All(f => f is string or null) && prefix.HasValue && suffix.HasValue)
            {
                string result = prefix.ValueOrDefault + string.Join("_", fragments) + suffix.ValueOrDefault;
                return new(result);
            }
            else
            {
                BindingCompilationService? service = (prefix.BindingOrDefault ?? suffix.BindingOrDefault)?.GetProperty<BindingCompilationService>();
                var result = new ParametrizedCode.Builder();
                var first = true;
                if (!prefix.ValueIsNullOrEmpty())
                {
                    result.Add(prefix.GetJsExpression(this, unwrapped: true));
                    result.Add("+");
                    Debug.Assert(fragments.Any());
                }
                foreach (var f in fragments)
                {
                    if (!first | (first = false))
                        result.Add("+'_'+");

                    if (f is IValueBinding binding)
                    {
                        service ??= binding.GetProperty<BindingCompilationService>(ErrorHandlingMode.ReturnNull);
                        result.Add(binding.GetParametrizedKnockoutExpression(this, unwrapped: true), OperatorPrecedence.Addition);
                    }
                    else result.Add(JavascriptCompilationHelper.CompileConstant(f));
                }
                if (!suffix.ValueIsNullOrEmpty())
                {
                    Debug.Assert(fragments.Any());
                    result.Add("+");
                    result.Add(suffix.GetJsExpression(this, unwrapped: true));
                }
                if (service is null)
                    throw new NotSupportedException("Could not generate control ID, there is a binding in the fragments, but it does not have a binding compilation service. Fragments: " + string.Join(", ", fragments));

                var resultBinding = ValueBindingExpression.CreateBinding<string>(
                    service.WithoutInitialization(),
                    h => null!,
                    result.Build(new OperatorPrecedence(OperatorPrecedence.Addition, false)),
                    this.GetDataContextType()
                );

                return new ValueOrBinding<string>(resultBinding);
            }
        }

        /// <summary>
        /// Calculates the corresponding attribute for the Id property.
        /// </summary>
        public ValueOrBinding<string>? CreateClientId(ValueOrBinding<string?> prefix = default, ValueOrBinding<string?> suffix = default)
        {
            var fragments = GetClientIdFragments();
            return fragments is null ? null : JoinValuesOrBindings(fragments, prefix, suffix);
        }

        private List<object?> GetUniqueIdFragments()
        {
            var fragments = new List<object?>
            {
                Internal.UniqueIDProperty.GetValue(this)
            };
            foreach (var ancestor in GetAllAncestors())
            {
                if (IsNamingContainer(ancestor))
                {
                    fragments.Add(
                        Internal.ClientIDFragmentProperty.GetValue(ancestor) ?? Internal.UniqueIDProperty.GetValue(ancestor)
                    );
                }
            }
            fragments.Reverse();
            return fragments;
        }

        private List<object?>? GetClientIdFragments()
        {
            var rawId = IDProperty.GetValue(this);

            // can't generate ID from nothing
            if (rawId == null) return null;

            var fragments = new List<object?> { rawId };
            if (ClientIDMode == ClientIDMode.Static)
            {
                // just rewrite Static mode ID
                return fragments;
            }

            DotvvmControl? childContainer = null;
            var searchingForIdElement = false;
            foreach (var ancestor in GetAllAncestors())
            {
                if (ancestor is not DotvvmControl ancestorControl)
                {
                    throw new DotvvmControlException(this, "The client ID cannot be determined for a control which is not part of the control tree. An ancestor that doesn't inherit from DotvvmControl was found on the path to the root.");
                }

                if (IsNamingContainer(ancestorControl))
                {
                    if (searchingForIdElement)
                    {
                        fragments.Add(childContainer!.GetDotvvmUniqueId().UnwrapToObject());
                    }
                    searchingForIdElement = false;

                    if (Internal.ClientIDFragmentProperty.GetValue(ancestorControl) is {} clientIdExpression)
                    {
                        fragments.Add(clientIdExpression);
                    }
                    else if (ancestorControl.GetValueRaw(IDProperty) is var ancestorId && ancestorId is not null or "")
                    {
                        // add the ID fragment
                        fragments.Add(ancestorId);
                    }
                    else
                    {
                        searchingForIdElement = true;
                        childContainer = ancestorControl;
                    }
                }

                if (searchingForIdElement && ancestorControl.IsPropertySet(ClientIDProperty))
                {
                    fragments.Add(ancestorControl.GetValueRaw(ClientIDProperty));
                    searchingForIdElement = false;
                }

                if (ancestorControl.ClientIDMode == ClientIDMode.Static)
                {
                    break;
                }
            }
            if (searchingForIdElement)
            {
                fragments.Add(childContainer!.GetDotvvmUniqueId().UnwrapToObject());
            }
            fragments.Reverse();
            return fragments;
        }


        /// <summary>
        /// Verifies that the control contains only a plain text content and tries to extract it.
        /// </summary>
        protected bool TryGetTextContent(out string textContent)
        {
            textContent = string.Join(string.Empty, Children.OfType<RawLiteral>().Where(l => !l.IsWhitespace).Select(l => l.UnencodedText));
            return Children.All(c => c is RawLiteral);
        }

        public override IEnumerable<DotvvmBindableObject> GetLogicalChildren()
        {
            return Children;
        }

        protected internal override DotvvmBindableObject CloneControl()
        {
            var newControl = (DotvvmControl)base.CloneControl();
            newControl.Children = new DotvvmControlCollection(newControl);
            foreach (var child in Children)
                newControl.Children.Add((DotvvmControl)child.CloneControl());
            return newControl;
        }

        IEnumerable<DotvvmBindableObject> IDotvvmControl.GetAllAncestors(bool includingThis) => this.GetAllAncestors(includingThis);
    }
}
