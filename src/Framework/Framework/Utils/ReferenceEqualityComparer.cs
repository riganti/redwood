using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotVVM.Framework.Utils
{
    // TODO next version: Replace with System.Collections.Generic.ReferenceEqualityComparer
    internal class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new ReferenceEqualityComparer<T>();


        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }
}
