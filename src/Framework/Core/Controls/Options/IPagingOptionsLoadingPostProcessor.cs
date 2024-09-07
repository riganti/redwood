using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotVVM.Framework.Controls;

/// <summary> Provides an extension point to the <see cref="GridViewDataSetExtensions.LoadFromQueryable{T}(DotVVM.Framework.Controls.IGridViewDataSet{T}, IQueryable{T})" /> method, which is invoked after the items are loaded from database. </summary>
public interface IPagingOptionsLoadingPostProcessor
{
    void ProcessLoadedItems<T>(IQueryable<T> filteredQueryable, IList<T> items);
    Task ProcessLoadedItemsAsync<T>(IQueryable<T> filteredQueryable, IList<T> items, CancellationToken cancellationToken);
}
