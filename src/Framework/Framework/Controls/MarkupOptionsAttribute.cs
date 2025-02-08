using DotVVM.Framework.Compilation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Declares instructions for control builder.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class MarkupOptionsAttribute : Attribute
    {

        /// <summary>
        /// Gets or sets whether client-side data-bindings can be used on this property (`value` and `controlProperty`).
        /// </summary>
        public bool AllowBinding
        {
            get => _allowBinding ?? true;
            set => _allowBinding = value;
        }

        internal bool? _allowBinding = null;

        /// <summary>
        /// Gets or sets whether the server-side value in markup can be used on this property. Allows both value hard-coded in markup and `resource` binding, which is always evaluated server-side.
        /// </summary>
        public bool AllowHardCodedValue
        {
            get => _allowHardCodedValue ?? true;
            set => _allowHardCodedValue = value;
        }
        internal bool? _allowHardCodedValue = null;

        /// <summary>
        /// Gets or sets whether the `resource` binding can be used on this property. By default, the <see cref="AllowHardCodedValue" /> is copied.
        /// </summary>
        public bool AllowResourceBinding
        {
            get => _allowResourceBinding ?? AllowHardCodedValue;
            set => _allowResourceBinding = value;
        }
        internal bool? _allowResourceBinding = null;

        /// <summary>
        /// Gets or sets the name in markup. Null means that the name of the property should be used.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Determines if multiple property assignments can be merged into one value. For example `&lt;div class='x' class='y' ...` is equivalent to `&lt;div class='x y'` because of the merging.
        /// </summary>
        public bool AllowValueMerging
        {
            get => _allowValueMerging ?? false;
            set => _allowValueMerging = value;
        }
        internal bool? _allowValueMerging = null;

        /// <summary>
        /// Type with non parametric constructor which implements IAttributeValueMerger interface
        /// </summary>
        public Type AttributeValueMerger
        {
            get => _attributeValueMerger ?? typeof(DefaultAttributeValueMerger);
            set => _attributeValueMerger = value;
        }
        internal Type? _attributeValueMerger = null;

        /// <summary>
        /// Gets or sets the mapping mode - whether the property is used as an attribute or inner element (or both are allowed).
        /// </summary>
        public MappingMode MappingMode
        {
            get => _mappingMode ?? MappingMode.Attribute;
            set => _mappingMode = value;
        }
        internal MappingMode? _mappingMode = null;

        /// <summary>
        /// Determines whether attributes without value are allowed.
        /// </summary>
        public bool AllowAttributeWithoutValue
        {
            get => _allowAttributeWithoutValue ?? false;
            set => _allowAttributeWithoutValue = value;
        }
        internal bool? _allowAttributeWithoutValue = null;

        /// <summary> Whether the property must always be specified on this control. It is also allowed to set the property using server-side styles. </summary>
        public bool Required
        {
            get => _required ?? false;
            set => _required = value;
        }
        internal bool? _required = null;
    }
}
