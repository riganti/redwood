using DotVVM.Framework.Binding;
using DotVVM.Framework.Controls;
using DotVVM.Framework.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace DotVVM.Samples.BasicSamples.ViewModels.ControlSamples.GridView
{
    public class GridViewPagingSortingServerSideViewModel : DotvvmViewModelBase
    {
        private static IQueryable<CustomerData> GetData()
        {
            return new[]
            {
                new CustomerData() { CustomerId = 1, Name = "John Doe", BirthDate = DateTime.Parse("1976-04-01"), MessageReceived = false},
                new CustomerData() { CustomerId = 2, Name = "John Deer", BirthDate = DateTime.Parse("1984-03-02"), MessageReceived = false },
                new CustomerData() { CustomerId = 3, Name = "Johnny Walker", BirthDate = DateTime.Parse("1934-01-03"), MessageReceived = true},
                new CustomerData() { CustomerId = 4, Name = "Jim Hacker", BirthDate = DateTime.Parse("1912-11-04"), MessageReceived = true},
                new CustomerData() { CustomerId = 5, Name = "Joe E. Brown", BirthDate = DateTime.Parse("1947-09-05"), MessageReceived = false},
                new CustomerData() { CustomerId = 6, Name = "Jim Harris", BirthDate = DateTime.Parse("1956-07-06"), MessageReceived = false},
                new CustomerData() { CustomerId = 7, Name = "J. P. Morgan", BirthDate = DateTime.Parse("1969-05-07"), MessageReceived = false },
                new CustomerData() { CustomerId = 8, Name = "J. R. Ewing", BirthDate = DateTime.Parse("1987-03-08"), MessageReceived = false},
                new CustomerData() { CustomerId = 9, Name = "Jeremy Clarkson", BirthDate = DateTime.Parse("1994-04-09"), MessageReceived = false },
                new CustomerData() { CustomerId = 10, Name = "Jenny Green", BirthDate = DateTime.Parse("1947-02-10"), MessageReceived = false},
                new CustomerData() { CustomerId = 11, Name = "Joseph Blue", BirthDate = DateTime.Parse("1948-12-11"), MessageReceived = false},
                new CustomerData() { CustomerId = 12, Name = "Jack Daniels", BirthDate = DateTime.Parse("1968-10-12"), MessageReceived = true},
                new CustomerData() { CustomerId = 13, Name = "Jackie Chan", BirthDate = DateTime.Parse("1978-08-13"), MessageReceived = false},
                new CustomerData() { CustomerId = 14, Name = "Jasper", BirthDate = DateTime.Parse("1934-06-14"), MessageReceived = false},
                new CustomerData() { CustomerId = 15, Name = "Jumbo", BirthDate = DateTime.Parse("1965-06-15"), MessageReceived = false },
                new CustomerData() { CustomerId = 16, Name = "Junkie Doodle", BirthDate = DateTime.Parse("1977-05-16"), MessageReceived = false }
            }.AsQueryable();
        }

        public ServerSideGridViewDataSet<CustomerData> CustomersDataSet { get; set; } = new() {
            PagingOptions = new PagingOptions() {
                PageSize = 10
            }
        };
        public ServerSideGridViewDataSet<CustomerData> EmptyCustomersDataSet { get; set; } = new() {
            PagingOptions = new PagingOptions() {
                PageSize = 10
            }
        };

        [Bind(Direction.None)]
        public string SelectedSortColumn { get; set; }

        [Bind(Direction.None)]
        public List<CustomerData> Customers { get; set; }

        [Bind(Direction.None)]
        public List<CustomerData> Null { get; set; }

        public ServerSideGridViewDataSet<CustomerData> NullDataSet { get; set; }

        [Bind(Direction.None)]
        public string CustomNameForName { get; set; } = "Name";

        public override Task Load()
        {
            CustomersDataSet.RequestRefresh();
            LoadData();

            return base.Load();
        }

        public override Task PreRender()
        {
            LoadData();

            return base.PreRender();
        }

        private void LoadData()
        {
            if (CustomersDataSet.IsRefreshRequired)
                CustomersDataSet.LoadFromQueryable(GetData());

            if (SelectedSortColumn == "Name")
            {
                Customers = GetData().OrderBy(c => c.Name).ToList();
            }
            else if (SelectedSortColumn == "BirthDate")
            {
                Customers = GetData().OrderBy(c => c.BirthDate).ToList();
            }
            else
            {
                Customers = GetData().ToList();
            }
        }

        public void TestCommand()
        {
            if (CustomersDataSet.SortingOptions.SortExpression == "BirthDate")
            {
                CustomersDataSet.SortingOptions.SortDescending = !CustomersDataSet.SortingOptions.SortDescending;
            }
            else
            {
                CustomersDataSet.PagingOptions.PageIndex = 0;
                CustomersDataSet.SortingOptions.SortExpression = "BirthDate";
                CustomersDataSet.SortingOptions.SortDescending = false;
            }
        }

        public void SortCustomers(string column)
        {
            SelectedSortColumn = column;
        }

        [AllowStaticCommand]
        public List<CustomerData> GetDataList()
        {
            return GetData().ToList();
        }
    }
}
