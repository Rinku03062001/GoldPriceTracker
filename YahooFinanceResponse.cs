using System.Text.Json.Serialization;
namespace GoldPriceTracker.Models
{
    public class YahooFinanceResponse
    {
        [JsonPropertyName("chart")]
        public ChartData? Chart { get; set; }
    }

    public class ChartData
    {
        [JsonPropertyName("result")]
        public List<ResultData>? Result { get; set; }
    }

    public class ResultData
    {
        [JsonPropertyName("meta")]
        public MetaData? Meta { get; set; }
    }

    public class  MetaData
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("regularMarketPrice")]
        public decimal RegularMarketPrice { get; set; }

        [JsonPropertyName("previousClose")]
        public decimal PreviousClose { get; set; }
    }

}
