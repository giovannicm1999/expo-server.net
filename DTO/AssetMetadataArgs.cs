namespace TollMobileUpdateServer.DTO
{
    public class AssetMetadataArgs
    {
        public string UpdateBundlePath { get; set; }
        public string FilePath { get; set; }
        public string Ext { get; set; }
        public bool IsLaunchAsset { get; set; }
        public string RuntimeVersion { get; set; }
        public string Platform { get; set; }
    }
}
