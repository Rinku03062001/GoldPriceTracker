namespace GoldPriceTracker.Services;

public interface INotificationService
{
    Task SendNotificationAsync(string message);
}
