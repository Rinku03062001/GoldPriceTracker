namespace GoldPriceTracker.Models;

public class CurrencyApiResponse
{
    public string Date { get; set; } = string.Empty;
    public Dictionary<string, decimal>? Xau { get; set; }
}

public class GoldPriceInfo
{
public decimal PricePer10gInr { get; set; } // Now represents 22K price
public decimal PreviousPricePer10gInr { get; set; }
public decimal ChangePercentage { get; set; }
public DateTime LastUpdated { get; set; }
}
