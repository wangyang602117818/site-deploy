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
        private readonly string? _lsWebhookSecret;
        private readonly string? _dodopaymentsKey;
        private readonly IWebHostEnvironment _env;
        public WebhookController(AppDbContext dbContext, IConfiguration configuration, IWebHostEnvironment env)
        {
            _dbContext = dbContext;
            _lsWebhookSecret = Environment.GetEnvironmentVariable("LemonSqueezySecret");
            if (string.IsNullOrEmpty(_lsWebhookSecret)) _lsWebhookSecret = configuration["LemonSqueezySecret"]!;
            _dodopaymentsKey = Environment.GetEnvironmentVariable("DodoPaymentsKey");
            if (string.IsNullOrEmpty(_dodopaymentsKey)) _dodopaymentsKey = configuration["DodoPaymentsKey"]!;
            _env = env;
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

            if (!_env.IsDevelopment())
            {
                // 传入原始字节数组验签！
                if (!LemonSignatureHelper.Verify(rawBodyBytes, signatureHeader, _lsWebhookSecret))
                {
                    return Unauthorized("Webhook signature verification failed");
                }
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

            var user = _dbContext.Users.Where(w => w.Email == lsOrder.CustomerEmail).FirstOrDefault();
            if (user == null)
            {
                user = new User
                {
                    Email = lsOrder.CustomerEmail,
                };
                _dbContext.Users.Add(user);
            }
            _dbContext.LsOrders.Add(lsOrder);

            await _dbContext.SaveChangesAsync();

            return Ok();
        }
        [HttpPost("dodopayments")]
        public async Task<IActionResult> DodoPayments()
        {
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            byte[] rawBody = ms.ToArray();
            string payload = Encoding.UTF8.GetString(rawBody);

            var headers = new Dictionary<string, string>
            {
                ["webhook-id"] = Request.Headers["webhook-id"].FirstOrDefault() ?? "",
                ["webhook-signature"] = Request.Headers["webhook-signature"].FirstOrDefault() ?? "",
                ["webhook-timestamp"] = Request.Headers["webhook-timestamp"].FirstOrDefault() ?? ""
            };

            try
            {
                string eventType = string.Empty;
                if (!_env.IsDevelopment())
                {
                    if (!VerifyDodoPaymentsSignature(headers, rawBody, _dodopaymentsKey))
                    {
                        Serilog.Log.Warning("Dodo signature verify fail");
                        return Unauthorized("Webhook signature verification failed");
                    }
                }

                using var jsonDoc = JsonDocument.Parse(payload);
                var root = jsonDoc.RootElement;
                if (string.IsNullOrWhiteSpace(eventType))
                {
                    eventType = GetStringByPaths(root, new[] { "type" }) ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(eventType))
                {
                    return BadRequest("Missing event type");
                }

                var data = TryGetProperty(root, "data", out var dataElement) ? dataElement : root;

                string orderId = GetStringByPaths(data,
                    new[] { "payment_id" },
                    new[] { "id" },
                    new[] { "transaction_id" },
                    new[] { "order_id" }) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(orderId))
                {
                    return BadRequest("Missing payment id");
                }

                string? customerEmail = GetStringByPaths(data,
                    new[] { "customer", "email" },
                    new[] { "billing", "email" },
                    new[] { "email" });

                string customerName = GetStringByPaths(data,
                    new[] { "customer", "name" },
                    new[] { "billing", "name" },
                    new[] { "name" }) ?? string.Empty;

                string orderNumber = GetStringByPaths(data,
                    new[] { "order_number" },
                    new[] { "order_id" },
                    new[] { "payment_id" },
                    new[] { "id" }) ?? orderId;

                bool isRefunded = eventType == "payment.refunded";
                bool isTestMode = GetBoolByPaths(root,
                    new[] { "test_mode" },
                    new[] { "data", "test_mode" }) ?? false;

                var existsOrder = await _dbContext.LsOrders.FirstOrDefaultAsync(x => x.OrderId == orderId);
                if (existsOrder != null)
                {
                    if (isRefunded && !existsOrder.IsRefunded)
                    {
                        existsOrder.IsRefunded = true;
                        existsOrder.RawPayload = payload;
                        await _dbContext.SaveChangesAsync();
                    }

                    return Ok("order processed!");
                }

                if (string.IsNullOrWhiteSpace(customerEmail))
                {
                    return BadRequest("Missing customer email");
                }

                customerEmail = customerEmail.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(customerName))
                {
                    customerName = customerEmail;
                }

                var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == customerEmail);
                if (user == null)
                {
                    user = new User { Email = customerEmail };
                    _dbContext.Users.Add(user);
                }

                _dbContext.LsOrders.Add(new LsOrder
                {
                    OrderId = orderId,
                    OrderNumber = orderNumber,
                    CustomerEmail = customerEmail,
                    CustomerName = customerName,
                    IsTestMode = isTestMode,
                    IsRefunded = isRefunded,
                    RawPayload = payload,
                    CreatedAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync();
                return Ok();
            }
            catch (JsonException)
            {
                return BadRequest("Invalid payload");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "DodoPayments webhook process failed");
                return StatusCode(500, "Webhook process failed");
            }
        }

        private static bool VerifyDodoPaymentsSignature(Dictionary<string, string> headers, byte[] rawBody, string? secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
                return false;

            if (!headers.TryGetValue("webhook-id", out var webhookId) ||
                !headers.TryGetValue("webhook-timestamp", out var webhookTimestamp) ||
                !headers.TryGetValue("webhook-signature", out var webhookSignature) ||
                string.IsNullOrWhiteSpace(webhookId) ||
                string.IsNullOrWhiteSpace(webhookTimestamp) ||
                string.IsNullOrWhiteSpace(webhookSignature))
            {
                return false;
            }

            // 时间戳防重放，5分钟
            if (!long.TryParse(webhookTimestamp, out long ts))
                return false;
            var tsTime = DateTimeOffset.FromUnixTimeSeconds(ts);
            var now = DateTimeOffset.UtcNow;
            if (Math.Abs((now - tsTime).TotalSeconds) > 300)
                return false;

            var key = secret;
            if (key.StartsWith("whsec_", StringComparison.OrdinalIgnoreCase))
            {
                key = key[6..];
            }

            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(key);
            }
            catch
            {
                Serilog.Log.Error("Dodo webhook secret base64 decode failed");
                return false;
            }

            // 正确拼接：id.ts. + rawBody
            var prefix = $"{webhookId}.{webhookTimestamp}.";
            byte[] prefixBytes = Encoding.UTF8.GetBytes(prefix);
            byte[] signedPayloadBytes = prefixBytes.Concat(rawBody).ToArray();

            using var hmac = new HMACSHA256(keyBytes);
            byte[] digest = hmac.ComputeHash(signedPayloadBytes);
            string expectedB64 = Convert.ToBase64String(digest);

            var signatures = webhookSignature.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var sigItem in signatures)
            {
                var commaIndex = sigItem.IndexOf(',');
                string actualB64 = commaIndex >= 0 ? sigItem[(commaIndex + 1)..] : sigItem;

                if (FixedTimeEqualsBase64(expectedB64, actualB64))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool FixedTimeEqualsBase64(string leftB64, string rightB64)
        {
            try
            {
                byte[] b1 = Convert.FromBase64String(leftB64);
                byte[] b2 = Convert.FromBase64String(rightB64);
                return b1.Length == b2.Length && CryptographicOperations.FixedTimeEquals(b1, b2);
            }
            catch
            {
                return false;
            }
        }


        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static string? GetStringByPaths(JsonElement root, params string[][] paths)
        {
            foreach (var path in paths)
            {
                JsonElement current = root;
                bool ok = true;
                foreach (var segment in path)
                {
                    if (!TryGetProperty(current, segment, out var next))
                    {
                        ok = false;
                        break;
                    }

                    current = next;
                }

                if (!ok)
                {
                    continue;
                }

                if (current.ValueKind == JsonValueKind.String)
                {
                    return current.GetString();
                }

                if (current.ValueKind == JsonValueKind.Number ||
                    current.ValueKind == JsonValueKind.True ||
                    current.ValueKind == JsonValueKind.False)
                {
                    return current.ToString();
                }
            }

            return null;
        }

        private static bool? GetBoolByPaths(JsonElement root, params string[][] paths)
        {
            foreach (var path in paths)
            {
                JsonElement current = root;
                bool ok = true;
                foreach (var segment in path)
                {
                    if (!TryGetProperty(current, segment, out var next))
                    {
                        ok = false;
                        break;
                    }

                    current = next;
                }

                if (!ok)
                {
                    continue;
                }

                if (current.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (current.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (current.ValueKind == JsonValueKind.String && bool.TryParse(current.GetString(), out var boolResult))
                {
                    return boolResult;
                }
            }

            return null;
        }
    }
}
