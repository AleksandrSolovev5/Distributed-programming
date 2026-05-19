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

    public string RequestId { get; private set; } = string.Empty;
    public bool RankReady { get; private set; }
    public double Rank { get; private set; }
    public int Similarity { get; private set; }

    public void OnGet( string id )
    {
        RequestId = id ?? string.Empty;
        _logger.LogDebug( RequestId );

        if ( string.IsNullOrWhiteSpace( RequestId ) )
        {
            RankReady = false;
            Similarity = 0;
            return;
        }

        var rankVal = _db.StringGet( "RANK-" + RequestId );
        if ( rankVal.HasValue )
        {
            RankReady = true;
            Rank = double.Parse( rankVal!, CultureInfo.InvariantCulture );
        }
        else
        {
            RankReady = false;
        }

        var simVal = _db.StringGet( "SIMILARITY-" + RequestId );
        Similarity = simVal.HasValue
            ? int.Parse( simVal! )
            : 0;
    }
}