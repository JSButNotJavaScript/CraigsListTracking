using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FunctionApp1.Utility.cs
{
    public class DiscordLogger
    {
        private HttpClient _httpClient;

        private string _userName;

        public DiscordLogger(HttpClient? httpClient, string userName = "webhook")
        {
            _userName = userName;
            _httpClient = httpClient ?? new HttpClient();
        }

        public record DiscordMessagePayload
        {
            public DiscordEmbed[] embeds { get; set; }
            public string username { get; set; }
        }

        private string FormatMessageInBody(string title, string header, string description)
        {
            var embed = new DiscordEmbed()
            {
                Author = new Author()
                {
                    Name = header,
                },
                Title = title,
                Description = description,
                Color = 0
            };

            var data = new DiscordMessagePayload
            {
                embeds = new DiscordEmbed[] { embed },
                username = _userName
            };

            return JsonConvert.SerializeObject(data);
        }


        private string FormatMessageInBody(string title, string header, string description, string imageURL)
        {
            var embed = new DiscordEmbed()
            {
                Author = new Author()
                {
                    Name = header
                },
                Title = title,
                Description = description,
                Color = 0,
                Image = new Image()
                {
                    Url = imageURL,
                },
            };

            var data = new
            {
                embeds = new DiscordEmbed[] { embed },
                username = _userName
            };
            return JsonConvert.SerializeObject(data);
        }

        private string FormatMessageInBody(DiscordMessage message)
        {

            var embed = MessageToEmbed(message);

            var data = new
            {
                embeds = new DiscordEmbed[] { embed },
                username = _userName
            };
            return JsonConvert.SerializeObject(data);
        }

        private DiscordEmbed MessageToEmbed(DiscordMessage message)
        {
            var embed = new DiscordEmbed()
            {
                Author = new Author()
                {
                    Name = message.Header
                },
                Title = message.Title,
                Description = message.Description,
                Color = 0,
                Image = new Image()
                {
                    Url = message.ImageUrl,
                },
            };

            return embed;
        }

        private string FormatMessagesInBody(IEnumerable<DiscordMessage> messages)
        {
            var embeddedMessages = messages.Select(message =>
            {
                var embed = MessageToEmbed(message);
                return embed;
            });

            var data = new
            {
                embeds = embeddedMessages.ToArray(),
                username = _userName
            };
            return JsonConvert.SerializeObject(data);
        }

        public async Task<bool> LogMessage(string webhookUrl, string message)
        {
            var formattedMessage = FormatMessageInBody(message, message, message);
            var content = new StringContent(formattedMessage, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> LogMessage(string webhookUrl, DiscordMessage message)
        {
            var formattedMessage = message.ImageUrl is null ? FormatMessageInBody(message.Title, message.Header, message.Description)
                : FormatMessageInBody(message);

            var content = new StringContent(formattedMessage, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content);
            return response.IsSuccessStatusCode;
        }

        public async Task<(bool, string[])> LogMessages(string webhookUrl, IList<DiscordMessage> messages)
        {
            var sendMessageTasks = new List<Task<HttpResponseMessage>>();

            // max amount of embeds a discord message can have
            var amountToTake = 10;
            var amountTaken = 0;

            var next10Messages = messages.Skip(amountTaken).Take(amountToTake);
            while (next10Messages.Count() > 0)
            {
                amountTaken += 10;
                var formattedMessage = FormatMessagesInBody(next10Messages);
                var content = new StringContent(formattedMessage, Encoding.UTF8, "application/json");
                sendMessageTasks.Add(_httpClient.PostAsync(webhookUrl, content));
                next10Messages = messages.Skip(amountTaken).Take(amountToTake);
            }

            var responses = await Task.WhenAll(sendMessageTasks);

            var failedResponses = responses.Where(r => !r.IsSuccessStatusCode).ToList();

            if (failedResponses.Count == 0)
            {
                return (true, new string[] { });
            }

            var errorMessages = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
            return (false, errorMessages.ToArray());
        }

        public record DiscordEmbed
        {
            [JsonProperty("author")]
            public Author Author;

            [JsonProperty("title")]
            public string Title;

            [JsonProperty("description")]
            public string Description;

            [JsonProperty("color")]
            public int Color;

            [JsonProperty("image")]
            public Image? Image;
        }

        public record Author
        {
            [JsonProperty("name")]
            public string Name;
        }
        public record Image
        {
            [JsonProperty("url")]
            public string Url;
        }

    }
    public record DiscordMessage
    {
        public string Header { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public string? ImageUrl { get; set; }
    }
}
