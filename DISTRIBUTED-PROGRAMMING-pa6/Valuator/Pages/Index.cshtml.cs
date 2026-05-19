using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using Valuator.Services;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly RedisShardRouter _redisRouter;
    private readonly RabbitMQ.Client.IConnection _mqConnection;

    private const string QueueName = "rank_tasks";

    private const string EventsExchangeName = "valuator.events";
    private const string SimilarityCalculatedRoutingKey = "similarity.calculated";

    public IndexModel(
        ILogger<IndexModel> logger,
        RedisShardRouter redisRouter,
        RabbitMQ.Client.IConnection mqConnection )
    {
        _logger = logger;
        _redisRouter = redisRouter;
        _mqConnection = mqConnection;
    }

    public void OnGet()
    {
    }

    public IActionResult OnPost( string text, string country )
    {
        if ( string.IsNullOrWhiteSpace( country ) )
        {
            ModelState.AddModelError( string.Empty, "Выберите страну" );
            return Page();
        }

        if ( string.IsNullOrWhiteSpace( text ) )
        {
            ModelState.AddModelError( string.Empty, "Введите текст" );
            return Page();
        }

        string region;

        try
        {
            region = _redisRouter.GetRegionByCountry( country );
        }
        catch ( Exception ex )
        {
            ModelState.AddModelError( string.Empty, ex.Message );
            return Page();
        }

        _logger.LogDebug( "Text: {Text}", text );
        _logger.LogDebug( "Country: {Country}, Region: {Region}", country, region );

        string id = Guid.NewGuid().ToString();

        _redisRouter.SaveShardMap( id, region );

        IDatabase shardDb = _redisRouter.GetShardDb( region );

        shardDb.StringSet( "TEXT-" + id, text );
        shardDb.StringSet( "COUNTRY-" + id, country );

        int similarity = CalculateSimilarity( shardDb, text );
        shardDb.StringSet( "SIMILARITY-" + id, similarity );

        PublishSimilarityCalculatedEvent( id, similarity );
        PublishRankTask( id );

        return Redirect( $"summary?id={id}" );
    }

    private void PublishRankTask( string id )
    {
        string payload = id;
        byte[] body = Encoding.UTF8.GetBytes( payload );

        using var channel = _mqConnection.CreateModel();

        channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null );

        var props = channel.CreateBasicProperties();
        props.Persistent = true;

        channel.BasicPublish(
            exchange: "",
            routingKey: QueueName,
            mandatory: false,
            basicProperties: props,
            body: body );
    }

    private void PublishSimilarityCalculatedEvent( string id, int similarity )
    {
        using var channel = _mqConnection.CreateModel();

        channel.ExchangeDeclare(
            exchange: EventsExchangeName,
            type: RabbitMQ.Client.ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null );

        var message = new SimilarityCalculatedEvent
        {
            EventType = "SimilarityCalculated",
            Id = id,
            Similarity = similarity
        };

        string json = JsonSerializer.Serialize( message );
        byte[] body = Encoding.UTF8.GetBytes( json );

        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.ContentType = "application/json";

        channel.BasicPublish(
            exchange: EventsExchangeName,
            routingKey: SimilarityCalculatedRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body );
    }

    private static int CalculateSimilarity( IDatabase shardDb, string text )
    {
        string hash = Sha256Hex( text );
        const string setKey = "DUPLICATES";

        bool added = shardDb.SetAdd( setKey, hash );
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

    private sealed class SimilarityCalculatedEvent
    {
        public string EventType { get; set; } = "";
        public string Id { get; set; } = "";
        public int Similarity { get; set; }
    }
}