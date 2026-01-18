using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShadiAbuJaber.DotNet.PlayerProject.Models
{
    public class ItunesSearchResponse
    {
        [JsonPropertyName("resultCount")]
        public int ResultCount { get; set; }

        [JsonPropertyName("results")]
        public List<ItunesResultItem>? Results { get; set; }
    }
}
