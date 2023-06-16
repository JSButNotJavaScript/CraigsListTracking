using CLFunctionApp.Utility.cs;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FunctionApp1.Utility.cs
{
    public class CraigslistSearchResultAPIFetcher : ICraigsListScraper
    {
        public async Task<IDictionary<string, CraigsListProduct>> ScrapeListings(string url)
        {
            var httpClient = new HttpClient();

            string userAgent = "CraigsListSearchResultFetcher/1.0";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Add("User-Agent", userAgent);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Status Code from CraigsList request did not indicate success");
            }

            var contentStream = await response.Content.ReadAsStringAsync();

            var products = JsonSerializer.Deserialize<Dictionary<string, CraigsListProduct>>(contentStream, new JsonSerializerOptions()
            {
                Converters = { new ProductDictionaryFromResponseConverter() }
            });

            return products;
        }

    }

    public class ProductDictionaryFromResponseConverter : JsonConverter<Dictionary<string, CraigsListProduct>>
    {
        public override Dictionary<string, CraigsListProduct> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {

            var dictionary = new Dictionary<string, CraigsListProduct>();

            using (var jsonDoc = JsonDocument.ParseValue(ref reader))
            {
                // need to access root.data.items, which is an array of arrays
                JsonElement root = jsonDoc.RootElement;

                var dataJson = root.GetProperty("data");

                var items = dataJson.GetProperty("items").EnumerateArray();

                foreach (var item in items)
                {
                    var price = item[3].ToString();
                    var title = item.EnumerateArray().Last().ToString();

                    var product = new CraigsListProduct()
                    {
                        Price = price.ToString(),
                        Url = title.ToString(),
                    };

                    if (dictionary.ContainsKey(title))
                    {
                        var a = item;
                    }

                    dictionary[title] = product;
                }

                return dictionary;
            }
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, CraigsListProduct> value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
