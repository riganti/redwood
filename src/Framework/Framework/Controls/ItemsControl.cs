using DotVVM.Framework.Binding;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation.Javascript;
using System.Reflection;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Styles;
using DotVVM.Framework.Compilation.ControlTree;
using System.Linq.Expressions;
using DotVVM.Framework.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FastExpressionCompiler;
using DotVVM.Framework.Compilation;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// A common base for all controls that operate on collection.
    /// </summary>
    public abstract class ItemsControl : HtmlGenericControl
    {
        /// <summary>
        /// Gets or sets the source collection or a GridViewDataSet that contains data in the control.
        /// </summary>
        [MarkupOptions(AllowHardCodedValue = false)]
        [BindingCompilationRequirements(
            required: new[] { typeof(DataSourceAccessBinding) },
            optional: new[] { typeof(DataSourceLengthBinding), typeof(CollectionElementDataContextBindingProperty) })]
        public object? DataSource
        {
            get { return GetValue(DataSourceProperty); }
            set { SetValue(DataSourceProperty, value); }
        }

        public static readonly DotvvmProperty DataSourceProperty =
            DotvvmProperty.Register<object?, ItemsControl>(t => t.DataSource, null);

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemsControl"/> class.
        /// </summary>
        public ItemsControl()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemsControl"/> class.
        /// </summary>
        public ItemsControl(string tagName) : base(tagName, false)
        {
        }

        /// <summary>
        /// Gets the data source binding.
        /// </summary>
        protected IStaticValueBinding GetDataSourceBinding()
        {
            var binding = GetBinding(DataSourceProperty);
            if (binding is null)
            {
                throw new DotvvmControlException(this, $"The DataSource property of the '{GetType().Name}' control must be set!");
            }
            if (binding is not IStaticValueBinding resourceBinding)
                throw new BindingHelper.BindingNotSupportedException(binding) { RelatedControl = this };
            return resourceBinding;
        }

        protected IValueBinding GetItemBinding()
        {
            return GetForeachDataBindExpression().GetProperty<DataSourceCurrentElementBinding>().Binding as IValueBinding ??
                throw new DotvvmControlException(this, $"The Item property of the '{GetType().Name}' control must be set to a value binding!");
        }

        public IEnumerable? GetIEnumerableFromDataSource() =>
            (IEnumerable?)GetForeachDataBindExpression().Evaluate(this);

        protected IStaticValueBinding GetForeachDataBindExpression() =>
            (IStaticValueBinding)GetDataSourceBinding().GetProperty<DataSourceAccessBinding>().Binding;

        protected string? TryGetKnockoutForeachExpression(bool unwrapped = false) =>
            (GetForeachDataBindExpression() as IValueBinding)?.GetKnockoutBindingExpression(this, unwrapped);

        protected string GetPathFragmentExpression()
        {
            var binding = GetDataSourceBinding();
            var stringified =
                binding.GetProperty<OriginalStringBindingProperty>(ErrorHandlingMode.ReturnNull)?.Code.Trim() ??
                binding.GetProperty<KnockoutExpressionBindingProperty>(ErrorHandlingMode.ReturnNull)?.Code.FormatKnockoutScript(this, binding) ??
                binding.GetProperty<ParsedExpressionBindingProperty>(ErrorHandlingMode.ReturnNull)?.Expression.ToCSharpString();

            if (stringified is null)
                throw new DotvvmControlException(this, $"Can't create path fragment from binding {binding}, it does not have OriginalString, ParsedExpression, nor KnockoutExpression property.");
        
            return stringified;
        }

        /// <summary> Returns data context which is expected in the ItemTemplate </summary>
        protected DataContextStack GetChildDataContext() =>
            GetDataSourceBinding().GetProperty<CollectionElementDataContextBindingProperty>().DataContext;

        [ApplyControlStyle]
        public static void OnCompilation(ResolvedControl control, BindingCompilationService bindingService)
        {
            // ComboBox does not have to have the DataSource property and then they don't use the CurrentIndexBindingProperty
            if (!control.Properties.TryGetValue(DataSourceProperty, out var dataSourceProperty)) return;
            if (!(dataSourceProperty is ResolvedPropertyBinding dataSourceBinding)) return;

            var dataContext = dataSourceBinding.Binding.Binding.GetProperty<CollectionElementDataContextBindingProperty>().DataContext;
            var bindingType = dataContext.ServerSideOnly ? BindingParserOptions.Resource : BindingParserOptions.Value;

            control.SetProperty(new ResolvedPropertyBinding(Internal.CurrentIndexBindingProperty,
                new ResolvedBinding(bindingService, bindingType, dataContext,
                parsedExpression: CreateIndexBindingExpression(dataContext))));
        }

        private static ParameterExpression CreateIndexBindingExpression(DataContextStack dataContext) =>
            Expression.Parameter(typeof(int), "_index")
                .AddParameterAnnotation(new BindingParameterAnnotation(dataContext, new CurrentCollectionIndexExtensionParameter()));

        protected IBinding GetIndexBinding(IDotvvmRequestContext context)
        {
            var result = GetValueRaw(Internal.CurrentIndexBindingProperty) as IBinding;
            if (result is {})
            {
                return result;
            }
            else
            {
                // slower path: create the _index binding at runtime
                var bindingService = context.Services.GetRequiredService<BindingCompilationService>();
                var dataContext = GetChildDataContext();
                return bindingService.Cache.CreateCachedBinding("_index", new object[] { dataContext }, () =>
                    new ValueBindingExpression<int>(bindingService, new object?[] {
                        dataContext,
                        new ParsedExpressionBindingProperty(CreateIndexBindingExpression(dataContext))
                    }));
            }

        }
    }
}
