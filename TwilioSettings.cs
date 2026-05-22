namespace GoldPriceTracker.Models;

public class TwilioSettings
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromPhoneNumber { get; set; } = string.Empty;
    public string ToPhoneNumber { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = false;
}
