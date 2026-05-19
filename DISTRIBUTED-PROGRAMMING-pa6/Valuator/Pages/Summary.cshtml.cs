using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly RedisShardRouter _redisRouter;

    public SummaryModel( ILogger<SummaryModel> logger, RedisShardRouter redisRouter )
    {
        _logger = logger;
        _redisRouter = redisRouter;
    }

    public string Id { get; set; } = "";
    public string Region { get; set; } = "";
    public string Country { get; set; } = "";
    public bool RankReady { get; set; }
    public double Rank { get; set; }
    public double Similarity { get; set; }
    public string ErrorMessage { get; set; } = "";

    public void OnGet( string id )
    {
        if ( string.IsNullOrWhiteSpace( id ) )
        {
            ErrorMessage = "Не указан id текста";
            return;
        }

        Id = id;

        string? region = _redisRouter.LookupRegionById( id );
        if ( string.IsNullOrWhiteSpace( region ) )
        {
            ErrorMessage = $"Не найден сегмент для id={id}";
            return;
        }

        Region = region;

        Console.WriteLine( $"LOOKUP: {id}, {region}" );
        _logger.LogInformation( "LOOKUP: {Id}, {Region}", id, region );

        var shardDb = _redisRouter.GetShardDb( region );

        var countryVal = shardDb.StringGet( "COUNTRY-" + id );
        Country = countryVal.HasValue ? countryVal.ToString() : "";

        var rankVal = shardDb.StringGet( "RANK-" + id );
        if ( rankVal.HasValue )
        {
            RankReady = true;
            Rank = double.Parse( rankVal.ToString(), CultureInfo.InvariantCulture );
        }
        else
        {
            RankReady = false;
        }

        var simVal = shardDb.StringGet( "SIMILARITY-" + id );
        Similarity = simVal.HasValue
            ? double.Parse( simVal.ToString(), CultureInfo.InvariantCulture )
            : 0.0;
    }
}