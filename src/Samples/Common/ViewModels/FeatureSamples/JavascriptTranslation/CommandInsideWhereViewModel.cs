using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.ViewModel;
using DotVVM.Framework.Hosting;

namespace DotVVM.Samples.Common.ViewModels.FeatureSamples.JavascriptTranslation
{
	public class CommandInsideWhereViewModel : DotvvmViewModelBase
	{
        public int? LastMessageCustomerId { get; set; }

        public List<CustomerData> Customers { get; set; } = [
            new() { CustomerId = 1, Name = "John Doe", BirthDate = DateTime.Parse("1976-04-01"), MessageReceived = true },
            new() { CustomerId = 2, Name = "John Deer", BirthDate = DateTime.Parse("1984-03-02") },
            new() { CustomerId = 3, Name = "Johnny Walker", BirthDate = DateTime.Parse("1934-01-03") },
            new() { CustomerId = 4, Name = "Jim Hacker", BirthDate = DateTime.Parse("1912-11-04"), MessageReceived = true },
            new() { CustomerId = 5, Name = "Joe E. Brown", BirthDate = DateTime.Parse("1947-09-05"), MessageReceived = true },
            new() { CustomerId = 6, Name = "Jim Harris", BirthDate = DateTime.Parse("1956-07-06") },
            new() { CustomerId = 7, Name = "J. P. Morgan", BirthDate = DateTime.Parse("1969-05-07") },
            new() { CustomerId = 8, Name = "J. R. Ewing", BirthDate = DateTime.Parse("1987-03-08"), MessageReceived = true },
            new() { CustomerId = 9, Name = "Jeremy Clarkson", BirthDate = DateTime.Parse("1994-04-09") },
            new() { CustomerId = 10, Name = "Jenny Green", BirthDate = DateTime.Parse("1947-02-10") },
            new() { CustomerId = 11, Name = "Joseph Blue", BirthDate = DateTime.Parse("1948-12-11"), MessageReceived = true },
            new() { CustomerId = 12, Name = "Jack Daniels", BirthDate = DateTime.Parse("1968-10-12") },
            new() { CustomerId = 13, Name = "Jackie Chan", BirthDate = DateTime.Parse("1978-08-13") },
            new() { CustomerId = 14, Name = "Jasper", BirthDate = DateTime.Parse("1934-06-14"), MessageReceived = true },
            new() { CustomerId = 15, Name = "Jumbo", BirthDate = DateTime.Parse("1965-06-15"), MessageReceived = true },
            new() { CustomerId = 16, Name = "Junkie Doodle", BirthDate = DateTime.Parse("1977-05-16"), MessageReceived = true }
        ];

        public void SendMessage(int? customerId)
        {
            LastMessageCustomerId = customerId;
        }
    }

    public class CustomerData
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }

        public DateTime BirthDate { get; set; }
        public bool MessageReceived { get; set; }
    }
}

