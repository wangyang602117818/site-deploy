using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Resend;
using Serilog;
using SiteDeploy.Document.DBContext;
using SiteDeploy.Document.Models;
using SiteDeploy.Document.Service;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "log.txt");
Serilog.Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddHttpClient();
builder.Services.AddScoped<BrevoEmailHelper>();
builder.Services.AddHostedService<LicenseEmailBackgroundService>();

builder.Services.AddResend(options =>
{
    options.ApiToken = builder.Configuration["Resend:ApiKey"]!;
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "DeployAuth-Document";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

string? connectionString = Environment.GetEnvironmentVariable("MongoConnstring");
if (string.IsNullOrEmpty(connectionString)) connectionString = builder.Configuration["MongoConnstring"]!;

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMongoDB(
        connectionString: connectionString,
        databaseName: "SiteDeployDB"
    );
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

var supportedCultures = new[]
{
    new CultureInfo("zh-CN"),
    new CultureInfo("en-US")
};

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

localizationOptions.RequestCultureProviders = new IRequestCultureProvider[]
{
    new QueryStringRequestCultureProvider { QueryStringKey = "culture" },
    new CookieRequestCultureProvider(),
    new AcceptLanguageHeaderRequestCultureProvider()
};

app.UseRequestLocalization(localizationOptions);
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
