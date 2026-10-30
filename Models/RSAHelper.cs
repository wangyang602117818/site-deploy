using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SiteDeploy.Document.Models
{
    public class RSAHelper
    {
        public RSAHelper() { }
        public void RASGen()
        {
            using RSA rsa = RSA.Create(2048);

            File.WriteAllText("license-private.pem", rsa.ExportRSAPrivateKeyPem());

            File.WriteAllText("license-public.pem", rsa.ExportSubjectPublicKeyInfoPem());

        }
        public Stream CreateLicenseFile()
        {
            // 组装授权业务对象
            var payloadObj = new
            {
                LicenseId = Guid.NewGuid().ToString("N"),
                IssueAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), // 签发时间戳
            };

            // 序列化为JSON payload（签名的原始数据）
            string payloadJson = JsonSerializer.Serialize(payloadObj);

            string? pemPrivateKeyContent;
            pemPrivateKeyContent = Environment.GetEnvironmentVariable("PRIVATE_KEY_CONTENT");
            if (string.IsNullOrEmpty(pemPrivateKeyContent))
            {
                string pemPrivateKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license-private.pem");
                if (System.IO.File.Exists(pemPrivateKeyPath))
                {
                    pemPrivateKeyContent = System.IO.File.ReadAllText(pemPrivateKeyPath);
                }
                else
                {
                    throw new FileNotFoundException("未找到私钥文件，且线上环境变量 PRIVATE_KEY_CONTENT 未配置。");
                }
            }
            // 对完整JSON做RSA‑SHA256签名
            string signature = SignData(payloadJson, pemPrivateKeyContent);

            // ✅ 重点：payload + 分隔符 + signature，完整写入文件
            string fileContent = $"{payloadJson}\n---SIGNATURE---\n{signature}";

            var bytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
            var stream = new MemoryStream(bytes);
            stream.Position = 0;
            return stream;
        }

        public string SignData(string data, string pemContent)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pemContent);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var sigBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sigBytes);
        }
    }
}
