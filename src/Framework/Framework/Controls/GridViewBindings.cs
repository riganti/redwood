using System;
using System.Collections.Concurrent;
using DotVVM.Framework.Binding.Expressions;

namespace DotVVM.Framework.Controls
{
    /// <summary> Contains pre-created command and value bindings for the <see cref="GridView" /> components. An instance can be obtained from <see cref="GridViewDataSetBindingProvider" /> </summary>
    public class GridViewBindings
    {
        private readonly ConcurrentDictionary<string, IStaticValueBinding<bool>> isSortColumnAscending = new(concurrencyLevel: 1, capacity: 16);
        private readonly ConcurrentDictionary<string, IStaticValueBinding<bool>> isSortColumnDescending = new(concurrencyLevel: 1, capacity: 16);

        public ICommandBinding? SetSortExpression { get; init; }

        internal IStaticValueBinding<Func<string, bool>>? IsColumnSortedAscending { get; init; }
        internal IStaticValueBinding<Func<string, bool>>? IsColumnSortedDescending { get; init; }

        public IStaticValueBinding<bool>? GetIsColumnSortedAscendingBinding(string sortExpression)
        {
            if (IsColumnSortedAscending == null)
            {
                return null;
            }
            return isSortColumnAscending.GetOrAdd(sortExpression, _ => IsColumnSortedAscending.Select(a => a(sortExpression)));
        }

        public IStaticValueBinding<bool>? GetIsColumnSortedDescendingBinding(string sortExpression)
        {
            if (IsColumnSortedDescending == null)
            {
                return null;
            }
            return isSortColumnDescending.GetOrAdd(sortExpression, _ => IsColumnSortedDescending.Select(a => a(sortExpression)));
        }
    }
}
