using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Styles;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.ResourceManagement;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Repeats a template for each item in the DataSource collection.
    /// </summary>
    [ControlMarkupOptions(AllowContent = false, DefaultContentProperty = nameof(ItemTemplate))]
    public class Repeater : ItemsControl
    {
        private EmptyData? emptyDataContainer;
        private DotvvmControl? clientSeparator;
        private DotvvmControl? clientSideTemplate;

        public Repeater(bool allowImplicitLifecycleRequirements = true)
        {
            if (allowImplicitLifecycleRequirements)
            {
                SetValue(Internal.IsNamingContainerProperty, true);
                if (GetType() == typeof(Repeater))
                    LifecycleRequirements &= ~(ControlLifecycleRequirements.InvokeMissingInit | ControlLifecycleRequirements.InvokeMissingLoad);
            }
        }

        [ApplyControlStyle]
        public static void OnCompilation(ResolvedControl control)
        {
            control.SetProperty(new ResolvedPropertyValue(Internal.IsNamingContainerProperty, true), replace: false, error: out _);
        }

        /// <summary>
        /// Gets or sets the template which will be displayed when the DataSource is empty.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        public ITemplate? EmptyDataTemplate
        {
            get { return (ITemplate?)GetValue(EmptyDataTemplateProperty); }
            set { SetValue(EmptyDataTemplateProperty, value); }
        }

        public static readonly DotvvmProperty EmptyDataTemplateProperty =
            DotvvmProperty.Register<ITemplate?, Repeater>(t => t.EmptyDataTemplate, null);

        /// <summary>
        /// Gets or sets the template for each Repeater item.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement, Required = true)]
        [ControlPropertyBindingDataContextChange(nameof(DataSource))]
        [CollectionElementDataContextChange(1)]
        public ITemplate ItemTemplate
        {
            get { return (ITemplate)GetValue(ItemTemplateProperty)!; }
            set { SetValue(ItemTemplateProperty, value); }
        }

        public static readonly DotvvmProperty ItemTemplateProperty =
            DotvvmProperty.Register<ITemplate, Repeater>(t => t.ItemTemplate);

        /// <summary>
        /// Gets or sets whether the control should render a wrapper element.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public bool RenderWrapperTag
        {
            get { return (bool)GetValue(RenderWrapperTagProperty)!; }
            set { SetValue(RenderWrapperTagProperty, value); }
        }

        public static readonly DotvvmProperty RenderWrapperTagProperty =
            DotvvmProperty.Register<bool, Repeater>(t => t.RenderWrapperTag, true);

        /// <summary>
        /// Gets or sets the template containing the elements that separate items.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        public ITemplate? SeparatorTemplate
        {
            get { return (ITemplate?)GetValue(SeparatorTemplateProperty); }
            set { SetValue(SeparatorTemplateProperty, value); }
        }

        public static readonly DotvvmProperty SeparatorTemplateProperty =
            DotvvmProperty.Register<ITemplate?, Repeater>(t => t.SeparatorTemplate, null);

        /// <summary>
        /// Gets or sets the name of the tag that wraps the Repeater.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string WrapperTagName
        {
            get { return (string)GetValue(WrapperTagNameProperty)!; }
            set { SetValue(WrapperTagNameProperty, value); }
        }

        public static readonly DotvvmProperty WrapperTagNameProperty =
            DotvvmProperty.Register<string, Repeater>(t => t.WrapperTagName, "div");

        /// <summary>
        /// Gets or sets if the repeater should use inline template (the default, traditional way of doing things) or if it should use Knockout's named template (with the template in &lt;script> tag).
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public bool RenderAsNamedTemplate
        {
            get { return (bool)GetValue(RenderAsNamedTemplateProperty)!; }
            set { SetValue(RenderAsNamedTemplateProperty, value); }
        }
        public static readonly DotvvmProperty RenderAsNamedTemplateProperty =
            DotvvmProperty.Register<bool, Repeater>(nameof(RenderAsNamedTemplate), defaultValue: true);

        protected override bool RendersHtmlTag => RenderWrapperTag;

        /// <summary>
        /// Occurs after the viewmodel is applied to the page and before the commands are executed.
        /// </summary>
        protected internal override void OnLoad(IDotvvmRequestContext context)
        {
            if (context.RequestType == DotvvmRequestType.Command)
            {
                SetChildren(context, renderClientTemplate: false, memoizeReferences: true);
            }

            base.OnLoad(context);
        }

        /// <summary>
        /// Occurs after the page commands are executed.
        /// </summary>
        protected internal override void OnPreRender(IDotvvmRequestContext context)
        {
            SetChildren(context, renderClientTemplate: !RenderOnServer, memoizeReferences: false);
            childrenCache.Clear(); // not going to need the unreferenced children anymore
            base.OnPreRender(context);
        }

        protected override void RenderBeginTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            TagName = WrapperTagName;

            if (GetValueBinding(DataSourceProperty) is {})
            {
                var (bindingName, bindingValue) = RenderOnServer ?
                                                ("dotvvm-SSR-foreach", GetServerSideForeachBindingGroup()) :
                                                GetForeachKnockoutBindingGroup(context);
                if (RenderWrapperTag)
                {
                    writer.AddKnockoutDataBind(bindingName, bindingValue);
                }
                else
                {
                    writer.WriteKnockoutDataBindComment(bindingName, bindingValue.ToString());
                }
            }

            if (RenderWrapperTag)
            {
                base.RenderBeginTag(writer, context);
            }
        }

        private KnockoutBindingGroup GetServerSideForeachBindingGroup() =>
            new KnockoutBindingGroup {
                { "data", TryGetKnockoutForeachExpression().NotNull() }
            };

        private (string bindingName, KnockoutBindingGroup bindingValue) GetForeachKnockoutBindingGroup(IDotvvmRequestContext context)
        {
            var useTemplate = this.RenderAsNamedTemplate;
            var value = new KnockoutBindingGroup();


            var javascriptDataSourceExpression = TryGetKnockoutForeachExpression().NotNull();
            value.Add(
                useTemplate ? "foreach" : "data",
                javascriptDataSourceExpression);

            if (useTemplate)
            {
                var itemTemplateId = context.ResourceManager.AddTemplateResource(context, clientSideTemplate!);

                value.Add("name", KnockoutHelper.MakeStringLiteral(itemTemplateId, htmlSafe: false));
            }

            if (clientSeparator != null)
            {
                // separator has to be always rendered as a named template
                var separatorTemplateId = context.ResourceManager.AddTemplateResource(context, clientSeparator);
                value.Add("separatorTemplate", KnockoutHelper.MakeStringLiteral(separatorTemplateId, htmlSafe: false));
            }

            return (
                useTemplate ? "template" : "foreach",
                value
            );
        }

        /// <summary>
        /// Renders the contents inside the control begin and end tags.
        /// </summary>
        protected override void RenderContents(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            if (clientSideTemplate == null)
            {
                Debug.Assert(clientSeparator == null);
                foreach (var child in Children.Except(new[] { emptyDataContainer! }))
                {
                    child.Render(writer, context);
                }
            }
            else if (!this.RenderAsNamedTemplate)
               clientSideTemplate.Render(writer, context);
        }

        protected override void RenderEndTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            if (RenderWrapperTag)
            {
                base.RenderEndTag(writer, context);
            }
            else if (GetValueBinding(DataSourceProperty) is {})
            {
                writer.WriteKnockoutDataBindEndComment();
            }

            emptyDataContainer?.Render(writer, context);
        }

        private DotvvmControl CreateEmptyItem(IDotvvmRequestContext context,  IStaticValueBinding dataSourceBinding)
        {
            if (emptyDataContainer == null)
            {
                emptyDataContainer = new EmptyData();
                emptyDataContainer.SetValue(EmptyData.RenderWrapperTagProperty, GetValueRaw(RenderWrapperTagProperty));
                emptyDataContainer.SetValue(EmptyData.WrapperTagNameProperty, GetValueRaw(WrapperTagNameProperty));
                emptyDataContainer.SetValue(EmptyData.VisibleProperty, GetValueRaw(VisibleProperty));
                emptyDataContainer.SetBinding(DataSourceProperty, dataSourceBinding);
                Children.Add(emptyDataContainer);
                EmptyDataTemplate!.BuildContent(context, emptyDataContainer);
            }
            return emptyDataContainer;
        }

        private readonly Dictionary<object, DataItemContainer> childrenCache = new(ReferenceEqualityComparer<object>.Instance);
        private DotvvmControl AddItem(IList<DotvvmControl> c, IDotvvmRequestContext context, object? item = null, int? index = null, bool serverOnly = false, bool allowMemoizationRetrieve = false, bool allowMemoizationStore = false)
        {
            if (allowMemoizationRetrieve && item != null && childrenCache.TryGetValue(item, out var container2) && container2.Parent == null)
            {
                Debug.Assert(item == container2.GetValueRaw(DataContextProperty));
                c.Add(container2);
                SetUpServerItem(context, item, (int)index!, serverOnly, container2);
                return container2;
            }

            var container = new DataItemContainer();
            container.SetDataContextTypeFromDataSource(GetBinding(DataSourceProperty)!);
            c.Add(container);
            if (item == null && index == null)
            {
                Debug.Assert(!serverOnly);
                SetUpClientItem(context, container);
            }
            else
            {
                SetUpServerItem(context, item!, (int)index!, serverOnly, container);
            }

            ItemTemplate.BuildContent(context, container);

            // write it to the cache after the content is build. If it would be before that, exception could be suppressed
            if (allowMemoizationStore && item != null)
            {
#if DotNetCore
                childrenCache.TryAdd(item, container);
#else
                if (!childrenCache.ContainsKey(item))
                    childrenCache.Add(item, container);
#endif
            }

            return container;
        }

        private DotvvmControl AddSeparator(IList<DotvvmControl> c, IDotvvmRequestContext context)
        {
            var placeholder = new PlaceHolder();
            placeholder.SetDataContextType(this.GetDataContextType());
            c.Add(placeholder);

            SeparatorTemplate!.BuildContent(context, placeholder);
            return placeholder;
        }

        /// <summary>
        /// Performs the data-binding and builds the controls inside the <see cref="Repeater"/>.
        /// </summary>
        private void SetChildren(IDotvvmRequestContext context, bool renderClientTemplate, bool memoizeReferences)
        {
            Children.Clear();
            emptyDataContainer = null;
            clientSeparator = null;
            clientSideTemplate = null;

            var dataSource = GetIEnumerableFromDataSource();
            var dataSourceBinding = GetDataSourceBinding();
            var serverOnly = dataSourceBinding is not IValueBinding;

            if (dataSource != null)
            {
                var index = 0;
                var isCommand = context.RequestType == DotvvmRequestType.Command; // on GET request we are not initializing the Repeater twice
                foreach (var item in dataSource)
                {
                    if (SeparatorTemplate != null && index > 0)
                    {
                        AddSeparator(Children, context);
                    }
                    AddItem(Children, context, item, index, serverOnly,
                        allowMemoizationRetrieve: isCommand && !memoizeReferences,
                        allowMemoizationStore: memoizeReferences
                    );
                    index++;
                }
            }

            if (renderClientTemplate && !serverOnly)
            {
                if (SeparatorTemplate != null)
                {
                    clientSeparator = AddSeparator(Children, context);
                }

                clientSideTemplate = AddItem(Children, context);
            }

            if (EmptyDataTemplate != null)
            {
                CreateEmptyItem(context, dataSourceBinding);
            }
        }

        private void SetUpClientItem(IDotvvmRequestContext context, DataItemContainer container)
        {
            container.DataContext = null;
            container.SetValue(Internal.PathFragmentProperty, GetPathFragmentExpression() + "/[$index]");
            container.SetValue(Internal.ClientIDFragmentProperty, this.GetIndexBinding(context));
        }

        private void SetUpServerItem(IDotvvmRequestContext context, object item, int index, bool serverOnly, DataItemContainer container)
        {
            container.DataItemIndex = index;
            container.DataContext = item;
            container.RenderItemBinding = !serverOnly;
            container.SetValue(Internal.IsServerOnlyDataContextProperty, serverOnly);
            container.SetValue(Internal.PathFragmentProperty, GetPathFragmentExpression() + "/[" + index + "]");
            container.ID = index.ToString();
        }
    }
}
