using FunctionApp1.Utility.cs;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace CLFunctionApp.Utility.cs
{
    public class CraigsListWebDriverScraper : ICraigsListScraper
    {
        private ILogger _logger;

        public CraigsListWebDriverScraper(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CraigsListWebDriverScraper>();
        }

        // CraigsList loads images into cards dynamically, so we need to scroll each card into view first before we extract their image urls
        private void ScrollEachCardIntoView(ChromeDriver chromeDriver)
        {
            var cardElement = chromeDriver.FindElement(By.ClassName("gallery-card"));

            var scrollHeight = cardElement.Size.Height;

            var documentHeight = (long)chromeDriver.ExecuteScript("return document.documentElement.scrollHeight");

            var amountScrolled = 0;

            while (amountScrolled < documentHeight)
            {
                chromeDriver.ExecuteScript($"window.scrollBy(0, {scrollHeight})");
                amountScrolled += scrollHeight;
                Thread.Sleep(500);
            }
        }

        public async Task<IDictionary<string, CraigsListProduct>> ScrapeListings(string url)
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments("--headless");

            var chromeDriver = new ChromeDriver(chromeOptions);

            chromeDriver.Navigate().GoToUrl(url);

            var elements = chromeDriver.FindElements(By.ClassName("gallery-card"));

            if (elements.Count() == 0)
            {
                return new Dictionary<string, CraigsListProduct>();
            }

            ScrollEachCardIntoView(chromeDriver);

            var products = new Dictionary<string, CraigsListProduct>();

            foreach (var element in elements)
            {
                string? imageUrl = null;
                try
                {
                    var imgElement = element.FindElement(By.TagName("img"));
                    imageUrl = imgElement?.GetAttribute("src") ?? "";
                }
                catch (Exception ex)
                {
                };

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

                _logger.LogInformation($"Added product :  {System.Text.Json.JsonSerializer.Serialize(craigsListProduct)}");
            }

            chromeDriver.Close();

            return products;
        }
    }
}
