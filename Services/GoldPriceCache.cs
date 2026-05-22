using GoldPriceTracker.Models;

namespace GoldPriceTracker.Services;

public interface IGoldPriceCache
{
    GoldPriceInfo? GetCurrentPrice();
    void SetCurrentPrice(decimal price);
}

public class GoldPriceCache : IGoldPriceCache
{
    private GoldPriceInfo? _currentPrice;
    private readonly object _lock = new object();

    public GoldPriceInfo? GetCurrentPrice()
    {
        lock (_lock)
        {
            return _currentPrice;
        }
    }

    public void SetCurrentPrice(decimal pricePer10g)
    {
        lock (_lock)
        {
            decimal prevPrice = _currentPrice?.PreviousPricePer10gInr ?? pricePer10g;
            decimal changePercentage = 0;

            if (_currentPrice != null)
            {
                prevPrice = _currentPrice.PricePer10gInr;
                if (prevPrice > 0)
                {
                    changePercentage = Math.Abs((pricePer10g - prevPrice) / prevPrice) * 100m;
                }
            }

            _currentPrice = new GoldPriceInfo
            {
                PricePer10gInr = pricePer10g,
                PreviousPricePer10gInr = prevPrice,
                ChangePercentage = changePercentage,
                LastUpdated = DateTime.UtcNow
            };
        }
    }
}
