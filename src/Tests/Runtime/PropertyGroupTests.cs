using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotVVM.Framework.Tests.Runtime
{
    [TestClass]
    public class PropertyGroupTests
    {
        [TestMethod]
        public void PGroup_Enumerate()
        {
            var el = new HtmlGenericControl("div");
            el.Attributes.Add("a", "1");
            el.Attributes.Add("b", "2");
            el.Attributes.Add("c", "3");

            var expected = new[] { "1", "2", "3" };
            XAssert.Equal(expected, el.Attributes.Select(p => p.Value).OrderBy(a => a));
            XAssert.Equal(expected, el.Attributes.RawValues.Select(p => p.Value).OrderBy(a => a));
            XAssert.Equal(expected, el.Attributes.Values.OrderBy(a => a));
            XAssert.Equal(expected, el.Attributes.Properties.Select(p => el.GetValue(p)).OrderBy(a => a));
            XAssert.Equal(expected, el.Attributes.Keys.Select(k => el.Attributes[k]).OrderBy(a => a));
            XAssert.Equal(["a", "b", "c"], el.Attributes.Keys.OrderBy(a => a));
        }

        [TestMethod]
        public void PGroup_AddMergeValues()
        {
            var el = new HtmlGenericControl("div");
            el.Attributes.Add("a", "1");
            el.Attributes.Add("a", "2");

            XAssert.Equal("1;2", el.Attributes["a"]);

            el.Attributes.Add("class", "c1");
            el.Attributes.Add("class", "c2");

            XAssert.Equal("c1 c2", el.Attributes["class"]);

            el.Attributes.Add("data-bind", "a: 1");
            el.Attributes.Add("data-bind", "b: 2");

            XAssert.Equal("a: 1,b: 2", el.Attributes["data-bind"]);
        }
    }
}
