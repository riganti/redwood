using System;
using System.Collections.Generic;
using DotVVM.Framework.ViewModel;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Represents a collection of items with paging and sorting which keeps the Items collection server-side (Bind(Direction.None)) and only transfers the necessary metadata (page index, sort direction). 
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    public class ServerSideGridViewDataSet<T>()
        : GenericGridViewDataSet<T, NoFilteringOptions, SortingOptions, PagingOptions, NoRowInsertOptions, NoRowEditOptions>(new(), new(), new(), new(), new())
    {

        [Bind(Direction.None)]
        public override IList<T> Items { get => base.Items; set => base.Items = value; }
        // return specialized dataset options
        public new GridViewDataSetOptions GetOptions()
        {
            return new GridViewDataSetOptions {
                FilteringOptions = FilteringOptions,
                SortingOptions = SortingOptions,
                PagingOptions = PagingOptions
            };
        }
    }
}
