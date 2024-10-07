using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TollMobileUpdateServer.DTO;

namespace TollMobileUpdateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ManifestController : ControllerBase
    {
        private readonly ILogger<ManifestController> _logger;
        private readonly IConfiguration _configuration;

        public ManifestController(ILogger<ManifestController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetManifest([FromQuery] string platform = "", [FromQuery(Name = "expo-runtime-version")] string runtimeVersion = "")
        {
            if (Request.Method != "GET")
            {
                return StatusCode(405, new { error = "Expected GET." });
            }

            var protocolVersionHeader = Request.Headers["expo-protocol-version"].ToString();
            var protocolVersion = !string.IsNullOrEmpty(protocolVersionHeader) ? int.Parse(protocolVersionHeader) : 0;

            platform = Request.Headers["expo-platform"];
            if (platform != "ios" && platform != "android")
            {
                return BadRequest(new { error = "Unsupported platform. Expected either ios or android." });
            }

            runtimeVersion = Request.Headers["expo-runtime-version"];
            if (string.IsNullOrEmpty(runtimeVersion))
            {
                return BadRequest(new { error = "No runtimeVersion provided." });
            }

            string updateBundlePath;
            try
            {
                updateBundlePath = await Utils.GetLatestUpdateBundlePathForRuntimeVersionAsync(runtimeVersion);
            }
            catch (Exception ex)
            {
                return NotFound(new { error = ex.Message });
            }

            var updateType = await GetTypeOfUpdateAsync(updateBundlePath);

            try
            {
                if (updateType == UpdateType.NormalUpdate)
                {
                    await PutUpdateInResponseAsync(protocolVersion, platform, updateBundlePath, runtimeVersion);
                }
                else if (updateType == UpdateType.Rollback)
                {
                    await PutRollBackInResponseAsync(protocolVersion, updateBundlePath);
                }
            }
            catch (NoUpdateAvailableException)
            {
                await PutNoUpdateAvailableInResponseAsync(protocolVersion);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request.");
                return StatusCode(500, new { error = ex.Message });
            }

            return Ok();
        }

        private enum UpdateType
        {
            NormalUpdate,
            Rollback
        }

        private async Task<UpdateType> GetTypeOfUpdateAsync(string updateBundlePath)
        {
            var directoryContents = Directory.GetFiles(updateBundlePath);
            return directoryContents.Any(f => Path.GetFileName(f) == "rollback") ? UpdateType.Rollback : UpdateType.NormalUpdate;
        }

        private async Task PutUpdateInResponseAsync(int protocolVersion, string platform, string updateBundlePath, string runtimeVersion)
        {
            var currentUpdateId = Request.Headers["expo-current-update-id"].ToString();
            var assetMetadataArg = new AssetMetadataArgs()
            {
                UpdateBundlePath = updateBundlePath,
                RuntimeVersion = runtimeVersion,
            };

            var manifestMetadata = await Utils.GetMetadataAsync(assetMetadataArg);

            if (currentUpdateId == Utils.ConvertSHA256HashToUUID(manifestMetadata.Id) && protocolVersion == 1)
            {
                throw new NoUpdateAvailableException();
            }

            var plataformMetadata = manifestMetadata.MetadataJson.FileMetadata.Android;

            var assetsMetadata = new List<string>();
            var launchAsset = await Utils.GetAssetMetadataAsync(new AssetMetadataArgs()
            {
                UpdateBundlePath = updateBundlePath,
                FilePath = plataformMetadata.Bundle,
                IsLaunchAsset = true,
                RuntimeVersion = runtimeVersion,
                Platform = platform,
                Ext = null
            }, _configuration.GetSection("ExpoServerHost").Value);
            var manifestExtra = new ManifestExtra() { ExpoClient = await Utils.GetExpoConfigAsync(updateBundlePath, runtimeVersion) };
            var assets = await Task.WhenAll(plataformMetadata.Assets.Select(async (asset) =>
            {
                return await Utils.GetAssetMetadataAsync(new AssetMetadataArgs()
                {
                    UpdateBundlePath = updateBundlePath,
                    FilePath = asset.Path,
                    Ext = asset.Ext,
                    IsLaunchAsset = false,
                    RuntimeVersion = runtimeVersion,
                    Platform = platform,
                }, _configuration.GetSection("ExpoServerHost").Value);
            }));

            var manifest = new Manifest()
            {
                Id = Utils.ConvertSHA256HashToUUID(manifestMetadata.Id),
                CreatedAt = manifestMetadata.CreatedAt,
                RuntimeVersion = runtimeVersion,
                Assets = assets.ToList(),
                LaunchAsset = launchAsset,
                Metadata = new { },
                Extra = manifestExtra
            };

            var signature = await GetSignatureIfNeededAsync(manifest);

            var form = new MultipartFormDataContent($"----boundary");
            form.Add(new StringContent(JsonConvert.SerializeObject(manifest), Encoding.UTF8, "application/json"), "manifest");

            Response.Headers["expo-protocol-version"] = protocolVersion.ToString();
            Response.Headers["expo-sfv-version"] = "0";
            Response.Headers["cache-control"] = "private, max-age=0";
            Response.ContentType = "multipart/mixed; boundary=----boundary";

            await Response.Body.WriteAsync(await form.ReadAsByteArrayAsync());
        }

        private async Task PutRollBackInResponseAsync(int protocolVersion, string updateBundlePath)
        {
            if (protocolVersion == 0)
            {
                throw new Exception("Rollbacks not supported on protocol version 0");
            }

            var embeddedUpdateId = Request.Headers["expo-embedded-update-id"].ToString();
            if (string.IsNullOrEmpty(embeddedUpdateId))
            {
                throw new Exception("Invalid Expo-Embedded-Update-ID request header specified.");
            }

            var currentUpdateId = Request.Headers["expo-current-update-id"].ToString();
            if (currentUpdateId == embeddedUpdateId)
            {
                throw new NoUpdateAvailableException();
            }

            var directive = await Utils.CreateRollBackDirectiveAsync(updateBundlePath);
            var signature = await GetSignatureIfNeededAsync(directive);

            var form = new MultipartFormDataContent($"----boundary");
            form.Add(new StringContent(JsonConvert.SerializeObject(directive), Encoding.UTF8, "application/json"), "directive");

            Response.Headers["expo-protocol-version"] = "1";
            Response.Headers["expo-sfv-version"] = "0";
            Response.Headers["cache-control"] = "private, max-age=0";
            Response.ContentType = "multipart/mixed; boundary=----boundary";

            await Response.Body.WriteAsync(await form.ReadAsByteArrayAsync());
        }

        private async Task PutNoUpdateAvailableInResponseAsync(int protocolVersion)
        {
            if (protocolVersion == 0)
            {
                throw new Exception("NoUpdateAvailable directive not available in protocol version 0");
            }

            var directive = await Utils.CreateNoUpdateAvailableDirectiveAsync();
            var signature = await GetSignatureIfNeededAsync(directive);

            var form = new MultipartFormDataContent($"----boundary");
            form.Add(new StringContent(JsonConvert.SerializeObject(directive), Encoding.UTF8, "application/json"), "directive");

            Response.Headers["expo-protocol-version"] = "1";
            Response.Headers["expo-sfv-version"] = "0";
            Response.Headers["cache-control"] = "private, max-age=0";
            Response.ContentType = "multipart/mixed; boundary=----boundary";

            await Response.Body.WriteAsync(await form.ReadAsByteArrayAsync());
        }

        private async Task<string> GetSignatureIfNeededAsync(object data)
        {
            var expectSignatureHeader = Request.Headers["expo-expect-signature"].ToString();
            if (string.IsNullOrEmpty(expectSignatureHeader)) return null;

            var privateKey = await Utils.GetPrivateKeyAsync();
            if (privateKey == null)
            {
                throw new InvalidOperationException("Code signing requested but no key supplied when starting server.");
            }

            var dataString = JsonConvert.SerializeObject(data);
            var hashSignature = Utils.SignRSASHA256(dataString, privateKey);
            
            var hashDictionary = new Dictionary<string, string>
            {
                { "sig", hashSignature },
                { "keyid", "main" }
            };

            var dictionary = Utils.ConvertToDictionaryItemsRepresentation(hashDictionary);
            string serializedDictionary = System.Text.Json.JsonSerializer.Serialize(dictionary, new JsonSerializerOptions { WriteIndented = true });

            return serializedDictionary;
        }
    }
}