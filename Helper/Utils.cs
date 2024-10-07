using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.AspNetCore.StaticFiles;
using TollMobileUpdateServer.DTO;

public class NoUpdateAvailableException : Exception { }

public static class Utils
{

    public static string CreateHash(byte[] file, string hashingAlgorithm, string encoding)
    {
        using var hashAlgorithm = HashAlgorithm.Create(hashingAlgorithm);
        if (hashAlgorithm == null)
            throw new InvalidOperationException("Invalid hashing algorithm");

        var hash = hashAlgorithm.ComputeHash(file);
        return encoding switch
        {
            "base64" => Convert.ToBase64String(hash),
            "hex" => BitConverter.ToString(hash).Replace("-", "").ToLower(),
            _ => throw new ArgumentException("Invalid encoding"),
        };
    }

    public static string GetBase64URLEncoding(string base64EncodedString)
    {
        return base64EncodedString.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public static IDictionary<string, object> ConvertToDictionaryItemsRepresentation(Dictionary<string, string> obj)
    {
        return obj.ToDictionary(kvp => kvp.Key, kvp => (object)new List<object> { kvp.Value, new Dictionary<string, object>() });
    }

    public static string SignRSASHA256(string data, string privateKey)  
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);

        var signedData = rsa.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signedData);
    }

    public static async Task<string> GetPrivateKeyAsync()
    {
        var privateKeyPath = Environment.GetEnvironmentVariable("PRIVATE_KEY_PATH");
        if (privateKeyPath == null)
            return null;

        var pemBuffer = await File.ReadAllTextAsync(Path.GetFullPath(privateKeyPath));
        return pemBuffer;
    }

    public static async Task<string> GetLatestUpdateBundlePathForRuntimeVersionAsync(string runtimeVersion)
    {
        var updatesDirectoryForRuntimeVersion = $"updates/{runtimeVersion}";
        if (!Directory.Exists(updatesDirectoryForRuntimeVersion))
            throw new Exception("Unsupported runtime version");

        var filesInUpdatesDirectory = Directory.GetFileSystemEntries(updatesDirectoryForRuntimeVersion);

        var directoriesInUpdatesDirectory = (await Task.WhenAll(filesInUpdatesDirectory
            .Select(async file =>
            {
                var fileStat = await Task.Run(() => new FileInfo(file));
                return fileStat.Attributes.HasFlag(FileAttributes.Directory) ? file : null;
            })))
            .Where(Truthy)
            .OrderByDescending(dir => int.Parse(Path.GetFileName(dir)))
            .ToList();

        return directoriesInUpdatesDirectory.First().Replace("\\","/");
    }

    public static async Task<Asset> GetAssetMetadataAsync(AssetMetadataArgs arg, string expoHostUrl)
    {
        var assetFilePath = $"{arg.UpdateBundlePath}/{arg.FilePath}";
        var asset = await File.ReadAllBytesAsync(Path.GetFullPath(assetFilePath));
        var assetHash = GetBase64URLEncoding(CreateHash(asset, "SHA256", "base64"));
        var key = CreateHash(asset, "MD5", "hex");
        var keyExtensionSuffix = arg.IsLaunchAsset ? "bundle" : arg.Ext;
        string contentType;
        new FileExtensionContentTypeProvider().TryGetContentType(arg.Ext, out contentType);

        return new Asset
        {
            Hash = assetHash,
            Key = key,
            FileExtension = $".{keyExtensionSuffix}",
            ContentType = contentType,
            Url = $"{expoHostUrl}/api/assets?asset={assetFilePath}&runtimeVersion={arg.RuntimeVersion}&platform={arg.Platform}"
        };
    }

    public static async Task<object> CreateRollBackDirectiveAsync(string updateBundlePath)
    {
        try
        {
            var rollbackFilePath = $"{updateBundlePath}/rollback";
            var rollbackFileStat = await Task.Run(() => new FileInfo(rollbackFilePath));
            return new
            {
                type = "rollBackToEmbedded",
                parameters = new
                {
                    commitTime = rollbackFileStat.CreationTimeUtc.ToString("o")
                }
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"No rollback found. Error: {ex.Message}");
        }
    }

    public static Task<object> CreateNoUpdateAvailableDirectiveAsync()
    {
        return Task.FromResult<object>(new { type = "noUpdateAvailable" });
    }

    public static async Task<Manifest> GetMetadataAsync(AssetMetadataArgs arg)
    {
        try
        {
            var metadataPath = $"{arg.UpdateBundlePath}/metadata.json";
            var updateMetadataBuffer = await File.ReadAllBytesAsync(Path.GetFullPath(metadataPath));
            var metadataJson = JsonConvert.DeserializeObject<MetadataJson>(Encoding.UTF8.GetString(updateMetadataBuffer));
            var metadataStat = await Task.Run(() => new FileInfo(metadataPath));

            return new Manifest()
            {
                MetadataJson = metadataJson,
                CreatedAt = metadataStat.CreationTimeUtc.ToString("o"),
                Id = CreateHash(updateMetadataBuffer, "SHA256", "hex")
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"No update found with runtime version: {arg.RuntimeVersion}. Error: {ex.Message}");
        }
    }

    public static async Task<ExpoClient> GetExpoConfigAsync(string updateBundlePath, string runtimeVersion)
    {
        try
        {
            var expoConfigPath = $"{updateBundlePath}/expoConfig.json";
            var expoConfigBuffer = await File.ReadAllTextAsync(Path.GetFullPath(expoConfigPath));
            return JsonConvert.DeserializeObject<ExpoClient>(expoConfigBuffer);
        }
        catch (Exception ex)
        {
            throw new Exception($"No expo config json found with runtime version: {runtimeVersion}. Error: {ex.Message}");
        }
    }

    public static string ConvertSHA256HashToUUID(string value)
    {
        return $"{value.Substring(0, 8)}-{value.Substring(8, 4)}-{value.Substring(12, 4)}-{value.Substring(16, 4)}-{value.Substring(20, 12)}";
    }

    public static bool Truthy<T>(T value) where T : class
    {
        return value != null;
    }
}
