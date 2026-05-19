using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _db;

    public IndexModel( ILogger<IndexModel> logger, IConnectionMultiplexer redis )
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public void OnGet()
    {
    }

    public IActionResult OnPost( string text )
    {
        if ( string.IsNullOrWhiteSpace( text ) )
        {
            ModelState.AddModelError( string.Empty, "Введите текст" );
            return Page();
        }

        _logger.LogDebug( text );

        string id = Guid.NewGuid().ToString();

        string rankKey = "RANK-" + id;
        double rank = CalculateRank( text );
        _db.StringSet( rankKey, rank.ToString( CultureInfo.InvariantCulture ) );

        string similarityKey = "SIMILARITY-" + id;
        int similarity = CalculateSimilarity( text );
        _db.StringSet( similarityKey, similarity );

        return Redirect( $"summary?id={id}" );
    }

    private static double CalculateRank( string text )
    {
        if ( text.Length == 0 )
            return 0.0;

        int nonLetters = 0;

        foreach ( char c in text )
        {
            if ( !char.IsLetter( c ) )
                nonLetters++;
        }

        return Math.Round( nonLetters / ( double )text.Length, 4 );
    }

    private int CalculateSimilarity( string text )
    {
        string hash = Sha256Hex( text );
        const string setKey = "DUPLICATES";           

        bool added = _db.SetAdd( setKey, hash );        

        return added ? 0 : 1;
    }


    private static string Sha256Hex( string text )
    {
        byte[] bytes = Encoding.UTF8.GetBytes( text );
        byte[] hash = SHA256.HashData( bytes );

        var sb = new StringBuilder( hash.Length * 2 );
        foreach ( byte b in hash )
            sb.Append( b.ToString( "x2" ) );

        return sb.ToString();
    }
}
