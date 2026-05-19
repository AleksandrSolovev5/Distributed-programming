using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IDatabase _db;

    public SummaryModel( ILogger<SummaryModel> logger, IConnectionMultiplexer redis )
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public bool RankReady { get; set; }
    public double Rank { get; set; }
    public double Similarity { get; set; }

    public void OnGet( string id )
    {
        _logger.LogDebug( id );

        var rankVal = _db.StringGet( "RANK-" + id );
        if ( rankVal.HasValue )
        {
            RankReady = true;
            Rank = double.Parse( rankVal!, CultureInfo.InvariantCulture );
        }
        else
        {
            RankReady = false;
        }

        var simVal = _db.StringGet( "SIMILARITY-" + id );
        Similarity = simVal.HasValue
            ? double.Parse( simVal!, CultureInfo.InvariantCulture )
            : 0.0;
    }
}