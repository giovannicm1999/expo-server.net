using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using TollMobileUpdateServer.DTO;

namespace TollMobileUpdateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AssetsController : ControllerBase
    {
        private readonly ILogger<AssetsController> _logger;

        public AssetsController(ILogger<AssetsController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsset([FromQuery] string asset, [FromQuery] string runtimeVersion, [FromQuery] string platform)
        {
            if (string.IsNullOrEmpty(asset))
            {
                return BadRequest(new { error = "No asset name provided." });
            }

            if (platform != "ios" && platform != "android")
            {
                return BadRequest(new { error = "No platform provided. Expected \"ios\" or \"android\"." });
            }

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

            var assetMetadataArg = new AssetMetadataArgs()
            {
                UpdateBundlePath = updateBundlePath,
                RuntimeVersion = runtimeVersion,
            };

            var manifest = await Utils.GetMetadataAsync(assetMetadataArg);

            var assetPath = Path.GetFullPath(asset);
            var assets = (manifest.MetadataJson.FileMetadata.Android.Assets).Cast<Asset>();

            var assetMetadata = assets.FirstOrDefault(a => a.Path.Equals(asset.Replace($"{updateBundlePath}/", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase));

            var isLaunchAsset = manifest.MetadataJson.FileMetadata.Android.Bundle == asset.Replace($"{updateBundlePath}/", "", StringComparison.OrdinalIgnoreCase);

            if (!System.IO.File.Exists(assetPath))
            {
                return NotFound(new { error = $"Asset \"{asset}\" does not exist." });
            }

            try
            {
                var assetData = await System.IO.File.ReadAllBytesAsync(assetPath);

                string contentType;
                if (isLaunchAsset)
                    contentType = "application/javascript";
                else
                    new FileExtensionContentTypeProvider().TryGetContentType($".{assetMetadata.Ext}", out contentType);

                return File(assetData, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading asset file.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

    }
}
