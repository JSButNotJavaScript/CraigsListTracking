using Azure.Storage.Blobs;
using CLFunctionApp.Utility.cs;
using FunctionApp1.Utility.cs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace CLFunctionApp
{
    public class CraigsListMonitor
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly ICraigsListScraper _craigsListScraper;
        private readonly DiscordLogger _discordLogger;

        public CraigsListMonitor(ILoggerFactory loggerFactory,
             IConfiguration configuration,
             ICraigsListScraper craigsListScraper,
             DiscordLogger discordLogger)
        {
            _logger = loggerFactory.CreateLogger<CraigsListMonitor>();
            _configuration = configuration;
            _craigsListScraper = craigsListScraper;

            var configurationDictionary = _configuration.GetChildren().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ADDED_LISTINGS_DISCORD_WEBHOOK = configurationDictionary[nameof(ADDED_LISTINGS_DISCORD_WEBHOOK)];
            SOLD_LISTINGS_DISCORD_WEBHOOK = configurationDictionary[nameof(SOLD_LISTINGS_DISCORD_WEBHOOK)];
            MONITOR_HEALTH_DISCORD_WEBHOOK = configurationDictionary[nameof(MONITOR_HEALTH_DISCORD_WEBHOOK)];
            CRAIGSLIST_SEARCH_URL = configurationDictionary[nameof(CRAIGSLIST_SEARCH_URL)];

            if (!configurationDictionary.TryGetValue(nameof(LISTINGS_BLOB_NAME), out LISTINGS_BLOB_NAME))
            {
                LISTINGS_BLOB_NAME = "ListingDictionary";
            }

            _discordLogger = discordLogger;

        }

        /// <summary>
        /// Receives message when a new listing is added
        /// </summary>
        private static string ADDED_LISTINGS_DISCORD_WEBHOOK;

        /// <summary>
        /// Receives a message on every run of the function app, also whenever an exception occurs
        /// </summary>
        private static string MONITOR_HEALTH_DISCORD_WEBHOOK;

        /// <summary>
        /// Receives message when a listing is sold
        /// </summary>
        private static string SOLD_LISTINGS_DISCORD_WEBHOOK;

        /// <summary>
        /// URL to scrape craigslist listings from
        /// </summary>
        private static string CRAIGSLIST_SEARCH_URL;

        private static string LISTINGS_BLOB_NAME;

        private static readonly string BLOB_CONTAINER_NAME = "listings";


        // 0 * * * * *	every minute	09:00:00; 09:01:00; 09:02:00; � 10:00:00
        // 0 */5 * * * *	every 5 minutes	09:00:00; 09:05:00, ...
        // 0 0 * * * *	every hour(hourly) 09:00:00; 10:00:00; 11:00:00
        [Function("Function1")]
        public async Task Run([TimerTrigger("0 * * * * *")] TimerInfo myTimer

            )
        {
            try
            {
                _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
                _logger.LogInformation("Started with Parameters: ");
                _logger.LogInformation($"LISTINGS_BLOB_NAME : {LISTINGS_BLOB_NAME}");
                _logger.LogInformation($"BLOB_CONTAINER_NAME : {BLOB_CONTAINER_NAME}");
                _logger.LogInformation($"CRAIGSLIST_SEARCH_URL : {CRAIGSLIST_SEARCH_URL}");

                var currentListings = await _craigsListScraper.ScrapeListings(CRAIGSLIST_SEARCH_URL);

                var blobClient = await GetListingsBlobClient();

                var previousListings = await GetPreviousListings(blobClient);

                var newlyPostedListings = await ComparePreviousAndCurrentListings(currentListings, previousListings);

                var anyNewPosts = newlyPostedListings.Count > 0;

                if (anyNewPosts)
                {
                    var description = "**NEW/UPDATED LISTINGS:**  \n" + string.Join(" \n", newlyPostedListings.Select(l => $"{l.Url}  ${(l.Price)}"));
                    var header = $"{newlyPostedListings.Count} NEW POSTS ";
                    var title = $"Total Search Total Results: {currentListings.Count}";

                    var discordMessages = newlyPostedListings
                        .Select(l => new DiscordMessage() { ImageUrl = l.ImageUrls.FirstOrDefault(), Description = l.Url, Title = l.Price, Header = l.Title })
                        .ToList();

                    (var postDiscordMessageSucceeded, string[] errorMessages) = await _discordLogger.LogMessages(ADDED_LISTINGS_DISCORD_WEBHOOK, discordMessages);


                    if (!postDiscordMessageSucceeded)
                    {
                        await _discordLogger.LogMessage(MONITOR_HEALTH_DISCORD_WEBHOOK,
                            new DiscordMessage() { Header = "failed to post update. ", Title = $"previous listing had {previousListings.Count} results. Current listings now has {currentListings.Count}", Description = string.Join('\n', errorMessages) });

                        _logger.LogError("Failed to log one or more discord messages", errorMessages);
                    }

                }

                await LogMonitorHealth(previousListings.Count);

                // store all listings that have been seen preivously
                foreach (var listingKeyValue in previousListings)
                {
                    currentListings[listingKeyValue.Key] = listingKeyValue.Value;
                }

                if (!await UploadProductsToBlob(currentListings, blobClient))
                {
                    _logger.LogError("Failed to upload listings");
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task LogMonitorHealth(int previousListingsCount)
        {
            var loggingFrequencyMinutes = 5;
            var currentTime = DateTime.UtcNow;

            var isTimeToLog = currentTime.Minute % loggingFrequencyMinutes == 0;

            if (isTimeToLog)
            {
                await _discordLogger.LogMessage(MONITOR_HEALTH_DISCORD_WEBHOOK, new DiscordMessage() { Header = $"{nameof(CraigsListMonitor)} still running", Title = $"previous listing had {previousListingsCount} results" });
            }
        }

        private async Task<Dictionary<string, CraigsListProduct>> GetPreviousListings(BlobClient blobClient)
        {

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("Blob does not exist. This should only happen on the first run. Returning empty dictionary");
                return new Dictionary<string, CraigsListProduct>();
            }

            byte[] oldListingBytes;

            using (MemoryStream ms = new MemoryStream())


            using (var oldListingStream = await blobClient.OpenReadAsync())
            {
                oldListingStream.CopyTo(ms);
                oldListingBytes = ms.ToArray();
            }

            var oldListingString = Encoding.UTF8.GetString(oldListingBytes);

            var dictionary = JsonSerializer.Deserialize<Dictionary<string, CraigsListProduct>>(oldListingString);

            return dictionary;
        }

        private async Task<BlobContainerClient> BuildBlobContainerClient()
        {
            var connString = _configuration.GetValue<string>("AzureWebJobsStorage");

            var client = new BlobContainerClient(connString, BLOB_CONTAINER_NAME);

            var response = await client.CreateIfNotExistsAsync();

            if (!await client.ExistsAsync())
            {
                _logger.LogInformation("Created Blob Container");
                await client.CreateAsync();
            }

            return client;
        }

        private async Task<BlobClient> GetListingsBlobClient()
        {
            var blobContainerClient = await BuildBlobContainerClient();

            var blobClient = blobContainerClient.GetBlobClient(LISTINGS_BLOB_NAME);

            return blobClient;
        }

        /// <summary>
        /// Creates lists of sold postings and newly added postings 
        /// Sold postings are listings that were present during the last run, and are no longer present. Vice-versa for newly added postings
        /// </summary>
        /// <param name="currentListings">current craigslist listings</param>
        /// <param name="previousListings">craigslist listings from the last run</param>
        /// <returns>lists of sold postings and newly added postings</returns>
        private async Task<IList<CraigsListProduct>>
           ComparePreviousAndCurrentListings(IDictionary<string, CraigsListProduct> currentListings, IDictionary<string, CraigsListProduct> previousListings)
        {

            var newlyPostedListings = currentListings.Values.Where(l => !previousListings.ContainsKey(l.Url)).ToList();

            return newlyPostedListings;
        }

        private async Task<bool> UploadProductsToBlob(IDictionary<string, CraigsListProduct> products, BlobClient blobClient)
        {
            var serializedDictionary = JsonSerializer.Serialize(products);


            var content = Encoding.UTF8.GetBytes(serializedDictionary);
            using (var ms = new MemoryStream(content))
                await blobClient.UploadAsync(ms, true);

            return true;
        }
    }
}
