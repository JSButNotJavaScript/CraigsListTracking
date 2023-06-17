using FunctionApp1.Utility.cs;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace CLFunctionApp.Utility.cs
{
    public class CraigsListWebDriverScraper : ICraigsListScraper
    {
        public async Task<IDictionary<string, CraigsListProduct>> ScrapeListings(string url)
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments("--headless");

            var chromeDriver = new ChromeDriver(chromeOptions);

            chromeDriver.Navigate().GoToUrl(url);

            var elements = chromeDriver.FindElements(By.ClassName("gallery-card"));

            var products = new Dictionary<string, CraigsListProduct>();

            foreach (var element in elements)
            {
                var imageUrl = "";
                try
                {
                    var imageElement = element.FindElement(By.TagName("img"));
                    imageUrl = imageElement.GetAttribute("src");
                }
                catch (NoSuchElementException) { };

                var titleElement = element.FindElement(By.ClassName("titlestring"));

                var priceElement = element.FindElement(By.ClassName("priceinfo"));

                var craigsListProduct = new CraigsListProduct
                {
                    Url = titleElement.GetAttribute("href"),
                    ImageUrl = imageUrl,
                    Title = titleElement.Text,
                    Price = priceElement.Text
                };

                products[craigsListProduct.Url] = craigsListProduct;
            }

            return products;
        }

    }
}
