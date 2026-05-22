using GoldPriceTracker.Models;
using GoldPriceTracker.Services;
using Microsoft.Extensions.Hosting.WindowsServices;



var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Enable running as a Windows Service
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "GoldPriceService";
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the custom services
builder.Services.Configure<TwilioSettings>(builder.Configuration.GetSection("Twilio"));
builder.Services.AddSingleton<INotificationService, NotificationService>();

builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));
// AddHttpClient registers the service AND injects the HttpClient it needs to reach Telegram
builder.Services.AddHttpClient<INotificationService, NotificationService>();

builder.Services.AddSingleton<IGoldPriceCache, GoldPriceCache>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<GoldPriceBackgroundService>();

builder.Services.AddHostedService<StockMarketBackgroundService>();



var app = builder.Build();

//  Only use Swagger/UI in Development (NOT in Windows Service)
if (!WindowsServiceHelpers.IsWindowsService() && app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

//  Only enable MVC UI when NOT running as Windows Service
if (!WindowsServiceHelpers.IsWindowsService())
{
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapRazorPages();
}

app.Run();