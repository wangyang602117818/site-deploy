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
        public static bool Verify(byte[] rawBodyBytes, string? signatureHex, string webhookSecret)
        {
            if (string.IsNullOrWhiteSpace(signatureHex) || string.IsNullOrWhiteSpace(webhookSecret))
                return false;

            using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(webhookSecret));
            byte[] computedHash = hmac.ComputeHash(rawBodyBytes);

            // 将header的hex签名字符串转回byte数组
            byte[] receivedHash;
            try
            {
                receivedHash = Convert.FromHexString(signatureHex);
            }
            catch
            {
                return false;
            }

            // 长度不一样直接失败
            if (computedHash.Length != receivedHash.Length)
                return false;

            // ✅常量时间安全对比，等价Node crypto.timingSafeEqual
            return CryptographicOperations.FixedTimeEquals(computedHash, receivedHash);
        }
    }
}
