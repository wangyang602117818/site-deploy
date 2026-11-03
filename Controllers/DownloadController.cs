using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteDeploy.Document.DBContext;
using SiteDeploy.Document.Models;

namespace SiteDeploy.Document.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        public DownloadController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        [HttpGet("file/{subFolder}/{fileName}")]
        public IActionResult DownloadFile(string subFolder, string fileName)
        {
            // 限制子目录防止越权
            if (subFolder != "framework-dependency" && subFolder != "self-contained")
            {
                return BadRequest(new { code = 400, message = "Invalid directory parameter" });
            }

            var safeFileName = Path.GetFileName(fileName);
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files", subFolder, safeFileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { code = 404, message = $"File {safeFileName} does not exist." });
            }

            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(safeFileName, out var contentType))
            {
                contentType = "application/octet-stream";
            }
            return PhysicalFile(filePath, contentType, safeFileName);
        }
        [HttpGet("downloadDbFile")]
        public IActionResult DownloadDbFile()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "site.db");
            return PhysicalFile(filePath, "application/octet-stream", "site.db");
        }
        [HttpGet("downloadLicense/{orderid}")]
        [Authorize]
        public IActionResult DownloadLicense(string orderid)
        {
            var exists = _dbContext.LsOrders.FirstOrDefault(f => f.OrderId == orderid);
            if (exists != null)
            {
                var licenseStream = new RSAHelper().CreateLicenseFile();
                return File(licenseStream, "application/octet-stream", "license.lic");
            }
            return NotFound();
        }
    }
}
