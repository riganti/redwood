﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Controls.DynamicData.Annotations;

namespace DotVVM.Framework.Controls.DynamicData.Metadata
{
    public class PropertyDisplayMetadata
    {
        
        public PropertyInfo PropertyInfo { get; }

        public string? DisplayName { get; set; }

        public string? GroupName { get; set; }

        public int? Order { get; set; }

        public string? FormatString { get; set; }

        public string? NullDisplayText { get; set; }

        public bool AutoGenerateField { get; set; }

        public DataType? DataType { get; set; }

        public IVisibilityFilter[] VisibilityFilters { get; set; } = Array.Empty<IVisibilityFilter>();

        public StyleAttribute? Styles { get; set; }

        public bool IsEditAllowed { get; set; }

        public SelectorAttribute? SelectorConfiguration { get; set; }

        public bool IsDefaultLabelAllowed { get; set; }

        public PropertyDisplayMetadata(PropertyInfo propertyInfo)
        {
            PropertyInfo = propertyInfo;
        }

    }
}
