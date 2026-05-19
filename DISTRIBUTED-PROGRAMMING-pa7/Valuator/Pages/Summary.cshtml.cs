using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

[Authorize]
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IDatabase _db;

    public SummaryModel( ILogger<SummaryModel> logger, IConnectionMultiplexer redis )
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public string Id { get; set; } = "";
    public bool RankReady { get; set; }
    public double Rank { get; set; }
    public double Similarity { get; set; }

    public IActionResult OnGet( string id )
    {
        if ( string.IsNullOrWhiteSpace( id ) )
            return NotFound();

        _logger.LogDebug( id );

        string? currentUserId = User.FindFirstValue( ClaimTypes.NameIdentifier );

        if ( string.IsNullOrWhiteSpace( currentUserId ) )
            return Challenge();

        RedisValue authorValue = _db.StringGet( "AUTHOR-" + id );

        // Если результата нет или он был создан старой версией без автора,
        // то показывать его нельзя.
        if ( !authorValue.HasValue )
            return NotFound();

        string authorId = authorValue!;

        // PA7: авторизация.
        // Summary может смотреть только автор текста.
        if ( authorId != currentUserId )
            return Forbid();

        Id = id;

        RedisValue rankVal = _db.StringGet( "RANK-" + id );
        if ( rankVal.HasValue )
        {
            RankReady = true;
            Rank = double.Parse( rankVal!, CultureInfo.InvariantCulture );
        }
        else
        {
            RankReady = false;
        }

        RedisValue simVal = _db.StringGet( "SIMILARITY-" + id );
        Similarity = simVal.HasValue
            ? double.Parse( simVal!, CultureInfo.InvariantCulture )
            : 0.0;

        return Page();
    }
}