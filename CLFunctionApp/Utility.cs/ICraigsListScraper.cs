using FunctionApp1.Utility.cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CLFunctionApp.Utility.cs
{
    public interface ICraigsListScraper
    {
        Task<IDictionary<string, CraigsListProduct>> ScrapeListings(string url);
    }
}
