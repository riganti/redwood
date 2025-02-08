using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using DotVVM.AutoUI.Annotations;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using Humanizer;

namespace DotVVM.AutoUI.Metadata
{
    public record PropertyDisplayMetadata
    {
        public PropertyInfo? PropertyInfo { get; }

        public string Name { get; init; }
        public Type Type { get; init; }

        public IStaticValueBinding? ValueBinding { get; init; }

        public LocalizableString? DisplayName { get; set; }
        public LocalizableString? Placeholder { get; set; }
        public LocalizableString? Description { get; set; }

        public string? GroupName { get; set; }

        public int? Order { get; set; }

        public string? FormatString { get; set; }

        public string? NullDisplayText { get; set; }

        public bool AutoGenerateField { get; set; }

        public List<VisibleAttribute> VisibleAttributes { get; set; } = new();

        public DataType? DataType { get; set; }

        public bool IsEditable { get; set; }

        public List<EnabledAttribute> EnabledAttributes { get; set; } = new();

        public StyleAttribute? Styles { get; set; }

        public SelectionAttribute? SelectionConfiguration { get; set; }


        public bool IsDefaultLabelAllowed { get; set; }

        public string[] UIHints { get; set; } = { };


        public PropertyDisplayMetadata(PropertyInfo propertyInfo)
        {
            PropertyInfo = propertyInfo;
            this.Name = propertyInfo.Name;
            this.Type = propertyInfo.PropertyType;
        }
        public PropertyDisplayMetadata(string name, IValueBinding binding)
        {
            this.Name = name;
            this.Type = binding.ResultType;
            this.PropertyInfo = binding.GetProperty<ReferencedViewModelPropertiesBindingProperty>()?.MainProperty;
        }


        /// <summary> Returns DisplayName or a default name derived from the PropertyInfo if it is not set. </summary>
        public LocalizableString GetDisplayName() =>
            DisplayName ?? LocalizableString.Constant(Name.Humanize());
    }
}
