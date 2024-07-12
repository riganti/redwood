using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Renders the pagination control which can be integrated with the GridViewDataSet object to provide the paging capabilities.
    /// </summary>
    [ControlMarkupOptions(AllowContent = false)]
    public class DataPager : HtmlGenericControl
    {
        private readonly GridViewDataSetBindingProvider gridViewDataSetBindingProvider;
        private readonly BindingCompilationService bindingCompilationService;

        private DataPagerBindings? pagerBindings;


        public DataPager(GridViewDataSetBindingProvider gridViewDataSetBindingProvider, BindingCompilationService bindingCompilationService)
            : base("ul", false)
        {
            this.gridViewDataSetBindingProvider = gridViewDataSetBindingProvider;
            this.bindingCompilationService = bindingCompilationService;
        }

        /// <summary>
        /// Gets or sets the GridViewDataSet object in the viewmodel.
        /// </summary>
        [MarkupOptions(AllowHardCodedValue = false, Required = true)]
        public IPageableGridViewDataSet<IPagingOptions>? DataSet
        {
            get { return (IPageableGridViewDataSet<IPagingOptions>?)GetValue(DataSetProperty); }
            set { SetValue(DataSetProperty, value); }
        }
        public static readonly DotvvmProperty DataSetProperty =
            DotvvmProperty.Register<IPageableGridViewDataSet<IPagingOptions>?, DataPager>(c => c.DataSet);

        /// <summary>
        /// Gets or sets the template of the button which moves the user to the first page.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        public ITemplate? FirstPageTemplate
        {
            get { return (ITemplate?)GetValue(FirstPageTemplateProperty); }
            set { SetValue(FirstPageTemplateProperty, value); }
        }
        public static readonly DotvvmProperty FirstPageTemplateProperty =
            DotvvmProperty.Register<ITemplate?, DataPager>(c => c.FirstPageTemplate, null);

        /// <summary>
        /// Gets or sets the template of the button which moves the user to the last page.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        public ITemplate? LastPageTemplate
        {
            get { return (ITemplate?)GetValue(LastPageTemplateProperty); }
            set { SetValue(LastPageTemplateProperty, value); }
        }
        public static readonly DotvvmProperty LastPageTemplateProperty =
            DotvvmProperty.Register<ITemplate?, DataPager>(c => c.LastPageTemplate, null);

        /// <summary>
        /// Gets or sets the template of the button which moves the user to the previous page.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        public ITemplate? PreviousPageTemplate
        {
            get { return (ITemplate?)GetValue(PreviousPageTemplateProperty); }
            set { SetValue(PreviousPageTemplateProperty, value); }
        }
        public static readonly DotvvmProperty PreviousPageTemplateProperty =
            DotvvmProperty.Register<ITemplate?, DataPager>(c => c.PreviousPageTemplate, null);

        /// <summary>
        /// Gets or sets the template of the button which moves the user to the next page.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        public ITemplate? NextPageTemplate
        {
            get { return (ITemplate?)GetValue(NextPageTemplateProperty); }
            set { SetValue(NextPageTemplateProperty, value); }
        }
        public static readonly DotvvmProperty NextPageTemplateProperty =
            DotvvmProperty.Register<ITemplate?, DataPager>(c => c.NextPageTemplate, null);

        /// <summary>
        /// Gets or sets the template of the button which moves the user to the numbered page.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        [ConstantDataContextChange(typeof(int[]), order: 0)]
        [CollectionElementDataContextChange(order: 1)]
        public ITemplate? PageNumberTemplate
        {
            get { return (ITemplate?)GetValue(PageNumberTemplateProperty); }
            set { SetValue(PageNumberTemplateProperty, value); }
        }
        public static readonly DotvvmProperty PageNumberTemplateProperty =
            DotvvmProperty.Register<ITemplate?, DataPager>(c => c.PageNumberTemplate, null);

        /// <summary>
        /// Gets or sets whether a hyperlink should be rendered for the current page number. If set to false, only a plain text is rendered.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public bool RenderLinkForCurrentPage
        {
            get { return (bool)GetValue(RenderLinkForCurrentPageProperty)!; }
            set { SetValue(RenderLinkForCurrentPageProperty, value); }
        }
        public static readonly DotvvmProperty RenderLinkForCurrentPageProperty =
            DotvvmProperty.Register<bool, DataPager>(c => c.RenderLinkForCurrentPage);


        /// <summary>
        /// Gets or sets whether the pager should hide automatically when there is only one page of results. Must not be set to true when using the Visible property.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public bool HideWhenOnlyOnePage
        {
            get { return (bool)GetValue(HideWhenOnlyOnePageProperty)!; }
            set { SetValue(HideWhenOnlyOnePageProperty, value); }
        }
        public static readonly DotvvmProperty HideWhenOnlyOnePageProperty
            = DotvvmProperty.Register<bool, DataPager>(c => c.HideWhenOnlyOnePage, true);

        public bool Enabled
        {
            get { return (bool)GetValue(EnabledProperty)!; }
            set { SetValue(EnabledProperty, value); }
        }
        public static readonly DotvvmProperty EnabledProperty =
            DotvvmPropertyWithFallback.Register<bool, DataPager>(nameof(Enabled), FormControls.EnabledProperty);

        /// <summary>
        /// Gets or sets styles for the list item element (&lt;li&gt;) rendered by the component.
        /// </summary>
        public HtmlCapability ListItemHtmlCapability
        {
            get => (HtmlCapability)ListItemHtmlCapabilityProperty.GetValue(this);
            set => ListItemHtmlCapabilityProperty.SetValue(this, value);
        }
        public static readonly DotvvmCapabilityProperty ListItemHtmlCapabilityProperty = DotvvmCapabilityProperty.RegisterCapability<HtmlCapability, DataPager>("ListItem");

        /// <summary>
        /// Gets or sets styles for the link buttons rendered by the component.
        /// </summary>
        public HtmlCapability LinkHtmlCapability
        {
            get => (HtmlCapability)LinkHtmlCapabilityProperty.GetValue(this);
            set => LinkHtmlCapabilityProperty.SetValue(this, value);
        }
        public static readonly DotvvmCapabilityProperty LinkHtmlCapabilityProperty = DotvvmCapabilityProperty.RegisterCapability<HtmlCapability, DataPager>("Link");


        /// <summary>
        /// Gets or sets the (static) command that will be triggered when the DataPager needs to load data (when navigating to different page).
        /// The command accepts one argument of type <see cref="GridViewDataSetOptions{TFilteringOptions, TSortingOptions, TPagingOptions}" /> and should return a new <see cref="GridViewDataSet{T}" /> or <see cref="GridViewDataSetResult{TItem, TFilteringOptions, TSortingOptions, TPagingOptions}" />.
        /// </summary>
        public ICommandBinding? LoadData
        {
            get => (ICommandBinding?)GetValue(LoadDataProperty);
            set => SetValue(LoadDataProperty, value);
        }
        public static readonly DotvvmProperty LoadDataProperty =
            DotvvmProperty.Register<ICommandBinding?, DataPager>(nameof(LoadData));

        /// <summary>
        /// Gets or sets the CSS class to be applied to the currently active page number.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string ActiveItemCssClass
        {
            get { return (string)GetValue(ActiveItemCssClassProperty); }
            set { SetValue(ActiveItemCssClassProperty, value); }
        }
        public static readonly DotvvmProperty ActiveItemCssClassProperty
            = DotvvmProperty.Register<string, DataPager>(c => c.ActiveItemCssClass, "active");

        /// <summary>
        /// Gets or sets the CSS class that to be applied to the disabled items.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string DisabledItemCssClass
        {
            get { return (string)GetValue(DisabledItemCssClassProperty); }
            set { SetValue(DisabledItemCssClassProperty, value); }
        }
        public static readonly DotvvmProperty DisabledItemCssClassProperty
            = DotvvmProperty.Register<string, DataPager>(c => c.DisabledItemCssClass, "disabled");

        protected HtmlGenericControl? ContentWrapper { get; set; }
        protected HtmlGenericControl? GoToFirstPageButton { get; set; }
        protected HtmlGenericControl? GoToPreviousPageButton { get; set; }
        protected Repeater? NumberButtonsRepeater { get; set; }
        protected HtmlGenericControl? GoToNextPageButton { get; set; }
        protected HtmlGenericControl? GoToLastPageButton { get; set; }

        protected internal override void OnLoad(IDotvvmRequestContext context)
        {
            DataBind(context);
            base.OnLoad(context);
        }

        protected internal override void OnPreRender(IDotvvmRequestContext context)
        {
            DataBind(context);
            base.OnPreRender(context);
        }

        protected virtual void DataBind(Hosting.IDotvvmRequestContext context)
        {
            Children.Clear();

            var dataSetBinding = GetValueBinding(DataSetProperty)!;
            var dataSetType = dataSetBinding.ResultType;

            var commandType = LoadData is {} ? GridViewDataSetCommandType.LoadDataDelegate : GridViewDataSetCommandType.Default;

            pagerBindings = gridViewDataSetBindingProvider.GetDataPagerBindings(this.GetDataContextType().NotNull(), dataSetBinding, commandType);

            var globalEnabled = GetValueOrBinding<bool>(EnabledProperty)!;

            ContentWrapper = CreateWrapperList();
            Children.Add(ContentWrapper);

            if (typeof(IPageableGridViewDataSet<IPagingFirstPageCapability>).IsAssignableFrom(dataSetType))
            {
                var disabled = new ValueOrBinding<bool>(pagerBindings.IsFirstPage.NotNull());
                GoToFirstPageButton = CreateNavigationButton("««", FirstPageTemplate, globalEnabled.And(disabled.Negate()), pagerBindings.GoToFirstPage!, context);
                GoToFirstPageButton.CssClasses.Add(DisabledItemCssClass, disabled);
                AddItemCssClass(GoToFirstPageButton, context);
                ContentWrapper.Children.Add(GoToFirstPageButton);
            }

            if (typeof(IPageableGridViewDataSet<IPagingPreviousPageCapability>).IsAssignableFrom(dataSetType))
            {
                var disabled = new ValueOrBinding<bool>(pagerBindings.IsFirstPage.NotNull());
                GoToPreviousPageButton = CreateNavigationButton("«", PreviousPageTemplate, globalEnabled.And(disabled.Negate()), pagerBindings.GoToPreviousPage!, context);
                GoToPreviousPageButton.CssClasses.Add(DisabledItemCssClass, disabled);
                AddItemCssClass(GoToPreviousPageButton, context);
                ContentWrapper.Children.Add(GoToPreviousPageButton);
            }

            if (pagerBindings.PageNumbers is {})
            {
                // number fields
                var liTemplate = CreatePageNumberButton(globalEnabled, PageNumberTemplate, pagerBindings, context);
                AddItemCssClass(liTemplate, context);

                NumberButtonsRepeater = new Repeater() {
                    DataSource = pagerBindings.PageNumbers,
                    RenderWrapperTag = false,
                    RenderAsNamedTemplate = false,
                    ItemTemplate = new CloneTemplate(liTemplate)
                };
                ContentWrapper.Children.Add(NumberButtonsRepeater);
            }

            if (typeof(IPageableGridViewDataSet<IPagingNextPageCapability>).IsAssignableFrom(dataSetType))
            {
                var disabled = new ValueOrBinding<bool>(pagerBindings.IsLastPage.NotNull());
                GoToNextPageButton = CreateNavigationButton("»", NextPageTemplate, globalEnabled.And(disabled.Negate()), pagerBindings.GoToNextPage!, context);
                GoToNextPageButton.CssClasses.Add(DisabledItemCssClass, disabled);
                AddItemCssClass(GoToNextPageButton, context);
                ContentWrapper.Children.Add(GoToNextPageButton);
            }

            if (typeof(IPageableGridViewDataSet<IPagingLastPageCapability>).IsAssignableFrom(dataSetType))
            {
                var disabled = new ValueOrBinding<bool>(pagerBindings.IsLastPage.NotNull());
                GoToLastPageButton = CreateNavigationButton("»»", LastPageTemplate, globalEnabled.And(disabled.Negate()), pagerBindings.GoToLastPage!, context);
                GoToLastPageButton.CssClasses.Add(DisabledItemCssClass, disabled);
                AddItemCssClass(GoToLastPageButton, context);
                ContentWrapper.Children.Add(GoToLastPageButton);
            }
        }

        protected virtual HtmlGenericControl CreateWrapperList()
        {
            var list = new HtmlGenericControl("ul");

            // If Visible property was set to something, it would be overwritten by this
            if (HideWhenOnlyOnePage && pagerBindings?.HasMoreThanOnePage is {} hasMoreThanOnePage)
            {
                list.SetProperty(
                    HtmlGenericControl.VisibleProperty,
                    new ValueOrBinding<bool>(hasMoreThanOnePage).And(GetValueOrBinding<bool>(VisibleProperty))
                );
            }
            else
            {
                list.SetProperty(HtmlGenericControl.VisibleProperty, GetValueOrBinding<bool>(VisibleProperty));
            }


            return list;
        }

        protected override void AddVisibleAttributeOrBinding(in RenderState r, IHtmlWriter writer) { } // handled by the wrapper list

        protected virtual HtmlGenericControl CreatePageNumberButton(ValueOrBinding<bool> globalEnabled, ITemplate? userDefinedContentTemplate, DataPagerBindings pagerBindings, IDotvvmRequestContext context)
        {
            var liTemplate = new HtmlGenericControl("li", ListItemHtmlCapability);
            liTemplate.CssClasses.Add(ActiveItemCssClass, new ValueOrBinding<bool>(pagerBindings.NotNull().IsActivePage.NotNull()));
            
            var link = new LinkButton().SetCapability(LinkHtmlCapability);
            
            link.SetBinding(ButtonBase.ClickProperty, pagerBindings.NotNull().GoToPage.NotNull());
            SetPageNumberButtonContent(context, link, pagerBindings, userDefinedContentTemplate);
            if (!RenderLinkForCurrentPage) link.SetBinding(IncludeInPageProperty, pagerBindings.IsActivePage.NotNull().Negate());
            if (!true.Equals(globalEnabled)) link.SetValue(ButtonBase.EnabledProperty, globalEnabled);
            liTemplate.Children.Add(link);

            if (!RenderLinkForCurrentPage)
            {
                var notLink = new HtmlGenericControl("span").SetCapability(LinkHtmlCapability);
                SetPageNumberSpanContent(context, notLink, pagerBindings, userDefinedContentTemplate);
                notLink.SetBinding(IncludeInPageProperty, pagerBindings.IsActivePage);
                liTemplate.Children.Add(notLink);
            }
            return liTemplate;
        }

        protected virtual HtmlGenericControl CreateNavigationButton(string defaultText, ITemplate? userDefinedContentTemplate, object enabledValue, ICommandBinding clickCommandBindingExpression,IDotvvmRequestContext context)
        {
            var li = new HtmlGenericControl("li", ListItemHtmlCapability);

            var link = new LinkButton().SetCapability(LinkHtmlCapability);

            SetNavigationButtonContent(context, link, defaultText, userDefinedContentTemplate);
            link.SetBinding(ButtonBase.ClickProperty, clickCommandBindingExpression);
            if (!true.Equals(enabledValue)) link.SetValue(ButtonBase.EnabledProperty, enabledValue);
            li.Children.Add(link);
            return li;
        }

        protected virtual void SetNavigationButtonContent(IDotvvmRequestContext context, LinkButton button, string text, ITemplate? contentTemplate)
        {
            if (contentTemplate != null)
            {
                contentTemplate.BuildContent(context, button);
            }
            else
            {
                button.Text = text;
            }
        }

        protected virtual void SetPageNumberSpanContent(IDotvvmRequestContext context, HtmlGenericControl span, DataPagerBindings pagerBindings, ITemplate? userDefinedContentTemplate)
        {
            if (userDefinedContentTemplate != null)
            {
                userDefinedContentTemplate.BuildContent(context, span);
            }
            else
            {
                span.SetBinding(Literal.TextProperty, pagerBindings.PageNumberText.NotNull());
            }
        }

        protected virtual void SetPageNumberButtonContent(IDotvvmRequestContext context, LinkButton link, DataPagerBindings pagerBindings, ITemplate? userDefinedContentTemplate)
        {
            if (userDefinedContentTemplate != null)
            {
                userDefinedContentTemplate.BuildContent(context, link);
            }
            else
            {
                link.SetBinding(ButtonBase.TextProperty, pagerBindings.PageNumberText.NotNull());
            }
        }

        protected virtual void AddItemCssClass(HtmlGenericControl item, IDotvvmRequestContext context)
        {
        }

        protected override void AddAttributesToRender(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            if (RenderOnServer)
            {
                throw new DotvvmControlException(this, "The DataPager control cannot be rendered in the RenderSettings.Mode='Server'.");
            }

            if (this.LoadData is {} loadData)
            {
                var helperBinding = new KnockoutBindingGroup();
                helperBinding.Add("dataSet", GetDataSetBinding().GetKnockoutBindingExpression(this, unwrapped: true));
                var loadDataExpression = KnockoutHelper.GenerateClientPostbackLambda("LoadData", loadData, this, new PostbackScriptOptions(elementAccessor: "$element", koContext: CodeParameterAssignment.FromIdentifier("$context")));
                helperBinding.Add("loadDataSet", loadDataExpression);
                writer.AddKnockoutDataBind("dotvvm-gridviewdataset", helperBinding.ToString());
            }

            if (GetValueBinding(EnabledProperty) is IValueBinding enabledBinding)
            {
                var disabledBinding = enabledBinding.GetProperty<NegatedBindingExpression>().Binding.CastTo<IValueBinding>();
                AddKnockoutDisabledCssDataBind(writer, context, disabledBinding.GetKnockoutBindingExpression(this));
            }
            else if (!Enabled)
            {
                writer.AddAttribute("class", DisabledItemCssClass, true);
            }

            base.AddAttributesToRender(writer, context);
        }

        protected virtual void AddKnockoutDisabledCssDataBind(IHtmlWriter writer, IDotvvmRequestContext context, string expression)
        {
            writer.AddKnockoutDataBind("css", $"{{ '{DisabledItemCssClass}': {expression} }}");
        }

        protected override void RenderContents(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            base.RenderContents(writer, context);
        }

        protected override void RenderBeginTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            // don't, delegated to the ContentWrapper html element
        }
        protected override void RenderEndTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
        }


        private IValueBinding GetDataSetBinding()
            => GetValueBinding(DataSetProperty) ?? throw new DotvvmControlException(this, "The DataSet property of the dot:DataPager control must be set!");
    }
}
