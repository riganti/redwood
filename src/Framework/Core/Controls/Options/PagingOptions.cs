using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotVVM.Framework.ViewModel;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Represents settings for offset-based paging using <see cref="PageIndex" /> and <see cref="PageSize" />.
    /// </summary>
    public class PagingOptions : IPagingOptions, IPagingFirstPageCapability, IPagingLastPageCapability, IPagingPreviousPageCapability, IPagingNextPageCapability, IPagingPageIndexCapability, IPagingPageSizeCapability, IPagingTotalItemsCountCapability, IApplyToQueryable, IPagingOptionsLoadingPostProcessor
    {
        /// <summary>
        /// Gets or sets the object that provides a list of page indexes near the current page.
        /// </summary>
        [Bind(Direction.None)]
        [Obsolete("This interface was removed. If you want to provide your custom page numbers, inherit the PagingOptions class and override the GetNearPageIndexes method.", true)]
        public INearPageIndexesProvider NearPageIndexesProvider { get; set; } = null!;

        /// <summary>
        /// Gets whether the <see cref="PageIndex" /> represents the first page.
        /// </summary>
        public bool IsFirstPage => PageIndex == 0;

        /// <summary>
        /// Gets whether the <see cref="PageIndex" /> represents the last page.
        /// </summary>
        public bool IsLastPage => PageIndex == PagesCount - 1;

        /// <summary>
        /// Gets the total number of pages.
        /// </summary>
        public int PagesCount
        {
            get
            {
                if (TotalItemsCount == 0 || PageSize == 0)
                {
                    return 1;
                }
                return (int)Math.Ceiling((double)TotalItemsCount / PageSize);
            }
        }

        /// <summary>
        /// Gets or sets a zero-based index of the current page.
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of items on a page.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets the total number of items in the data store without respect to paging.
        /// </summary>
        public int TotalItemsCount { get; set; }

        /// <summary> Sets PageIndex to zero. </summary>
        public void GoToFirstPage() => PageIndex = 0;

        /// <summary> Sets PageIndex to the last page (PagesCount - 1). </summary>
        public void GoToLastPage() => PageIndex = PagesCount - 1;

        /// <summary> Increments the page counter, if the next page exists. </summary>
        public void GoToNextPage()
        {
            if (PageIndex < PagesCount - 1)
            {
                PageIndex++;
            }
        }
        /// <summary> Decrements the page counter, unless PageIndex is already zero. </summary>
        public void GoToPreviousPage()
        {
            if (PageIndex > 0)
            {
                PageIndex--;
            }
        }
        /// <summary> Sets page index to the <paramref name="pageIndex"/>. If the index overflows, PageIndex is set to the first/last page. </summary>
        public void GoToPage(int pageIndex)
        {
            if (PageIndex >= 0 && PageIndex < PagesCount)
            {
                PageIndex = pageIndex;
            }
        }


        /// <summary>
        /// Gets a list of page indexes near the current page. It can be used to build data pagers.
        /// </summary>
        public IList<int> NearPageIndexes => GetNearPageIndexes();

        /// <summary>
        /// Gets a list of page indexes near the current page. Override this method to provide your own strategy.
        /// </summary>
        public virtual IList<int> GetNearPageIndexes()
        {
            return GetDefaultNearPageIndexes(5);
        }

        /// <summary>
        /// Returns the near page indexes in the maximum specified distance from the current page index.
        /// </summary>
        protected virtual IList<int> GetDefaultNearPageIndexes(int distance)
        {
            var count = this.PagesCount;
            var index = Math.Max(0, Math.Min(count - 1, PageIndex)); // clamp index to be a valid page
            var firstIndex = Math.Max(index - distance, 0);
            var lastIndex = Math.Min(index + distance + 1, count);
            return Enumerable
                .Range(firstIndex, lastIndex - firstIndex)
                .ToList();
        }

        public virtual IQueryable<T> ApplyToQueryable<T>(IQueryable<T> queryable)
        {
            return PagingImplementation.ApplyPagingToQueryable(queryable, this);
        }

        public virtual void ProcessLoadedItems<T>(IQueryable<T> filteredQueryable, IList<T> items)
        {
            TotalItemsCount = filteredQueryable.Count();
        }
        public async Task ProcessLoadedItemsAsync<T>(IQueryable<T> filteredQueryable, IList<T> items, CancellationToken cancellationToken)
        {
            TotalItemsCount = await PagingImplementation.QueryableAsyncCount(filteredQueryable, cancellationToken);
        }

    }
}
