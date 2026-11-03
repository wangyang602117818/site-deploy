using System.Security.Cryptography;
using System.Text;

namespace SiteDeploy.Document.Models
{
    public class DingRobotHelper
    {
        //你的webhook基础token部分
        private const string AccessToken = "5581a20e5fa3d19d2bc0ec327f1019b82522cc3b281979d02796f7b3e65042c0";
        //填你的 SECxxxx 密钥
        private const string Secret = "SEC44b6e0a399185ce9492af78a9a53f1bd966b1bf504331c11f3d8449ef6c60f02";
        public static async Task SendDingInfo(string title, string content)
        {
            await SendDingLog(true, title, content);
        }
        public static async Task SendDingError(string title, string content)
        {
            await SendDingLog(false, title, content);
        }
        /// <summary>
        /// 发送钉钉日志消息，自动区分成功/错误颜色
        /// </summary>
        /// <param name="isSuccess">true=成功日志；false=错误告警</param>
        /// <param name="title">模块标题</param>
        /// <param name="content">正文内容</param>
        private static async Task SendDingLog(bool isSuccess, string title, string content)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string stringToSign = $"{timestamp}\n{Secret}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
            var signBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            string signBase64 = Convert.ToBase64String(signBytes);
            string signUrlEncode = Uri.EscapeDataString(signBase64);

            string statusMark;
            string colorHex;
            if (isSuccess)
            {
                statusMark = "✅任务执行完成！";
                colorHex = "#009944";
            }
            else
            {
                statusMark = "❌任务执行异常！";
                colorHex = "#dd2222";
            }

            var now = DateTime.Now.ToString("yyyy‑MM‑dd HH:mm:ss");

            string markdownText = $$"""
                ### {{title}}
                <font color="{{colorHex}}">{{statusMark}}</font>
                - **时间**: {{now}}
                - **内容**: {{content}}
                """;

            string jsonBody = (new
            {
                msgtype = "markdown",
                markdown = new
                {
                    title = title,
                    text = markdownText
                },
                at = new { isAtAll = false }
            }).Serialize();

            string url = $"https://oapi.dingtalk.com/robot/send?access_token={AccessToken}&timestamp={timestamp}&sign={signUrlEncode}";
            using var client = new HttpClient();
            var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(url, httpContent);
            var resText = await resp.Content.ReadAsStringAsync();
        }
    }
}
