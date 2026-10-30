using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteDeploy.Document.DBContext;
using SiteDeploy.Document.Models;
using System;
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

            string payload;
            using (var reader = new StreamReader(Request.Body))
            {
                payload = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
            }

            var signatureHeader = Request.Headers["x-signature"].FirstOrDefault();
            if (!LemonSignatureHelper.Verify(payload, signatureHeader, _lsWebhookSecret))
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
