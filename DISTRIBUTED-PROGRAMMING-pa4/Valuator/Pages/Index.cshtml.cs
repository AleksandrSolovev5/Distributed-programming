using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _db;
    private readonly RabbitMQ.Client.IConnection _mqConnection;

    private const string QueueName = "rank_tasks";

    private const string EventsExchangeName = "valuator.events";
    private const string SimilarityCalculatedRoutingKey = "similarity.calculated";

    public IndexModel(
        ILogger<IndexModel> logger,
        IConnectionMultiplexer redis,
        RabbitMQ.Client.IConnection mqConnection )
    {
        _logger = logger;
        _db = redis.GetDatabase();
        _mqConnection = mqConnection;
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

        string textKey = "TEXT-" + id;
        _db.StringSet( textKey, text );

        int similarity = CalculateSimilarity( text );
        _db.StringSet( "SIMILARITY-" + id, similarity );

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

        channel.ExchangeDeclare( // проверка что exchange существует
            exchange: EventsExchangeName,
            type: RabbitMQ.Client.ExchangeType.Direct, // RabbitMQ будет направлять сообщение по точному routing key
            durable: true, // exchange устойчивый
            autoDelete: false,// exchange не надо автоматичски удалять
            arguments: null );

        var message = new SimilarityCalculatedEvent
        {
            EventType = "SimilarityCalculated",
            Id = id,
            Similarity = similarity
        };

        string json = JsonSerializer.Serialize( message ); 
        byte[] body = Encoding.UTF8.GetBytes( json ); 

        var props = channel.CreateBasicProperties(); // Создает обект с дополнительными свойствами сообщения
        props.Persistent = true; // делает сообщение постоянным
        props.ContentType = "application/json";

        channel.BasicPublish(
            exchange: EventsExchangeName,
            routingKey: SimilarityCalculatedRoutingKey,
            mandatory: false, // если подходящей очереди нет, RabbitMQ не обязан возвращать сообщение отправителю
            basicProperties: props,
            body: body );
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

    private sealed class SimilarityCalculatedEvent
    {
        public string EventType { get; set; } = "";
        public string Id { get; set; } = "";
        public int Similarity { get; set; }
    }
}