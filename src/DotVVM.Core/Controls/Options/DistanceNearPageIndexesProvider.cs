using System;
using System.Collections.Generic;
using System.Linq;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Provides a list of page indexes near current paged based on distance.
    /// </summary>
    public class DistanceNearPageIndexesProvider : INearPageIndexesProvider
    {
        private readonly int distance;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistanceNearPageIndexesProvider" /> class.
        /// </summary>
        /// <param name="distance">The distance specifying the range of page indexes to return.</param>
        public DistanceNearPageIndexesProvider(int distance)
        {
            this.distance = distance;
        }

        /// <summary>
        /// Gets a list of page indexes near current page.
        /// </summary>
        /// <param name="pagingOptions">The settings for paging.</param>
        public IList<int> GetIndexes(IPagingOptions pagingOptions)
        {
            return Enumerable.Range(0, pagingOptions.PagesCount)
                .Where(n => Math.Abs(n - pagingOptions.PageIndex) <= distance)
                .ToList();
        }
    }
}