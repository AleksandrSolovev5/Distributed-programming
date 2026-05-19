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

    public double Rank { get; set; }
    public double Similarity { get; set; }

    public void OnGet( string id )
    {
        _logger.LogDebug( id );

        string rankKey = "RANK-" + id;
        string similarityKey = "SIMILARITY-" + id;

        var rankValue = _db.StringGet( rankKey );             
        var similarityValue = _db.StringGet( similarityKey ); 

        // Если ключа нет - 0 
        if ( !rankValue.HasValue )
            Rank = 0.0;
        else
            Rank = double.Parse( rankValue!, CultureInfo.InvariantCulture ); 

        if ( !similarityValue.HasValue )
            Similarity = 0.0;
        else
            Similarity = double.Parse( similarityValue!, CultureInfo.InvariantCulture );
    }
}
// как коннектится 
// не хранить доп данные в БД