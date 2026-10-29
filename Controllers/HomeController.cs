using Microsoft.AspNetCore.Mvc;
using SiteDeploy.Document.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SiteDeploy.Document.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult GetLicenseFile()
        {
            Stream stream = new RSAHelper().CreateLicenseFile();
            string contentType = "application/octet-stream";
            string fileName = "license.lic";
            return File(stream, contentType, fileName);
        }


    }
}
