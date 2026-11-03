using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SiteDeploy.Document.Models
{
    public class BrevoEmailHelper
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string _fromName = "SiteDeploy";
        private readonly string _fromEmail = "wy602117818@gmail.com";

        public BrevoEmailHelper(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = Environment.GetEnvironmentVariable("BrevoApiKey");
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = config["BrevoApiKey"]!;
            }
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }
        /// <summary>
        /// 发送带 license.lic 附件的邮件
        /// </summary>
        /// <param name="toEmail">收件人邮箱</param>
        /// <param name="toName">收件人名字</param>
        /// <param name="licenseStream">license文件流</param>
        public async Task SendLicenseEmailAsync(string toEmail, string toName, Stream licenseStream)
        {
            // 读取流并且转base64
            using var ms = new MemoryStream();
            await licenseStream.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            string fileBase64 = Convert.ToBase64String(fileBytes);

            var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "template", "license_template.html");
            var bodyObj = new
            {
                sender = new { name = _fromName, email = _fromEmail },
                to = new[] { new { email = toEmail, name = toName } },
                subject = "Your SiteDeploy License File",
                htmlContent = File.ReadAllText(path),
                attachment = new[]
                {
                    new {
                        name = "license.txt",
                        content = fileBase64
                    }
                }
            };

            string json = JsonSerializer.Serialize(bodyObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PostAsync("https://api.brevo.com/v3/smtp/email", content);
            var respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Brevo send fail: {resp.StatusCode}, {respText}");
            }
        }
        public async Task SendLicenseEmailAsync(string toEmail, string toName, string authCode)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "template", "verification_template.html");
            var bodyObj = new
            {
                sender = new { name = _fromName, email = _fromEmail },
                to = new[] { new { email = toEmail, name = toName } },
                subject = "Your Verification Code",
                htmlContent = File.ReadAllText(path).Replace("{code}", authCode),
            };

            string json = JsonSerializer.Serialize(bodyObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PostAsync("https://api.brevo.com/v3/smtp/email", content);
            var respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Brevo send fail: {resp.StatusCode}, {respText}");
            }
        }
    }
}
