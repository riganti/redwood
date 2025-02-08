using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DotVVM.AutoUI.Annotations;
using DotVVM.AutoUI.Configuration;
using DotVVM.AutoUI.Metadata;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Utils;
using DotVVM.Framework.ViewModel.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.AutoUI
{
    public class AutoUIContext
    {
        public DataContextStack DataContextStack { get; }
        public IServiceProvider Services { get; }

        public IViewModelValidationMetadataProvider ValidationMetadataProvider { get; }

        public IPropertyDisplayMetadataProvider PropertyDisplayMetadataProvider { get; }
        public AutoUIConfiguration AutoUiConfiguration { get; }

        public Type EntityType => DataContextStack.DataContextType;

        public string? ViewName { get; set; }

        public string? GroupName { get; set; }

        public BindingCompilationService BindingService { get; }

        public AutoUIContext(DataContextStack dataContextStack, IServiceProvider services)
        {
            DataContextStack = dataContextStack;
            Services = services;

            ValidationMetadataProvider = services.GetRequiredService<IViewModelValidationMetadataProvider>();
            PropertyDisplayMetadataProvider = services.GetRequiredService<IPropertyDisplayMetadataProvider>();
            AutoUiConfiguration = services.GetRequiredService<AutoUIConfiguration>();
            BindingService = services.GetRequiredService<BindingCompilationService>();
        }

        public ValidationAttribute[] GetPropertyValidators(PropertyInfo property)
        {
            return ValidationMetadataProvider.GetAttributesForProperty(property).ToArray();
        }
        public ValidationAttribute[] GetPropertyValidators(PropertyDisplayMetadata property)
        {
            if (property.PropertyInfo is null)
                return Array.Empty<ValidationAttribute>();
            return GetPropertyValidators(property.PropertyInfo);
        }

        public IStaticValueBinding CreateValueBinding(PropertyDisplayMetadata property)
        {
            if (property.ValueBinding is not null)
                return property.ValueBinding;

            if (property.PropertyInfo is null)
                throw new ArgumentException("property.PropertyInfo is null => cannot create value binding for this property");

            var s = this.BindingService;
            var serverOnly = this.DataContextStack.ServerSideOnly;
            return s.Cache.CreateCachedBinding("AutoUI-Value", new object?[] { BoxingUtils.Box(serverOnly), property.PropertyInfo, DataContextStack }, () => {
                var _this = Expression.Parameter(DataContextStack.DataContextType, "_this").AddParameterAnnotation(new BindingParameterAnnotation(DataContextStack));
                var expr = Expression.Property(_this, property.PropertyInfo);
                var bindingType = serverOnly ? typeof(ResourceBindingExpression<>) : typeof(ValueBindingExpression<>);
                return (IStaticValueBinding)BindingService.CreateBinding(bindingType, new object[] {
                    DataContextStack,
                    new ParsedExpressionBindingProperty(expr)
                });
            });
        }

        [Obsolete("This method probably doesn't do what you'd expect - It does not work correctly for collection elements, because it will miss _index parameter. Please use the `BindingHelper.GetDataContextType(Repeater.ItemTemplateProperty, repeater, context.DataContextStack)` method for the specific property where the binding is being placed (Repeater is just example). If the type is changed using DotvvmBindableObject.DataContext property, use the DataContextStack.Create method.")]
        public DataContextStack CreateChildDataContextStack(DataContextStack dataContextStack, params Type[] nestedDataContextTypes)
        {
            foreach (var type in nestedDataContextTypes)
            {
                dataContextStack = DataContextStack.Create(type, dataContextStack, dataContextStack.NamespaceImports, dataContextStack.ExtensionParameters, dataContextStack.BindingPropertyResolvers);
            }
            return dataContextStack;
        }

        [Obsolete("This method probably doesn't do what you'd expect - It does not work correctly for collection elements, because it will miss _index parameter. Please use the `BindingHelper.GetDataContextType(Repeater.ItemTemplateProperty, repeater, context.DataContextStack)` method for the specific property where the binding is being placed (Repeater is just example). If the type is changed using DotvvmBindableObject.DataContext property, use the DataContextStack.Create method.")]
        public DataContextStack CreateChildDataContextStack(params Type[] nestedDataContextTypes) =>
            CreateChildDataContextStack(DataContextStack, nestedDataContextTypes);

        public IViewContext CreateViewContext()
        {
            return new ViewContext()
            {
                ViewName = ViewName,
                GroupName = GroupName
            };
        }
    }
}
