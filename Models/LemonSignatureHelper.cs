using System.Security.Cryptography;
using System.Text;

namespace SiteDeploy.Document.Models
{
    public class LemonSignatureHelper
    {
        /// <summary>
        /// LemonSqueezy webhook HMAC‑SHA256签名校验
        /// </summary>
        /// <param name="payload">请求原始body字符串</param>
        /// <param name="signatureHeader">Http请求头 X‑Lemon‑Signature</param>
        /// <param name="webhookSecret">Lemon后台webhook secret</param>
        /// <returns>true=签名合法</returns>
        public static bool Verify(string payload, string? signatureHeader, string webhookSecret)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(webhookSecret))
                return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

            // 输出小写hex（官方要求hex digest，不是base64！）
            string computedHex = BitConverter.ToString(hashBytes)
                .Replace("-", "")
                .ToLowerInvariant();

            return computedHex == signatureHeader.ToLowerInvariant();
        }
    }
}
