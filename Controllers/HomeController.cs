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
    }
}
