namespace GoldPriceTracker.Models;

public class GoldPriceViewModel
{
    public decimal PricePer10gInr { get; set; }
    public DateTime LastUpdated { get; set; }
    public decimal ChangePercentage { get; set; }
    public string Status { get; set; } = "N/A";
}
