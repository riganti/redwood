using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace DotVVM.Framework.Compilation
{
    public class HtmlAttributeValueMerger: AttributeValueMergerBase
    {
        public static Expression MergeExpressions(GroupedDotvvmProperty property, Expression a, Expression b)
        {
            var attributeName = property.GroupMemberName;
            var separator = HtmlWriter.GetSeparatorForAttribute(attributeName);
            var method = typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) });
            return Expression.Add(Expression.Add(a, Expression.Constant(separator), method), b, method);
            // return Expression.Call(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string) }), a, Expression.Constant(separator), b);
        }

        public static string? MergeValues(DotvvmPropertyId property, string? a, string? b)
        {
            if (!property.IsPropertyGroup) throw new ArgumentException("HtmlAttributeValueMerger only supports property group", nameof(property));
            var attributeName = property.GroupMemberName;
            // for perf reasons only do this compile time - we'll deduplicate the attribute if it's a CSS class
            if (attributeName == "class" && a is string && b is string)
            {
                var classesA = a.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var classesB = b.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Where(c => !classesA.Contains(c));
                b = string.Join(" ", classesB);
            }

            return HtmlWriter.JoinAttributeValues(attributeName, a, b);
        }


        public override object? MergePlainValues(DotvvmPropertyId prop, object? a, object? b)
        {
            if (!prop.IsPropertyGroup) throw new ArgumentException("HtmlAttributeValueMerger only supports property group", nameof(prop));
            var attributeName = prop.GroupMemberName;
            if (a is null) return b;
            if (b is null) return a;

            if (a is string aString && b is string bString)
                return HtmlWriter.JoinAttributeValues(attributeName, aString, bString);

            // append to list. Order does not matter in html attributes
            if (a is HtmlGenericControl.AttributeList alist)
                return new HtmlGenericControl.AttributeList(b, alist);
            else if (b is HtmlGenericControl.AttributeList blist)
                return new HtmlGenericControl.AttributeList(a, blist);

            return new HtmlGenericControl.AttributeList(a, new HtmlGenericControl.AttributeList(b, null));
        }
    }
}
