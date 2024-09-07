using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotVVM.Framework.Controls
{
    public static class GridViewDataSetExtensions
    {

        public static void LoadFromQueryable<T>(this IGridViewDataSet<T> dataSet, IQueryable<T> queryable)
        {
            if (dataSet.FilteringOptions is not IApplyToQueryable filteringOptions)
            {
                throw new ArgumentException($"The FilteringOptions of {dataSet.GetType()} must implement IApplyToQueryable!");
            }
            if (dataSet.SortingOptions is not IApplyToQueryable sortingOptions)
            {
                throw new ArgumentException($"The SortingOptions of {dataSet.GetType()} must implement IApplyToQueryable!");
            }
            if (dataSet.PagingOptions is not IApplyToQueryable pagingOptions)
            {
                throw new ArgumentException($"The PagingOptions of {dataSet.GetType()} must implement IApplyToQueryable!");
            }

            var filtered = filteringOptions.ApplyToQueryable(queryable);
            var sorted = sortingOptions.ApplyToQueryable(filtered);
            var paged = pagingOptions.ApplyToQueryable(sorted);
            dataSet.Items = paged.ToList();

            if (pagingOptions is IPagingOptionsLoadingPostProcessor pagingOptionsLoadingPostProcessor)
            {
                pagingOptionsLoadingPostProcessor.ProcessLoadedItems(filtered, dataSet.Items);
            }

            dataSet.IsRefreshRequired = false;
        }

        public static async Task LoadFromQueryableAsync<T>(this IGridViewDataSet<T> dataSet, IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            if (dataSet.FilteringOptions is not IApplyToQueryable filteringOptions)
            {
                throw new ArgumentException($"The FilteringOptions of {dataSet.GetType()} must implement IApplyToQueryable!");
            }
            if (dataSet.SortingOptions is not IApplyToQueryable sortingOptions)
            {
                throw new ArgumentException($"The SortingOptions of {dataSet.GetType()} must implement IApplyToQueryable!");
            }
            if (dataSet.PagingOptions is not IApplyToQueryable pagingOptions)
            {
                throw new ArgumentException($"The PagingOptions of {dataSet.GetType()} must implement IApplyToQueryable!");
            }

            var filtered = filteringOptions.ApplyToQueryable(queryable);
            var sorted = sortingOptions.ApplyToQueryable(filtered);
            var paged = pagingOptions.ApplyToQueryable(sorted);
            if (paged is not IAsyncEnumerable<T> asyncPaged)
            {
                throw new ArgumentException($"The specified IQueryable ({queryable.GetType().FullName}), does not support async enumeration. Please use the LoadFromQueryable method.", nameof(queryable));
            }

            var result = new List<T>();
            await foreach (var item in asyncPaged.WithCancellation(cancellationToken))
            {
                result.Add(item);
            }
            dataSet.Items = result;

            if (pagingOptions is IPagingOptionsLoadingPostProcessor pagingOptionsLoadingPostProcessor)
            {
                await pagingOptionsLoadingPostProcessor.ProcessLoadedItemsAsync(filtered, result, cancellationToken);
            }

            dataSet.IsRefreshRequired = false;
        }

        public static void GoToFirstPageAndRefresh(this IPageableGridViewDataSet<IPagingFirstPageCapability> dataSet)
        {
            dataSet.PagingOptions.GoToFirstPage();
            (dataSet as IRefreshableGridViewDataSet)?.RequestRefresh();
        }

        public static void GoToLastPageAndRefresh(this IPageableGridViewDataSet<IPagingLastPageCapability> dataSet)
        {
            dataSet.PagingOptions.GoToLastPage();
            (dataSet as IRefreshableGridViewDataSet)?.RequestRefresh();
        }

        public static void GoToPreviousPageAndRefresh(this IPageableGridViewDataSet<IPagingPreviousPageCapability> dataSet)
        {
            dataSet.PagingOptions.GoToPreviousPage();
            (dataSet as IRefreshableGridViewDataSet)?.RequestRefresh();
        }

        public static void GoToNextPageAndRefresh(this IPageableGridViewDataSet<IPagingNextPageCapability> dataSet)
        {
            dataSet.PagingOptions.GoToNextPage();
            (dataSet as IRefreshableGridViewDataSet)?.RequestRefresh();
        }

        public static void GoToPageAndRefresh(this IPageableGridViewDataSet<IPagingPageIndexCapability> dataSet, int pageIndex)
        {
            dataSet.PagingOptions.GoToPage(pageIndex);
            (dataSet as IRefreshableGridViewDataSet)?.RequestRefresh();
        }


        public static async Task LoadAsync<T, TFilteringOptions, TSortingOptions, TPagingOptions, TRowInsertOptions, TRowEditOptions>(
            this GenericGridViewDataSet<T, TFilteringOptions, TSortingOptions, TPagingOptions, TRowInsertOptions, TRowEditOptions> dataSet,
            Func<GridViewDataSetOptions<TFilteringOptions, TSortingOptions, TPagingOptions>, Task<GridViewDataSetResult<T, TFilteringOptions, TSortingOptions, TPagingOptions>>> loadMethod)
                where TFilteringOptions : IFilteringOptions
                where TSortingOptions : ISortingOptions
                where TPagingOptions : IPagingOptions
                where TRowInsertOptions : IRowInsertOptions
                where TRowEditOptions : IRowEditOptions
        {
            var options = dataSet.GetOptions();
            var result = await loadMethod(options);
            dataSet.ApplyResult(result);
        }

    }
}
