//using GoldPriceTracker.Models;
//using Microsoft.Extensions.Options;
//using Twilio;
//using Twilio.Rest.Api.V2010.Account;
//using Twilio.Types;

//namespace GoldPriceTracker.Services;

//public class NotificationService : INotificationService
//{
//    private readonly ILogger<NotificationService> _logger;
//    private readonly TwilioSettings _twilioSettings;

//    public NotificationService(ILogger<NotificationService> logger, IOptions<TwilioSettings> twilioSettings)
//    {
//        _logger = logger;
//        _twilioSettings = twilioSettings.Value;
//    }

//    public async Task SendNotificationAsync(string message)
//    {
//        // Option 1: Console log (always logging for now as requested)
//        _logger.LogInformation("CONSOLE NOTIFICATION: {Message}", message);

//        // Option 2: SMS using Twilio API
//        if (_twilioSettings.IsEnabled && !string.IsNullOrEmpty(_twilioSettings.AccountSid) && !string.IsNullOrEmpty(_twilioSettings.AuthToken))
//        {
//            try
//            {
//                TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

//                var messageResource = await MessageResource.CreateAsync(
//                    body: message,
//                    from: new PhoneNumber(_twilioSettings.FromPhoneNumber),
//                    to: new PhoneNumber(_twilioSettings.ToPhoneNumber)
//                );

//                _logger.LogInformation("Twilio SMS sent successfully. SID: {Sid}", messageResource.Sid);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to send Twilio SMS notification.");
//            }
//        }
//        else
//        {
//            _logger.LogInformation("Twilio notification is disabled or not configured properly. Skipping SMS.");
//        }
//    }
//}




// for telegram messaging
using System.Text;
using System.Text.Json;
using GoldPriceTracker.Models;
using Microsoft.Extensions.Options;

namespace GoldPriceTracker.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly TelegramSettings _telegramSettings;

    public NotificationService(ILogger<NotificationService> logger, HttpClient httpClient, IOptions<TelegramSettings> telegramSettings)
    {
        _logger = logger;
        _httpClient = httpClient;
        _telegramSettings = telegramSettings.Value;
    }

    public async Task SendNotificationAsync(string message)
    {
        _logger.LogInformation("CONSOLE NOTIFICATION: {Message}", message);

        if (_telegramSettings.IsEnabled && !string.IsNullOrEmpty(_telegramSettings.BotToken) && _telegramSettings.ChatIds.Any())
        {
            // Loop through every Chat ID in your appsettings.json
            foreach (var chatId in _telegramSettings.ChatIds)
            {
                try
                {
                    string url = $"https://api.telegram.org/bot{_telegramSettings.BotToken}/sendMessage";

                    var payload = new
                    {
                        chat_id = chatId,
                        text = message
                    };

                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Telegram message sent successfully to Chat ID: {ChatId}", chatId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send Telegram message. API returned {StatusCode} for Chat ID: {ChatId}", response.StatusCode, chatId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while sending Telegram notification to Chat ID: {ChatId}", chatId);
                }
            }
        }
        else
        {
            _logger.LogInformation("Telegram notification is disabled or missing credentials in appsettings.json. Skipping.");
        }
    }
}
