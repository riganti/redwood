using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotVVM.Framework.Controls;
using DotVVM.Framework.ViewModel;

namespace DotVVM.Samples.BasicSamples.ViewModels.ControlSamples.GridView
{
    public class GridViewStaticCommandViewModel : DotvvmViewModelBase
    {
        public GridViewDataSet<CustomerData> StandardDataSet { get; set; } = new() {
            PagingOptions = new PagingOptions
            {
                PageSize = 10
            }
        };

        public NextTokenGridViewDataSet NextTokenDataSet { get; set; } = new();

        public NextTokenHistoryGridViewDataSet NextTokenHistoryDataSet { get; set; } = new();

        public MultiSortGridViewDataSet MultiSortDataSet { get; set; } = new();

        private static IQueryable<CustomerData> GetData()
        {
            return new[]
            {
                new CustomerData {CustomerId = 1, Name = "John Doe", BirthDate = DateTime.Parse("1976-04-01"), MessageReceived = false},
                new CustomerData {CustomerId = 2, Name = "John Deer", BirthDate = DateTime.Parse("1984-03-02"), MessageReceived = false},
                new CustomerData {CustomerId = 3, Name = "Johnny Walker", BirthDate = DateTime.Parse("1934-01-03"), MessageReceived = false},
                new CustomerData {CustomerId = 4, Name = "Jim Hacker", BirthDate = DateTime.Parse("1912-11-04"), MessageReceived = false},
                new CustomerData {CustomerId = 5, Name = "Joe E. Brown", BirthDate = DateTime.Parse("1947-09-05"), MessageReceived = true},
                new CustomerData {CustomerId = 6, Name = "Jack Daniels", BirthDate = DateTime.Parse("1934-01-03"), MessageReceived = false},
                new CustomerData {CustomerId = 7, Name = "James Bond", BirthDate = DateTime.Parse("1965-05-07"), MessageReceived = true},
                new CustomerData {CustomerId = 8, Name = "John Smith", BirthDate = DateTime.Parse("1974-03-08"), MessageReceived = false},
                new CustomerData {CustomerId = 9, Name = "Jack & Jones", BirthDate = DateTime.Parse("1934-01-03"), MessageReceived = true},
                new CustomerData {CustomerId = 10, Name = "Jim Bill", BirthDate = DateTime.Parse("1934-01-03"), MessageReceived = true},
                new CustomerData {CustomerId = 11, Name = "James Joyce", BirthDate = DateTime.Parse("1982-11-28"), MessageReceived = true},
                new CustomerData {CustomerId = 12, Name = "Joudy Jane", BirthDate = DateTime.Parse("1934-01-03"), MessageReceived = true}
            }.AsQueryable();
        }

        public override Task PreRender()
        {
            // fill dataset
            if (!Context.IsPostBack)
            {
                StandardDataSet.LoadFromQueryable(GetData());
                NextTokenDataSet.LoadFromQueryable(GetData());
                NextTokenHistoryDataSet.LoadFromQueryable(GetData());
                MultiSortDataSet.LoadFromQueryable(GetData());
            }
            return base.PreRender();
        }

        [AllowStaticCommand]
        public static async Task<GridViewDataSet<CustomerData>> LoadStandard(GridViewDataSetOptions options)
        {
            var dataSet = new GridViewDataSet<CustomerData>();
            dataSet.ApplyOptions(options);
            dataSet.LoadFromQueryable(GetData());
            return dataSet;
        }

        [AllowStaticCommand]
        public static async Task<NextTokenGridViewDataSet> LoadToken(GridViewDataSetOptions<NoFilteringOptions, SortingOptions, CustomerDataNextTokenPagingOptions> options)
        {
            var dataSet = new NextTokenGridViewDataSet();
            dataSet.ApplyOptions(options);
            dataSet.LoadFromQueryable(GetData());
            return dataSet;
        }

        [AllowStaticCommand]
        public static async Task<NextTokenHistoryGridViewDataSet> LoadTokenHistory(GridViewDataSetOptions<NoFilteringOptions, SortingOptions, CustomerDataNextTokenHistoryPagingOptions> options)
        {
            var dataSet = new NextTokenHistoryGridViewDataSet();
            dataSet.ApplyOptions(options);
            dataSet.LoadFromQueryable(GetData());
            return dataSet;
        }

