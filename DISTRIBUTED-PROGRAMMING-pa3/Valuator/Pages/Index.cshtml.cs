using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _db;

    private readonly RabbitMQ.Client.IConnection _mqConnection; // _mqConnection — Singleton

    private const string QueueName = "rank_tasks";

    public IndexModel(
        ILogger<IndexModel> logger,
        IConnectionMultiplexer redis,
        RabbitMQ.Client.IConnection mqConnection )
    {
        _logger = logger;
        _db = redis.GetDatabase();
        _mqConnection = mqConnection;
    }

    public void OnGet() { }

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

        PublishRankTask( id );

        return Redirect( $"summary?id={id}" );
    }

    private void PublishRankTask( string id)
    {
        string payload = id;
        byte[] body = Encoding.UTF8.GetBytes( payload ); // превращаем строку в массив байтов

        using var channel = _mqConnection.CreateModel(); // channel создаём на каждый запрос

        channel.QueueDeclare(
            queue: QueueName,
            durable: true, // очередь переживает перезапуск RabbitMQ
            exclusive: false, // очередь не привязана к одному подключению
            autoDelete: false, // очередь не удаляется автоматически
            arguments: null );

        var props = channel.CreateBasicProperties();
        props.Persistent = true; // сохранять сообщение на диск

        channel.BasicPublish(
            exchange: "",
            routingKey: QueueName,
            mandatory: false,
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
}