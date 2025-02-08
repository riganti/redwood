using DotVVM.Framework.Controls;
using DotVVM.Framework.ViewModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotVVM.Samples.BasicSamples.ViewModels.ControlSamples.DataPager
{
    public class DataPagerTemplatesViewModel : DotvvmViewModelBase
    {
        public GridViewDataSet<Data> DataSet { get; set; }

        public string[] RomanNumerals { get; set; } = new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX" };

        public override Task Init()
        {
            DataSet = new GridViewDataSet<Data>()
            {
                PagingOptions = new PagingOptions()
                {
                    PageSize = 3
                }
            };
            return base.Init();
        }

        public override Task PreRender()
        {
            if (DataSet.IsRefreshRequired)
            {
                DataSet.LoadFromQueryable(FakeDB(50));
            }

            return base.PreRender();
        }

        private IQueryable<Data> FakeDB(int itemsCreatorCounter)
        {
            var dbdata = new List<Data>();
            for (var i = 0; i < itemsCreatorCounter; i++)
            {
                dbdata.Add(new Data
                {
                    Text = $"Item {i}"
                });
            }
            return dbdata.AsQueryable();
        }

        public class Data
        {
            public string Text { get; set; }
        }
    }
}
