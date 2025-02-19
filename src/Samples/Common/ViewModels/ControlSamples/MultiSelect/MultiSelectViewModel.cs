using System.Collections.Generic;
using System;

namespace DotVVM.Samples.Common.ViewModels.ControlSamples.MultiSelect
{
	public class MultiSelectViewModel
	{
		public IEnumerable<string> SelectedValues { get; set; }
		public IEnumerable<string> Values => new[] { "Praha", "Brno", "Napajedla" };

		public int ChangedCount { get; set; }

		public void OnSelectionChanged()
		{
			ChangedCount++;
		}

		public void ChangeSelection()
		{
			SelectedValues = ["Brno", "Napajedla"];
		}

		public void ChangeSelectionHardcoded()
		{
            SelectedValues = ["2", "3"];
        }
    }
}
