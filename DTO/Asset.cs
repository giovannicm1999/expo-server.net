using Newtonsoft.Json;

namespace TollMobileUpdateServer.DTO
{
    public class Asset
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("fileExtension")]
        public string FileExtension { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("ext")]
        public string Ext { get; set; }
    }
}
