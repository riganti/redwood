using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.ViewModel;
using DotVVM.Framework.Hosting;

namespace DotVVM.Samples.Common.ViewModels.ControlSamples.Timer
{
    public class TimerViewModel : DotvvmViewModelBase
    {
        public int Value1 { get; set; }
        public int Value2 { get; set; }
        public int Value3 { get; set; }

        public bool Enabled1 { get; set; } = true;
        public bool Enabled2 { get; set; } 
    }
}

