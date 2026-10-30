using MimeKit;
using System.Net.Mail;
using MailKit.Net.Smtp;
using static System.Net.Mime.MediaTypeNames;

namespace SiteDeploy.Document.Models
{
    public class EmailHelper
    {
        private readonly IConfiguration _config;

        public EmailHelper(IConfiguration config)
        {
            _config = config;
        }
        /// <summary>
        /// 发送带license.lic附件邮件
        /// </summary>
        /// <param name="toEmail">收件人邮箱</param>
        /// <param name="toName">收件人名字</param>
        /// <param name="licenseStream">license文件流</param>
        public async Task SendLicenseEmailAsync(string toEmail, string toName, Stream licenseStream)
        {
            var host = _config["MailOption:Server"]!;
            var port = int.Parse(_config["MailOption:Port"]!);
            var user = _config["MailOption:User"]!;
            var pass = _config["MailOption:Password"]!;
            var fromName = "wang";
            var fromEmail = _config["MailOption:User"]!;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = "SiteDeploy 授权文件 license.lic";

            licenseStream.Position = 0;
            var attachment = new MimePart("application", "octet-stream")
            {
                Content = new MimeContent(licenseStream),
                FileName = "license.lic"
            };

            var body = new TextPart("plain")
            {
                Text = $@"您好 {toName}！
                        感谢购买 SiteDeploy。
                        附件是你的授权文件 license.lic。
                        将此文件放置程序目录完成激活。"
            };

            var multipart = new MimeKit.Multipart("mixed");
            multipart.Add(body);
            multipart.Add(attachment);
            message.Body = multipart;

            using var client = new MailKit.Net.Smtp.SmtpClient();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
