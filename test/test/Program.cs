using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using test.Services;
using test.Services.Interfaces;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

const string localSiteUrl = "http://localhost:7050";
const string localSiteMarker = "pzt43-local-schedule";

var executableName = Path.GetFileNameWithoutExtension(
    Environment.ProcessPath ?? "");

var isLocalLaunch = args.Any(x =>
    x.Equals(
        "--local-launch",
        StringComparison.OrdinalIgnoreCase)) ||
    executableName.Equals(
        "Расписание",
        StringComparison.OrdinalIgnoreCase) ||
    executableName.Equals(
        "Расписание ГГПК",
        StringComparison.OrdinalIgnoreCase) ||
    executableName.Equals(
        "Raspisanie",
        StringComparison.OrdinalIgnoreCase);

if (isLocalLaunch &&
    await IsSiteAvailableAsync(
        localSiteUrl,
        localSiteMarker))
{
    OpenBrowser(localSiteUrl);
    return;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = isLocalLaunch
        ? AppContext.BaseDirectory
        : Directory.GetCurrentDirectory()
});

if (isLocalLaunch)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole();
    builder.WebHost.UseUrls(localSiteUrl);
}

var dataProtectionDirectory = new DirectoryInfo(
    Path.Combine(
        AppContext.BaseDirectory,
        "DataProtection-Keys"));

dataProtectionDirectory.Create();

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(dataProtectionDirectory)
    .SetApplicationName("PZT43Schedule");

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IScheduleParserService, ScheduleParserService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("X-Frame-Options");
    context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'self' *;");
    await next();
});

if (!isLocalLaunch)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet(
    "/local-launch-status",
    () => Results.Text(localSiteMarker));

if (isLocalLaunch)
{
    app.Lifetime.ApplicationStarted.Register(() =>
        OpenBrowser(localSiteUrl));
}

app.Run();

static async Task<bool> IsSiteAvailableAsync(
    string url,
    string expectedMarker)
{
    try
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(1)
        };

        var response = await client.GetStringAsync(
            $"{url}/local-launch-status");

        return response.Equals(
            expectedMarker,
            StringComparison.Ordinal);
    }
    catch
    {
        return false;
    }
}

static void OpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch
    {
    }
}
