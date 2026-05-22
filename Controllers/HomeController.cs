using Microsoft.AspNetCore.Mvc;
using GoldPriceTracker.Services;
using GoldPriceTracker.Models;

namespace GoldPriceTracker.Controllers;

public class HomeController : Controller
{
    private readonly IGoldPriceCache _cache;

    public HomeController(IGoldPriceCache cache)
    {
        _cache = cache;
    }

    public IActionResult Index()
    {
        var priceInfo = _cache.GetCurrentPrice();
        if (priceInfo != null)
        {
            var viewModel = new GoldPriceViewModel
            {
                PricePer10gInr = priceInfo.PricePer10gInr,
                LastUpdated = priceInfo.LastUpdated,
                ChangePercentage = priceInfo.ChangePercentage,
                Status = "UNCHANGED"
            };

            if (priceInfo.PreviousPricePer10gInr > 0 && priceInfo.PricePer10gInr != priceInfo.PreviousPricePer10gInr)
            {
                viewModel.Status = priceInfo.PricePer10gInr > priceInfo.PreviousPricePer10gInr ? "UP" : "DOWN";
            }
            ViewData["Title"] = "Gold Price (22K, 10g)"; // Update title for 22K
            return View(viewModel);
        }
        return View(new GoldPriceViewModel()); // Return an empty model if no price yet
    }
}
