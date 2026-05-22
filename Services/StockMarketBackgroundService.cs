using GoldPriceTracker.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace GoldPriceTracker.Services
{
    public class StockMarketBackgroundService : BackgroundService
    {
        private readonly HttpClient _httpClient;
        private readonly INotificationService _notificationService;
        private readonly ILogger<StockMarketBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        // Track if we already sent the 1% alert today to avoid spamming
        private bool _niftyAlertSent = false;
        private bool _sensexAlertSent = false;

        // Add this field to the class to track which symbols have sent alerts today
        private readonly HashSet<string> _alertsSentToday = new HashSet<string>();

        public StockMarketBackgroundService(HttpClient httpClient, INotificationService notificationService, ILogger<StockMarketBackgroundService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _notificationService = notificationService;
            _logger = logger;
            _configuration = configuration;

            // Yahoo Finance requires a User-Agent header, otherwise it blocks the request
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }


        // it fetches the USD to INR exchange rate from Yahoo Finance and returns it as a decimal. 
        private async Task<decimal> GetUsdToInrRateAsync()
        {
            try
            {
                string url = "https://query1.finance.yahoo.com/v8/finance/chart/INR=X";
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<YahooFinanceResponse>(response);

                if (data?.Chart?.Result != null && 
                    data.Chart.Result.Count > 0 && 
                    data.Chart.Result[0]?.Meta != null)
                {
                    return data.Chart.Result[0]!.Meta!.RegularMarketPrice;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch INR exchange rate: {ex.Message}");
            }

            // Fallback rate just in case the API glitches
            return 83.50m;
        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stock Market Tracker Started.");

            int pollingMinutes = _configuration.GetValue<int>("PollingIntervalMinutes", 1);

            // Get live exchange rate for the initial startup
            decimal liveInrRate = await GetUsdToInrRateAsync();

            // Send initial startup prices
            await FetchAndNotifyAsync("^NSEI", "NIFTY 50", true);
            await FetchAndNotifyAsync("^BSESN", "SENSEX", true);
            await FetchCommodityAndNotifyAsync("GC=F", "GOLD (24K)", liveInrRate, true);
            await FetchCommodityAndNotifyAsync("SI=F", "SILVER", liveInrRate, true);
            await FetchAndNotifyAsync("CRUDEOIL", "CRUDE OIL", true);
            await FetchAndNotifyAsync("USDINR=X", "USD/INR", true);
            await FetchAndNotifyAsync("RELIANCE.NS", "RELIANCE", true);
            await FetchAndNotifyAsync("TCS.NS", "TCS", true);
            await FetchAndNotifyAsync("ICICI.NS", "ICICI", true);
            await FetchAndNotifyAsync("HDFCBANK.NS", "HDFC BANK", true);
            await FetchAndNotifyAsync("SBIN.NS", "SBIN", true);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Wait 1 minute before checking again
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                await FetchAndNotifyAsync("^NSEI", "NIFTY 50", false);
                await FetchAndNotifyAsync("^BSESN", "SENSEX", false);
                await FetchCommodityAndNotifyAsync("GC=F", "GOLD (24K)", liveInrRate, false);
                await FetchCommodityAndNotifyAsync("SI=F", "SILVER", liveInrRate, false);
                await FetchAndNotifyAsync("CRUDEOIL", "CRUDE OIL", false);
                await FetchAndNotifyAsync("USDINR=X", "USD/INR", false);
                await FetchAndNotifyAsync("RELIANCE.NS", "RELIANCE", false);
                await FetchAndNotifyAsync("TCS.NS", "TCS", false);
                await FetchAndNotifyAsync("ICICI.NS", "ICICI", false);
                await FetchAndNotifyAsync("HDFCBANK.NS", "HDFC BANK", false);
                await FetchAndNotifyAsync("SBIN.NS", "SBIN", false);

            }
        }

        private async Task FetchAndNotifyAsync(string symbol, string friendlyName, bool isStartup)
        {
            try
            {
                string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}";
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<YahooFinanceResponse>(response);

                if (data?.Chart?.Result != null && data.Chart.Result.Count > 0)
                {
                    var marketData = data.Chart.Result[0].Meta;
                    if (marketData != null)
                    {
                        decimal currentPrice = marketData.RegularMarketPrice;
                        decimal previousClose = marketData.PreviousClose;

                        // Calculate daily percentage change
                        decimal changePercentage = Math.Abs((currentPrice - previousClose) / previousClose) * 100m;
                        string direction = currentPrice >= previousClose ? "UP 🟢" : "DOWN 🔴";

                        if (isStartup)
                        {
                            string msg = $"📈 {friendlyName} Tracker Started!\nCurrent Price: ₹{currentPrice:N2}\nChange Today: {changePercentage:F2}% {direction}";
                            await _notificationService.SendNotificationAsync(msg);
                        }
                        else if (changePercentage >= 1.0m)
                        {
                            // Check if we already alerted for this specific index today
                            bool alreadySent = symbol == "^NSEI" ? _niftyAlertSent : _sensexAlertSent;

                            if (!alreadySent)
                            {
                                string alertMsg = $"🚨 {friendlyName} VOLATILITY ALERT 🚨\n\n{friendlyName} is {direction} by {changePercentage:F2}% today!\nCurrent Price: ₹{currentPrice:N2}\nPrevious Close: ₹{previousClose:N2}";
                                await _notificationService.SendNotificationAsync(alertMsg);

                                // Mark as sent so we don't spam the user every minute it stays above 1%
                                if (symbol == "^NSEI") _niftyAlertSent = true;
                                if (symbol == "^BSESN") _sensexAlertSent = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch {friendlyName} data: {ex.Message}");
            }
        }



        private async Task FetchCommodityAndNotifyAsync(string symbol, string friendlyName, decimal usdToInrRate, bool isStartup)
        {
            try
            {
                string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}";
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<YahooFinanceResponse>(response);

                if (data?.Chart?.Result != null && data.Chart.Result.Count > 0)
                {
                    var marketData = data.Chart.Result[0].Meta;

                    if (marketData != null)
                    {
                        // 1. Get raw USD price per Troy Ounce
                        decimal currentPriceUsdPerOunce = marketData.RegularMarketPrice;
                        decimal previousCloseUsdPerOunce = marketData.PreviousClose;

                        // 2. Convert to INR per 10 grams
                        decimal currentPriceInr10g = (currentPriceUsdPerOunce * usdToInrRate / 31.1035m) * 10m;
                        decimal previousCloseInr10g = (previousCloseUsdPerOunce * usdToInrRate / 31.1035m) * 10m;

                        // 3. Calculate percentage change
                        decimal changePercentage = Math.Abs((currentPriceInr10g - previousCloseInr10g) / previousCloseInr10g) * 100m;
                        string direction = currentPriceInr10g >= previousCloseInr10g ? "UP 🟢" : "DOWN 🔴";

                        if (isStartup)
                        {
                            string msg = $"📈 {friendlyName} Tracker Started!\nCurrent Price: ₹{currentPriceInr10g:N2} (per 10g)\nChange Today: {changePercentage:F2}% {direction}";
                            await _notificationService.SendNotificationAsync(msg);
                        }
                        else if (changePercentage >= 1.0m)
                        {
                            if (!_alertsSentToday.Contains(symbol))
                            {
                                string alertMsg = $"🚨 {friendlyName} VOLATILITY ALERT 🚨\n\n{friendlyName} is {direction} by {changePercentage:F2}% today!\nCurrent Price: ₹{currentPriceInr10g:N2} (per 10g)\nPrevious Close: ₹{previousCloseInr10g:N2} (per 10g)";
                                await _notificationService.SendNotificationAsync(alertMsg);

                                _alertsSentToday.Add(symbol);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch {friendlyName} data: {ex.Message}");
            }
        }
    }
}
