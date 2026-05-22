using Microsoft.AspNetCore.Mvc;
using GoldPriceTracker.Services;

namespace GoldPriceTracker.Controllers;

[ApiController]
[Route("[controller]")]
public class GoldPriceController : ControllerBase
{
    private readonly IGoldPriceCache _cache;

    public GoldPriceController(IGoldPriceCache cache)
    {
        _cache = cache;
    }

    [HttpGet("/gold-price")]
    public IActionResult GetGoldPrice()
    {
        var priceInfo = _cache.GetCurrentPrice();
        if (priceInfo == null)
        {
            return StatusCode(503, new { message = "Gold price is not available yet. Please try again later." });
        }

        return Ok(new 
        {
            price_per_10g_inr_22k = Math.Round(priceInfo.PricePer10gInr, 2),
            last_updated = priceInfo.LastUpdated,
            change_percentage = Math.Round(priceInfo.ChangePercentage, 2)
        });
    }
}