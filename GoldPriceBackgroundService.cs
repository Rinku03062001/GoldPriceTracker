using System.Text.Json;
using GoldPriceTracker.Models;

namespace GoldPriceTracker.Services;

public class GoldPriceBackgroundService : BackgroundService
{
    private readonly ILogger<GoldPriceBackgroundService> _logger;
    private readonly IGoldPriceCache _cache;
    private readonly HttpClient _httpClient;
    private readonly INotificationService _notificationService;
    private readonly int _pollingIntervalMinutes;
    private const string ApiUrl = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/xau.json";

    public GoldPriceBackgroundService(ILogger<GoldPriceBackgroundService> logger, IGoldPriceCache cache, HttpClient httpClient, INotificationService notificationService, IConfiguration configuration)
    {
        _logger = logger;
        _cache = cache;
        _httpClient = httpClient;
        _notificationService = notificationService;
        _pollingIntervalMinutes = configuration.GetValue<int>("PollingIntervalMinutes", 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Gold Price Background Service is starting with a polling interval of {PollingIntervalMinutes} minutes.", _pollingIntervalMinutes);

        //await _notificationService.SendNotificationAsync("TEST: The Windows Service just started successfully!");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchAndProcessGoldPriceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching gold price.");
            }

            // Wait for the configured polling interval
            await Task.Delay(TimeSpan.FromMinutes(_pollingIntervalMinutes), stoppingToken);
        }
    }

    private async Task FetchAndProcessGoldPriceAsync(CancellationToken stoppingToken)
    {
        const int maxRetries = 3;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                var response = await _httpClient.GetAsync(ApiUrl, stoppingToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(stoppingToken);
                var apiResponse = JsonSerializer.Deserialize<CurrencyApiResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apiResponse?.Xau != null && apiResponse.Xau.TryGetValue("inr", out decimal currentPrice) && currentPrice > 0)
                {
                    await ProcessAndCachePriceAsync(currentPrice);
                    return; // Success, exit retry loop
                }
                else
                {
                    _logger.LogWarning("Invalid response format or invalid price data received from API. Using last cached value if available.");
                    break; // No point in retrying if the API returns 200 OK but invalid payload
                }
            }
            catch (HttpRequestException ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "HTTP Request failed (Attempt {RetryCount} of {MaxRetries}).", retryCount, maxRetries);

                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Max retries reached. Failed to fetch gold price. Using last cached value if available.");
                    break;
                }

                // Exponential backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), stoppingToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse API response JSON. Using last cached value if available.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching gold price. Using last cached value if available.");
                break;
            }
            
        }
    }

    private async Task ProcessAndCachePriceAsync(decimal pricePerOunceInr)
    {
        // 1 troy ounce = 31.1035 grams
        // Calculate price for 10 grams: price_per_10g = (price_per_ounce / 31.1035) * 10
        decimal pricePer10g24K = (pricePerOunceInr / 31.1035m) * 10m;
        pricePer10g24K = Math.Round(pricePer10g24K, 2);

        // Convert 24K price to 22K price (assuming 22K is 91.67% pure)
        decimal purityFactor22K = 0.9167m;
        decimal pricePer10g22K = Math.Round(pricePer10g24K * purityFactor22K, 2);

        _logger.LogInformation("Gold Price Conversion | Original (Ounce, 24K): {PricePerOunce:N2} INR | Converted (10g, 24K): {PricePer10g24K:N2} INR | Converted (10g, 22K): {PricePer10g22K:N2} INR",
            pricePerOunceInr, pricePer10g24K, pricePer10g22K);
        
        var previousPriceInfo = _cache.GetCurrentPrice();

        if (previousPriceInfo != null)
        {
            decimal previousPricePer10g = previousPriceInfo.PricePer10gInr;
            decimal changePercentage = Math.Abs((pricePer10g22K - previousPricePer10g) / previousPricePer10g) * 100m;

            if (changePercentage >= 0.5m)
            {
                string direction = pricePer10g22K > previousPricePer10g ? "increased" : "decreased";

                // Log the specific required details
                _logger.LogInformation("Gold Price Alert | Old Price (10g, 22K): {OldPrice} | New Price (10g, 22K): {NewPrice} | Percentage Change: {ChangePercentage:F2}%",
                    previousPricePer10g, pricePer10g22K, changePercentage);

                string notificationMessage = $"Gold price (22K, 10g) has {direction} by {changePercentage:F2}%. Old Price: {previousPricePer10g}, New Price: {pricePer10g22K}";

                await _notificationService.SendNotificationAsync(notificationMessage);
            }
        }
        else
        {
            _logger.LogInformation("Initial Gold price (10g, 22K) cached: {Price}", pricePer10g22K);
            
            string welcomeMessage = $"Gold Price Tracker Started! Current 24K Gold Price (10g) is: ₹{pricePer10g24K:N2}";
            await _notificationService.SendNotificationAsync(welcomeMessage);
        }

        _cache.SetCurrentPrice(pricePer10g22K);
    }
}