        [AllowStaticCommand]
        public static async Task<MultiSortGridViewDataSet> LoadMultiSort(GridViewDataSetOptions<NoFilteringOptions, MultiCriteriaSortingOptions, PagingOptions> options)
        {
            var dataSet = new MultiSortGridViewDataSet();
            dataSet.ApplyOptions(options);
            dataSet.LoadFromQueryable(GetData());
            return dataSet;
        }

        public class NextTokenGridViewDataSet() : GenericGridViewDataSet<CustomerData, NoFilteringOptions, SortingOptions, CustomerDataNextTokenPagingOptions, RowInsertOptions<CustomerData>, RowEditOptions>(
            new(), new(), new(), new(), new()
        );

        public class CustomerDataNextTokenPagingOptions : NextTokenPagingOptions, IApplyToQueryable, IPagingOptionsLoadingPostProcessor
        {
            public IQueryable<T> ApplyToQueryable<T>(IQueryable<T> queryable)
            {
                var token = int.Parse(CurrentToken ?? "0");

                return queryable.Cast<CustomerData>()
                    .OrderBy(c => c.CustomerId)
                    .Where(c => c.CustomerId > token)
                    .Take(3)
                    .Cast<T>();
            }

            public void ProcessLoadedItems<T>(IQueryable<T> filteredQueryable, IList<T> items)
            {
                var lastToken = items.Cast<CustomerData>()
                    .OrderByDescending(c => c.CustomerId)
                    .FirstOrDefault()?.CustomerId;

                lastToken ??= 0;
                if (lastToken == 12)
                {
                    NextPageToken = null;
                }
                else
                {
                    NextPageToken = lastToken.ToString();
                }
            }

            Task IPagingOptionsLoadingPostProcessor.ProcessLoadedItemsAsync<T>(IQueryable<T> filteredQueryable, IList<T> items, CancellationToken cancellationToken)
            {
                ProcessLoadedItems(filteredQueryable, items);
                return Task.CompletedTask;
            }
        }

        public class NextTokenHistoryGridViewDataSet() : GenericGridViewDataSet<CustomerData, NoFilteringOptions, SortingOptions, CustomerDataNextTokenHistoryPagingOptions, RowInsertOptions<CustomerData>, RowEditOptions>(
            new(), new(), new(), new(), new()
        );

        public class CustomerDataNextTokenHistoryPagingOptions : NextTokenHistoryPagingOptions, IApplyToQueryable, IPagingOptionsLoadingPostProcessor
        {
            private const int PageSize = 3;

            public IQueryable<T> ApplyToQueryable<T>(IQueryable<T> queryable)
            {
                if (TokenHistory.Count == 0)
                {
                    TokenHistory.Add(null);
                }
                var token = PageIndex < TokenHistory.Count ? int.Parse(TokenHistory[PageIndex] ?? "0") : 0;

                return queryable.Cast<CustomerData>()
                    .OrderBy(c => c.CustomerId)
                    .Where(c => c.CustomerId > token)
                    .Take(PageSize + 1)
                    .Cast<T>();
            }

            public void ProcessLoadedItems<T>(IQueryable<T> filteredQueryable, IList<T> items)
            {
                var hasMoreItems = false;
                while (items.Count > PageSize)
                {
                    items.RemoveAt(items.Count - 1);
                    hasMoreItems = true;
                }

                if (PageIndex == TokenHistory.Count - 1 && hasMoreItems)
                {
                    var lastToken = items.Cast<CustomerData>()
                        .LastOrDefault()?.CustomerId;

                    TokenHistory.Add((lastToken ?? 0).ToString());
                }
            }

            Task IPagingOptionsLoadingPostProcessor.ProcessLoadedItemsAsync<T>(IQueryable<T> filteredQueryable, IList<T> items, CancellationToken cancellationToken)
            {
                ProcessLoadedItems(filteredQueryable, items);
                return Task.CompletedTask;
            }
        }

        public class MultiSortGridViewDataSet() : GenericGridViewDataSet<CustomerData, NoFilteringOptions, MultiCriteriaSortingOptions, PagingOptions, RowInsertOptions<CustomerData>, RowEditOptions>(
            new(), new(), new(), new(), new()
        );
    }
}
