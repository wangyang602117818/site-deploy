using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SiteDeploy.Document.Models
{
    public class RSAHelper
    {
        public static string PrivateKeyXml = $@"-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEAskBEBOc7ODSMm/D6g76iqD7FiE/hsTxHaPklN/cBVAbrg9G1
kuY/1cLpWuOHy4QFfYepuMjLtEt1Yi6jVcI/Q4VZAreSug50lRWHFdgsE/En/wJm
lsPZkx9YPHfDfmCwIp9W6Hl1AzF0qPQ/jVm7weTDkdvKB+rRGJdFcS6CziVJEv22
PcI6uf0Lm16Zv9liskuo1EQghlMDiUE2w9mnjWPzcJeYnNPeAhiy4aLSsgKaDa+4
e9Xubj7OGNth7HRhF2TBezJMtKJUssZEqL9Ig3YAQnNMI8ICcJgu+2Fo44psD2ls
ot+w3efohG7+LR2u/2a0rNFmCA6r/ne13HW/LQIDAQABAoIBAHUSyJn/1WFhAkkZ
yS/mcabXM54H+VATK0PC/7L2RKUPtnfpsLw/BgY78lCDyJjeqUx9Yfe/yisstHg7
8YNOBYcvefrS30fcXq3cadDrHmgqp/5RcVfsYmbC2407i+bhUk4rIUWnIDdssBaX
9XW9ThhXTCvsk5EHpTXlhPKFfdwufzbX9IlxKORNlXK8gOm9LnIC93ieK3Pk3BX6
vM7UBDdhA6n5e8BanV8XLhatobFyQsl3ZWFlnV+sjPzstL4XA5ufArSLw/6ptmdf
Ho6LhPrGasHpJ+Q+YoDxuzkxYsozYbVcEQUgUImvfCxz9QZ996jLNX7Fq6vfZEm0
xFiAW/ECgYEAwsov+2+LtNI8VvgRjvkBOIg7u3yogka/dcPAk8XXdIE4gNTJWPPx
U1q/9O4Rd9ShG9jwHP2x1YSUFC99sOozsT+TomDHJlvfjeRhIarGLmA860QFwWuK
LS7sidFJT90sdQea8hy9aQ4o0WwFWe57Q6XoZhRSmQVn9NGDb7QECQMCgYEA6kOf
V/mkiUA8QikO7fRFJNECYct6MX5nI4Kc9BsA00YvbR6F0qUQhESXutkFLzFJfOY0
uwJeoIeYPN1Xl2UBOtqVrmQCSC9VPi0Rk3u2bDK2rpn3AuV553u9UeImlfJSwrZ1
liL4iyv6h4aCnKQmlOTDtiPcHOkVPbkdxlQwaA8CgYBcYdwD2dFeft8TOC/LqRkF
IMeDkgmxT7B6P87Txt76aFbN8x1TTM7Fg6LKnSY3uOZ3+JlYhpGS/H/mWue3wjul
8ssoFdIdxTDvcMMIXqADWq1Jv0cjhsdPypNwgYvNjs4I6wR3k13Zt1dwi1S2ZD6d
xM/FqhnKbR/yonM9xRis1QKBgFXLwlX0BgCGWAtbAi6/4XrkWVz7qprp0lg9A8Kb
CShXchMitJw6+9uVS4SQ6scn4+QsY2+CJKniPy07W1MFGPg3Cc6+rd9GBpAo7WD4
VA04XZOAq+iRpp3Swbl1e/A6uXi6XkJIOIKv7qcn6jn4HHE3fz370Cd+bdsXKwJx
3vCdAoGBALEo3bm4hU3LEGeAACTD7I5JaIeLX2OAi8VsCOyzXql/k6k2ITSmMFnb
XPlI3CQc2JME/FjDBGPIs6KD+0NEAEHr8QXk5ccHLXyx5CsPeyKip68tiorILEWd
bix2OX4hT3Q9o7ArGlLI72rYBSjJ4YIxqSUsSwoHBIFVGVraRxLL
-----END RSA PRIVATE KEY-----";
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

            // 对完整JSON做RSA‑SHA256签名
            string signature = SignData(payloadJson, PrivateKeyXml);

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
