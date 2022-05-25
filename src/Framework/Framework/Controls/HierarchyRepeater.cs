using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Framework.Compilation.Validation;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Repeats a template for each item in the hierarchical DataSource collection.
    /// </summary>
    [ControlMarkupOptions(AllowContent = false, DefaultContentProperty = nameof(ItemTemplate))]
    public class HierarchyRepeater : ItemsControl
    {
        private EmptyData? emptyDataContainer;
        private DotvvmControl? clientItemTemplate;
        private DotvvmControl? clientRootLevel;
        private string? clientItemTemplateId;

        public HierarchyRepeater() : base("div")
        {
            SetValue(Internal.IsNamingContainerProperty, true);
        }

        /// <summary>
        /// Gets or sets the binding which retrieves children of a data item.
        /// </summary>
        [CollectionElementDataContextChange(1)]
        [ControlPropertyBindingDataContextChange(nameof(DataSource))]
        [BindingCompilationRequirements(new[] { typeof(DataSourceAccessBinding) }, new[] { typeof(DataSourceLengthBinding) })]
        [MarkupOptions(Required = true)]
        public IStaticValueBinding<IEnumerable<object>>? ItemChildrenBinding
        {
            get => (IStaticValueBinding<IEnumerable<object>>?)GetValue(ItemChildrenBindingProperty);
            set => SetValue(ItemChildrenBindingProperty, value);
        }

        public static readonly DotvvmProperty ItemChildrenBindingProperty
            = DotvvmProperty.Register<IStaticValueBinding<IEnumerable<object>>?, HierarchyRepeater>(t => t.ItemChildrenBinding);

        /// <summary>
        /// Gets or sets the template for each HierarchyRepeater item.
        /// </summary>
        [CollectionElementDataContextChange(1)]
        [ControlPropertyBindingDataContextChange(nameof(DataSource))]
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement, Required = true)]
        public ITemplate ItemTemplate
        {
            get => (ITemplate)GetValue(ItemTemplateProperty)!;
            set => SetValue(ItemTemplateProperty, value);
        }

        public static readonly DotvvmProperty ItemTemplateProperty =
            DotvvmProperty.Register<ITemplate, HierarchyRepeater>(t => t.ItemTemplate);

        /// <summary>
        /// Gets or sets the template which will be displayed when the data source is empty.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        public ITemplate? EmptyDataTemplate
        {
            get => (ITemplate?)GetValue(EmptyDataTemplateProperty);
            set => SetValue(EmptyDataTemplateProperty, value);
        }

        public static readonly DotvvmProperty EmptyDataTemplateProperty =
            DotvvmProperty.Register<ITemplate?, HierarchyRepeater>(t => t.EmptyDataTemplate);

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
            DotvvmProperty.Register<bool, HierarchyRepeater>(t => t.RenderWrapperTag, true);

        /// <summary>
        /// Gets or sets the name of the tag that wraps the <see cref="HierarchyRepeaterLevel"/>.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string WrapperTagName
        {
            get { return (string)GetValue(WrapperTagNameProperty)!; }
            set { SetValue(WrapperTagNameProperty, value); }
        }

        public static readonly DotvvmProperty WrapperTagNameProperty =
            DotvvmProperty.Register<string, HierarchyRepeater>(t => t.WrapperTagName, "div");

        public WrapperCapability LevelWrapperCapability
        {
            get => (WrapperCapability)this.GetValue(LevelWrapperCapabilityProperty)!;
            set => this.SetValue(LevelWrapperCapabilityProperty, value);
        }
        public static readonly DotvvmCapabilityProperty LevelWrapperCapabilityProperty =
            DotvvmCapabilityProperty.RegisterCapability<WrapperCapability, HierarchyRepeater>("Level");

        public WrapperCapability ItemWrapperCapability
        {
            get => (WrapperCapability)this.GetValue(ItemWrapperCapabilityProperty)!;
            set => this.SetValue(ItemWrapperCapabilityProperty, value);
        }
        public static readonly DotvvmCapabilityProperty ItemWrapperCapabilityProperty =
            DotvvmCapabilityProperty.RegisterCapability<WrapperCapability, HierarchyRepeater>("Item");

        protected internal override void OnLoad(IDotvvmRequestContext context)
        {
            if (context.RequestType == DotvvmRequestType.Command)
            {
                SetChildren(context, renderClientTemplate: false);
            }
            base.OnLoad(context);
        }

        protected internal override void OnPreRender(IDotvvmRequestContext context)
        {
            SetChildren(context, renderClientTemplate: !RenderOnServer);
            base.OnPreRender(context);
        }

        protected override void RenderBeginTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            TagName = RenderWrapperTag ? WrapperTagName : null;
            base.RenderBeginTag(writer, context);
        }

        protected override void RenderEndTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            base.RenderEndTag(writer, context);
            emptyDataContainer?.Render(writer, context);
        }

        protected override void RenderContents(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            if (clientRootLevel is null)
            {
                foreach (var child in Children.Except(new[] { emptyDataContainer!, clientItemTemplate! }))
                {
                    child.Render(writer, context);
                }
            }
            else
            {
                clientRootLevel.Render(writer, context);
            }
        }

        private void SetChildren(IDotvvmRequestContext context, bool renderClientTemplate)
        {
            Children.Clear();
            emptyDataContainer = null;
            clientItemTemplate = null;

            if (GetIEnumerableFromDataSource() is {} enumerable)
            {
                CreateServerLevel(this.Children,
                    context,
                    enumerable,
                    parentPath: ImmutableArray<int>.Empty,
                    foreachExpression: this.TryGetKnockoutForeachExpression()
                );
            }

            if (renderClientTemplate && GetDataSourceBinding() is IValueBinding)
            {
                // whenever possible, we use the dotvvm deterministic ids, but if we are in a client-side template,
                // we'd get a binding... so we just generate a random Guid, not ideal but it will work.
                var uniqueIdValueOrBinding = GetDotvvmUniqueId();
                var uniqueId = uniqueIdValueOrBinding.HasValue ? uniqueIdValueOrBinding.ValueOrDefault : Guid.NewGuid().ToString();
                clientItemTemplateId = $"{uniqueId}-item";
                clientItemTemplate = AddClientItemTemplate(Children, context);
                context.ResourceManager.AddTemplateResource(context, clientItemTemplate, clientItemTemplateId);
                clientRootLevel = LevelWrapperCapability.GetWrapper();

                Children.Add(clientRootLevel);
                clientRootLevel.Children.Add(new HierarchyRepeaterLevel {
                    IsRoot = true,
                    ForeachExpression = this.TryGetKnockoutForeachExpression().NotNull(),
                    ItemTemplateId = clientItemTemplateId,
                });
            }

            if (EmptyDataTemplate is not null)
            {
                emptyDataContainer ??= AddEmptyItem(Children, context);
            }
        }

        private DotvvmControl CreateServerLevel(
            IList<DotvvmControl> c,
            IDotvvmRequestContext context,
            IEnumerable items,
            ImmutableArray<int> parentPath,
            string? foreachExpression)
        {
            var dataContextLevelWrapper = new HierarchyRepeaterLevel {
                ForeachExpression = foreachExpression
            };
            if (parentPath.Length > 0)
            {
                dataContextLevelWrapper.SetValue(Internal.IsNamingContainerProperty, true);
                dataContextLevelWrapper.SetValue(Internal.UniqueIDProperty, parentPath[parentPath.Length - 1].ToString());
            }
            c.Add(dataContextLevelWrapper);
            var levelWrapper = LevelWrapperCapability.GetWrapper();
            dataContextLevelWrapper.Children.Add(levelWrapper);

            var index = 0;
            foreach (var item in items)
            {
                CreateServerItem(levelWrapper.Children, context, item, parentPath, index, foreachExpression is null);
                index++;
            }
            return dataContextLevelWrapper;
        }

        private DotvvmControl CreateServerItem(
            IList<DotvvmControl> c,
            IDotvvmRequestContext context,
            object item,
            ImmutableArray<int> parentPath,
            int index,
            bool serverOnly)
        {
            var itemWrapper = ItemWrapperCapability.GetWrapper();
            c.Add(itemWrapper);
            var dataItem = new DataItemContainer { DataItemIndex = index, RenderItemBinding = !serverOnly };
            dataItem.SetValue(Internal.UniqueIDProperty, index.ToString() + "L"); // must be different from sibling HierarchyRepeaterLevel
            itemWrapper.Children.Add(dataItem);
            dataItem.SetDataContextTypeFromDataSource(GetDataSourceBinding());
            // NB: the placeholder is needed because during data context resolution DataItemContainers are looked up
            //     only among parents
            var placeholder = new PlaceHolder { DataContext = item };
            var parentSegment = parentPath.IsDefaultOrEmpty
                ? string.Empty
                : $"/[{string.Join("]/[", parentPath)}]";
            placeholder.SetValue(
                Internal.PathFragmentProperty,
                $"{GetPathFragmentExpression()}{parentSegment}/[{index}]");
            placeholder.SetValue(Internal.UniqueIDProperty, "item");
            placeholder.SetDataContextTypeFromDataSource(GetDataSourceBinding()); // DataContext type has to be duplicated on the placeholder, because BindingHelper.FindDataContextTarget (in v4.1)
            dataItem.Children.Add(placeholder);
            ItemTemplate.BuildContent(context, placeholder);

            // if the item has children then recurse down
            var itemChildren = GetItemChildren(item);
            if (itemChildren.Any())
            {
                var foreachExpression = serverOnly ? null : ((IValueBinding)ItemChildrenBinding
                    .NotNull("ItemChildrenBinding property is required")
                    .GetProperty<DataSourceAccessBinding>()
                    .Binding)
                    .GetParametrizedKnockoutExpression(dataItem)
                    .ToString(p =>
                        p == JavascriptTranslator.KnockoutViewModelParameter ? CodeParameterAssignment.FromIdentifier($"ko.unwrap($foreachCollectionSymbol)[{index}]()") :
                        p == JavascriptTranslator.KnockoutViewModelObservableParameter ? CodeParameterAssignment.FromIdentifier($"ko.unwrap($foreachCollectionSymbol)[{index}]") :
                        p is JavascriptTranslator.ViewModelSymbolicParameter vm ?
                            vm.WithIndex(vm.ParentIndex - 1).DefaultAssignment :
                        default
                    );

                CreateServerLevel(itemWrapper.Children, context, itemChildren, parentPath.Add(index), foreachExpression);
            }
            return itemWrapper;
        }

        private static ParametrizedCode indexPathExpression =
            JavascriptTranslator.KnockoutContextParameter
                .ToExpression()
                .Member("$indexPath")
                .Member("map").Invoke(new JsIdentifierExpression("ko.unwrap"))
                .Member("join").Invoke(new JsLiteral("_"))
                .Binary(BinaryOperatorType.Plus, new JsLiteral("L"))
                .FormatParametrizedScript();

        private DotvvmControl AddClientItemTemplate(IList<DotvvmControl> c, IDotvvmRequestContext context)
        {
            var bindingService = context.Services.GetRequiredService<BindingCompilationService>();

            var dataItem = new DataItemContainer();
            dataItem.DataContext = null;
            dataItem.SetValue(Internal.PathFragmentProperty, $"{GetPathFragmentExpression()}/[$indexPath]");
            dataItem.SetDataContextTypeFromDataSource(GetDataSourceBinding());
            var clientIdFragmentProperty = ValueBindingExpression.CreateBinding<string?>(
                bindingService.WithoutInitialization(),
                h => null,
                indexPathExpression,
                dataItem.GetDataContextType());
            dataItem.SetValue(Internal.ClientIDFragmentProperty, clientIdFragmentProperty);
            c.Add(dataItem);

            var itemWrapper = ItemWrapperCapability.GetWrapper();
            dataItem.Children.Add(itemWrapper);

            var dataContextChangeItemWrapper = new HierarchyRepeaterItem();
            dataContextChangeItemWrapper.SetValue(Internal.UniqueIDProperty, "item");
            itemWrapper.Children.Add(dataContextChangeItemWrapper);
            ItemTemplate.BuildContent(context, dataContextChangeItemWrapper);

            var foreachExpression = ((IValueBinding)ItemChildrenBinding!
                .GetProperty<DataSourceAccessBinding>()
                .Binding)
                .GetParametrizedKnockoutExpression(dataItem)
                .ToString(p =>
                    p == JavascriptTranslator.KnockoutViewModelParameter ?
                        CodeParameterAssignment.FromIdentifier("$item()") :
                    p == JavascriptTranslator.KnockoutViewModelObservableParameter ?
                        CodeParameterAssignment.FromIdentifier("$item") :
                    p is JavascriptTranslator.ViewModelSymbolicParameter vm ?
                        vm.WithIndex(vm.ParentIndex - 1).DefaultAssignment :
                    default
                );

            var levelWrapper = LevelWrapperCapability.GetWrapper();

            itemWrapper.Children.Add(levelWrapper);

            var dataContextLevelWrapper = new HierarchyRepeaterLevel {
                ItemTemplateId = clientItemTemplateId,
                ForeachExpression = foreachExpression
            };
            levelWrapper.Children.Add(dataContextLevelWrapper);
            dataContextLevelWrapper.SetProperty(IncludeInPageProperty,
                (IValueBinding)ItemChildrenBinding!.GetProperty<DataSourceLengthBinding>().Binding);

            return dataItem;
        }

        private EmptyData AddEmptyItem(IList<DotvvmControl> c, IDotvvmRequestContext context)
        {
            var emptyDataContainer = new EmptyData();
            emptyDataContainer.SetValue(EmptyData.RenderWrapperTagProperty, GetValueRaw(RenderWrapperTagProperty));
            emptyDataContainer.SetValue(EmptyData.WrapperTagNameProperty, GetValueRaw(WrapperTagNameProperty));
            emptyDataContainer.SetValue(EmptyData.VisibleProperty, GetValueRaw(VisibleProperty));
            emptyDataContainer.SetBinding(DataSourceProperty, GetDataSourceBinding());
            c.Add(emptyDataContainer);
            EmptyDataTemplate!.BuildContent(context, emptyDataContainer);
            return emptyDataContainer;
        }

        private IEnumerable<object> GetItemChildren(object item)
        {
            var tempContainer = new DataItemContainer { Parent = this, DataContext = item };
            tempContainer.SetDataContextTypeFromDataSource(GetDataSourceBinding());
            return ItemChildrenBinding!.Evaluate(tempContainer) ?? Enumerable.Empty<object>();
        }

        [ControlUsageValidator]
        public static IEnumerable<ControlUsageError> ValidateUsage(ResolvedControl control)
        {
            if (!control.TryGetProperty(DataSourceProperty, out var dataSource))
            {
                yield return new("DataSource is required on HierarchyRepeater");
                yield break;
            }
            if (dataSource is not ResolvedPropertyBinding { Binding: var dataSourceBinding })
            {
                yield return new("HierarchyRepeater.DataSource must be a binding");
                yield break;
            }
            if (!control.TryGetProperty(ItemChildrenBindingProperty, out var itemChildren) ||
                itemChildren is not ResolvedPropertyBinding { Binding: var itemChildrenBinding })
            {
                yield break;
            }

            if (dataSourceBinding.ParserOptions.BindingType != itemChildrenBinding.ParserOptions.BindingType)
            {
                yield return new(
                    "HierarchyRepeater.DataSource and HierarchyRepeater.ItemChildrenBinding must have the same binding type, use `value` or `resource` binding for both properties.",
                    dataSourceBinding.DothtmlNode,
                    itemChildrenBinding.DothtmlNode
                );
            }
        }


        /// <summary>
        /// An internal control for a level of the <see cref="HierarchyRepeater"/> that renders
        /// the appropriate foreach binding.
        /// </summary>
        private class HierarchyRepeaterLevel : DotvvmControl
        {
            public string? ItemTemplateId { get; set; }

            public string? ForeachExpression { get; set; }

            public bool IsRoot { get; set; } = false;

            protected override void RenderControl(IHtmlWriter writer, IDotvvmRequestContext context)
            {
                if (ForeachExpression is null)
                {
                    base.RenderControl(writer, context);
                    return;
                }

                if (!RenderOnServer)
                {
                    var koGroup = new KnockoutBindingGroup {
                        { "foreach", ForeachExpression }
                    };
                    koGroup.AddValue("name", ItemTemplateId.NotNull());
                    koGroup.AddValue("hierarchyRole", IsRoot ? "Root" : "Child");
                    writer.WriteKnockoutDataBindComment("template", koGroup.ToString());
                }
                else
                {
                    writer.WriteKnockoutDataBindComment("dotvvm-SSR-foreach", new KnockoutBindingGroup {
                        { "data", ForeachExpression}
                    }.ToString());
                }

                base.RenderControl(writer, context);

                writer.WriteKnockoutDataBindEndComment();
            }
        }

        /// <summary>
        /// An internal control for an item of the <see cref="HierarchyRepeater"/>. Always renders a simple wit
        /// binding.
        /// </summary>
        private class HierarchyRepeaterItem : DotvvmControl
        {
            protected override void RenderControl(IHtmlWriter writer, IDotvvmRequestContext context)
            {
                writer.WriteKnockoutDataBindComment("with", "$item");
                base.RenderControl(writer, context);
                writer.WriteKnockoutDataBindEndComment();
            }
        }
    }
}
