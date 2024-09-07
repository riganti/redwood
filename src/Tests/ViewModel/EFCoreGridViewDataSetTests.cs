#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotVVM.Framework.Controls;
using DotVVM.Framework.ViewModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotVVM.Framework.Tests.ViewModel
{
    [TestClass]
    public class EFCoreGridViewDataSetTests
    {
        private readonly DbContextOptions<MyDbContext> contextOptions;

        public EFCoreGridViewDataSetTests()
        {
            contextOptions = new DbContextOptionsBuilder<MyDbContext>()
                .UseInMemoryDatabase("BloggingControllerTest")
                .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        class MyDbContext: DbContext
        {
            public MyDbContext(DbContextOptions options) : base(options)
            {
            }

            public DbSet<Entry> Entries { get; set; }
        }

        record Entry(int Id, string Name, int SomethingElse = 0);

        MyDbContext Init()
        {
            var context = new MyDbContext(contextOptions);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.Entries.AddRange([
                new (1, "Z"),
                new (2, "Y"),
                new (3, "X"),
                new (4, "W"),
                new (5, "V"),
                new (6, "U", 5),
                new (7, "T", 5),
                new (8, "S", 5),
                new (9, "R", 3),
                new (10, "Q", 3),
            ]);
            context.SaveChanges();
            return context;
        }

        [TestMethod]
        public void LoadData_PagingSorting()
        {
            using var context = Init();

            var dataSet = new GridViewDataSet<Entry>()
            {
                PagingOptions = { PageSize = 3, PageIndex = 0 },
                SortingOptions = { SortExpression = nameof(Entry.Name), SortDescending = false },
            };

            dataSet.LoadFromQueryable(context.Entries);

            Assert.AreEqual(3, dataSet.Items.Count);
            Assert.AreEqual(10, dataSet.PagingOptions.TotalItemsCount);
            Assert.AreEqual(10, dataSet.Items[0].Id);
            Assert.AreEqual(9, dataSet.Items[1].Id);
            Assert.AreEqual(8, dataSet.Items[2].Id);
        }

        [TestMethod]
        public void LoadData_PagingSorting_PreFiltered()
        {
            using var context = Init();

            var dataSet = new GridViewDataSet<Entry>()
            {
                PagingOptions = { PageSize = 3, PageIndex = 0 },
                SortingOptions = { SortExpression = nameof(Entry.Name), SortDescending = false },
            };

            dataSet.LoadFromQueryable(context.Entries.Where(e => e.SomethingElse == 3));

            Assert.AreEqual(2, dataSet.Items.Count);
            Assert.AreEqual(2, dataSet.PagingOptions.TotalItemsCount);
            Assert.AreEqual(10, dataSet.Items[0].Id);
            Assert.AreEqual(9, dataSet.Items[1].Id);
        }

        [TestMethod]
        public async Task LoadData_PagingSortingAsync()
        {
            using var context = Init();

            var dataSet = new GridViewDataSet<Entry>()
            {
                PagingOptions = { PageSize = 3, PageIndex = 0 },
                SortingOptions = { SortExpression = nameof(Entry.Name), SortDescending = false },
            };

            await dataSet.LoadFromQueryableAsync(context.Entries);

            Assert.AreEqual(3, dataSet.Items.Count);
            Assert.AreEqual(10, dataSet.PagingOptions.TotalItemsCount);
            Assert.AreEqual(10, dataSet.Items[0].Id);
            Assert.AreEqual(9, dataSet.Items[1].Id);
            Assert.AreEqual(8, dataSet.Items[2].Id);
        }

        [TestMethod]
        public async Task LoadData_PagingSorting_PreFilteredAsync()
        {
            using var context = Init();

            var dataSet = new GridViewDataSet<Entry>()
            {
                PagingOptions = { PageSize = 3, PageIndex = 0 },
                SortingOptions = { SortExpression = nameof(Entry.Name), SortDescending = false },
            };

            await dataSet.LoadFromQueryableAsync(context.Entries.Where(e => e.SomethingElse == 3));

            Assert.AreEqual(2, dataSet.Items.Count);
            Assert.AreEqual(2, dataSet.PagingOptions.TotalItemsCount);
            Assert.AreEqual(10, dataSet.Items[0].Id);
            Assert.AreEqual(9, dataSet.Items[1].Id);
        }
    }
}
#endif
