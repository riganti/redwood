using DotVVM.Framework.Compilation.ControlTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DotVVM.Framework.Binding
{
    public sealed class GroupedDotvvmProperty : DotvvmProperty, IGroupedPropertyDescriptor
    {
        public DotvvmPropertyGroup PropertyGroup { get; }

        public string GroupMemberName { get; }

        IPropertyGroupDescriptor IGroupedPropertyDescriptor.PropertyGroup => PropertyGroup;

        private GroupedDotvvmProperty(string memberName, ushort memberId, DotvvmPropertyGroup group)
            : base(DotvvmPropertyId.CreatePropertyGroupId(group.Id, memberId), group.Name + ":" + memberName, group.DeclaringType)
        {
            this.GroupMemberName = memberName;
            this.PropertyGroup = group;
        }


        public static GroupedDotvvmProperty Create(DotvvmPropertyGroup group, string name, ushort id)
        {
            var prop = new GroupedDotvvmProperty(name, id, group) {
                PropertyType = group.PropertyType,
                DefaultValue = group.DefaultValue,
                IsValueInherited = false,
                ObsoleteAttribute = group.ObsoleteAttribute,
                OwningCapability = group.OwningCapability,
                UsedInCapabilities = group.UsedInCapabilities
            };

            DotvvmProperty.InitializeProperty(prop, group.AttributeProvider); // TODO: maybe inline and specialize to just copy the group attributes
            return prop;
        }
    }
}
