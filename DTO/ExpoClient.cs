namespace TollMobileUpdateServer.DTO
{
    public class ExpoClient
    {
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Owner { get; set; }
        public string Version { get; set; }
        public string Orientation { get; set; }
        public string Icon { get; set; }
        public Splash Splash { get; set; }
        public string RuntimeVersion { get; set; }
        public Update Updates { get; set; }
        public List<string> AssetBundlePatterns { get; set; }
        public Ios Ios { get; set; }
        public Android Android { get; set; }
        public Web Web { get; set; }
        public string SdkVersion { get; set; }
        public List<string> Platforms { get; set; }
        public string CurrentFullName { get; set; }
        public string OriginalFullName { get; set; }
    }

    public class Splash
    {
        public string Image { get; set; }
        public string ResizeMode { get; set; }
        public string BackgroundColor { get; set; }
    }

    public class Update
    {
        public string Url { get; set; }
        public bool Enabled { get; set; }
        public int FallbackToCacheTimeout { get; set; }
    }

    public class Ios
    {
        public bool SupportsTablet { get; set; }
        public string BundleIdentifier { get; set; }
    }

    public class Android
    {
        public AdaptiveIcon AdaptiveIcon { get; set; }
        public string Package { get; set; }
    }

    public class AdaptiveIcon
    {
        public string ForegroundImage { get; set; }
        public string BackgroundColor { get; set; }
    }

    public class Web
    {
        public string Favicon { get; set; }
    }
}
