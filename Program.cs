using Microsoft.EntityFrameworkCore;
using Serilog;
using SiteDeploy.Document.DBContext;
using SiteDeploy.Document.Models;
using SiteDeploy.Document.Service;

var builder = WebApplication.CreateBuilder(args);


string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "log.txt");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

//邮件帮助类
builder.Services.AddScoped<EmailHelper>();
//注册后台服务
builder.Services.AddHostedService<LicenseEmailBackgroundService>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite("Data Source=app.db");
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
