using System;
using System.IO;
using System.Text.Json;
using FunctionApp1.Utility.cs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CLFunctionApp
{
    public class CraigslistPhotoDownloader
    {
        private readonly ILogger _logger;

        public CraigslistPhotoDownloader(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CraigslistPhotoDownloader>();
        }

        [Function("CraigslistPhotoDownloader")]
        public void Run([BlobTrigger("listings/{name}")] string myBlob, string name)
        {
            _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {myBlob}");
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, CraigsListProduct>>(myBlob);
        }
    }
}
