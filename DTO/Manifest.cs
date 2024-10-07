using Newtonsoft.Json;

namespace TollMobileUpdateServer.DTO
{
    public class Manifest
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("runtimeVersion")]
        public string RuntimeVersion { get; set; }

        [JsonProperty("fileMetadata")]
        public FileMetadata FileMetadata { get; set; }

        [JsonProperty("metadataJson")]
        public MetadataJson MetadataJson { get; set; }

        [JsonProperty("launchAsset")]
        public Asset LaunchAsset { get; set; }

        [JsonProperty("assets")]
        public List<Asset> Assets { get; set; }

        [JsonProperty("metadata")]
        public object Metadata { get; set; }

        [JsonProperty("extra")]
        public ManifestExtra Extra { get; set; }

    }
}
