namespace GoldPriceTracker.Models
{
    public class TelegramSettings
    {
        public bool IsEnabled { get; set; }
        public string BotToken { get; set; } = string.Empty;

        // A list so you can message multiple friends/users for free
        public List<string> ChatIds { get; set; } = new List<string>();
    }
}
