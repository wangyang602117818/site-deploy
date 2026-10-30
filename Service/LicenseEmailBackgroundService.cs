using Microsoft.EntityFrameworkCore;
using Serilog;
using SiteDeploy.Document.DBContext;
using SiteDeploy.Document.Models;

namespace SiteDeploy.Document.Service
{
    public class LicenseEmailBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        // 轮询间隔：30秒查一次数据库，可自行调整
        private const int ScanIntervalSeconds = 30;

        public LicenseEmailBackgroundService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("LicenseEmailBackgroundService 已启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 创建Scope，获取DbContext（BackgroundService是单例，不能直接注入DbContext）
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var emailHelper = scope.ServiceProvider.GetRequiredService<EmailHelper>();

                    // 查询：未发送邮件、没有退款的订单
                    var pendingOrders = await db.LsOrders
                        .Where(o => !o.IsEmailSend && !o.IsRefunded)
                        .ToListAsync(stoppingToken);

                    if (pendingOrders.Any())
                    {
                        Log.Information($"发现待发送授权邮件订单 {pendingOrders.Count} 条");

                        foreach (var order in pendingOrders)
                        {
                            try
                            {
                                // 你的生成license流方法
                                using var licenseStream = new RSAHelper().CreateLicenseFile();
                                // 发送邮件
                                await emailHelper.SendLicenseEmailAsync(order.CustomerEmail, order.CustomerName, licenseStream);
                                // 发送成功更新标记
                                order.IsEmailSend = true;
                                order.EmailSentAt = DateTime.UtcNow;
                                await db.SaveChangesAsync(stoppingToken);

                                Log.Information($"订单{order.OrderId}授权邮件发送成功 {order.CustomerEmail}");
                                DingRobotHelper.SendDingInfo("授权邮件发送成功", $"订单{order.OrderId}授权邮件发送成功 {order.CustomerEmail}").Wait();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, $"订单{order.OrderId}发送邮件失败，下一轮会重试");
                                DingRobotHelper.SendDingError("授权邮件发送失败", $"订单{order.OrderId}发送邮件失败 {order.CustomerEmail}，下一轮会重试 {ex.Message}").Wait();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "后台扫描订单发生异常");
                }

                // 等待N秒继续下一轮
                await Task.Delay(TimeSpan.FromSeconds(ScanIntervalSeconds), stoppingToken);
            }

            Log.Information("LicenseEmailBackgroundService 停止");
        }
    }
}
