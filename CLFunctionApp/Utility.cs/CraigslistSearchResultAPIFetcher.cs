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

            var responseString = await response.Content.ReadAsStringAsync();

            var contentStreamJSONSubstring = ExtractDeserializableJSON(responseString);

            var products = JsonSerializer.Deserialize<Dictionary<string, CraigsListProduct>>(contentStreamJSONSubstring, new JsonSerializerOptions()
            {
                Converters = { new ProductDictionaryFromResponseConverter() }
            });

            return products;
        }

        private string ExtractDeserializableJSON(string responseContentString)
        {
            var openingBraceIndex = responseContentString.IndexOf('{');

            int closingBraceIndex = -1;

            for (var i = responseContentString.Length - 1; i >= 0 && closingBraceIndex == -1; i--)
            {
                var currentCharacter = responseContentString[i];
                if (currentCharacter == '}')
                {
                    closingBraceIndex = i;
                }
            }

            var contentStreamJSONSubstring = responseContentString.Substring(openingBraceIndex, closingBraceIndex - openingBraceIndex + 1);

            return contentStreamJSONSubstring;
        }
    }


    class ProductDictionaryFromResponseConverter : JsonConverter<Dictionary<string, CraigsListProduct>>
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

                    var imageUrls = item[5]
                        .EnumerateArray()
                        .Select(s => s.ToString())
                        // only valid image URls seem to begin with this
                        .Where(s => s.StartsWith("3:0"))
                        // format URL by removing the "3:" and constructing 
                        .Select(s => $"https://images.craigslist.org/{s.Substring(2)}_600x450.jpg")
                        .ToList();

                    var product = new CraigsListProduct()
                    {
                        Price = price.ToString(),
                        Url = title.ToString(),
                        ImageUrls = imageUrls
                    };

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
