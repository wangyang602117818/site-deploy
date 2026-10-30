using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SiteDeploy.Document.DBContext;
using SiteDeploy.Document.Models;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SiteDeploy.Document.Controllers
{
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly string _lsWebhookSecret;
        public WebhookController(AppDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _lsWebhookSecret = configuration["LemonSqueezySecret"]!;
        }
        [HttpPost("lemonsqueezy")]
        public async Task<IActionResult> LemonSqueezy()
        {
            Request.EnableBuffering();

            byte[] rawBodyBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                rawBodyBytes = ms.ToArray();
                Request.Body.Position = 0; //重置流，框架后续如果要读还可以读
            }

            // 为了业务解析，再转成字符串用于json解析
            string payload = System.Text.Encoding.UTF8.GetString(rawBodyBytes);
            var signatureHeader = Request.Headers["x-signature"].FirstOrDefault();
            // 传入原始字节数组验签！
            if (!LemonSignatureHelper.Verify(rawBodyBytes, signatureHeader, _lsWebhookSecret))
            {
                return Unauthorized("Webhook signature verification failed");
            }

            var jsonDoc = JsonSerializer.Deserialize<JsonElement>(payload);
            var meta = jsonDoc.GetProperty("meta");
            string eventName = meta.GetProperty("event_name").GetString()!;
            bool isTestMode = meta.GetProperty("test_mode").GetBoolean();

            var data = jsonDoc.GetProperty("data");
            string orderId = data.GetProperty("id").GetString()!;
            var attributes = data.GetProperty("attributes");

            bool orderExists = await _dbContext.LsOrders.AnyAsync(o => o.OrderId == orderId);
            if (orderExists)
            {
                return Ok("order processed!");
            }

            var lsOrder = new LsOrder
            {
                OrderId = orderId,
                OrderNumber = attributes.GetProperty("order_number").ToString(),
                CustomerEmail = attributes.GetProperty("user_email").GetString()!,
                CustomerName = attributes.GetProperty("user_name").GetString()!,
                IsTestMode = isTestMode,
                IsRefunded = eventName == "order_refunded",
                RawPayload = payload,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.LsOrders.Add(lsOrder);
            await _dbContext.SaveChangesAsync();

            return Ok();
        }
        //public IActionResult Lemonsqueezy()
        //{
        //    Serilog.Log.Information("Webhook received from Lemonsqueezy");
        //    Stream stream = new RSAHelper().CreateLicenseFile();
        //    string contentType = "application/octet-stream";
        //    string fileName = "license.lic";
        //    return File(stream, contentType, fileName);
        //}
    }
}
