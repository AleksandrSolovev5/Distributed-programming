using System.Globalization;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using StackExchange.Redis;

const string QueueName = "rank_tasks";

const string EventsExchangeName = "valuator.events";
const string RankCalculatedRoutingKey = "rank.calculated";

string redisConnectionString = GetRequiredEnv( "REDIS_CONNECTION" );

var redis = ConnectionMultiplexer.Connect( redisConnectionString );
var db = redis.GetDatabase();

using var connection = new ConnectionFactory
{
    HostName = GetEnv( "RABBITMQ_HOST", "localhost" ),
    Port = int.Parse( GetEnv( "RABBITMQ_PORT", "5673" ) ),
    UserName = GetRequiredEnv( "RABBITMQ_USER" ),
    Password = GetRequiredEnv( "RABBITMQ_PASSWORD" )
}.CreateConnection();

using var channel = connection.CreateModel();

channel.QueueDeclare(
    queue: QueueName,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null );

channel.ExchangeDeclare(
    exchange: EventsExchangeName,
    type: ExchangeType.Direct,
    durable: true,
    autoDelete: false,
    arguments: null );

channel.BasicQos( prefetchSize: 0, prefetchCount: 1, global: false );

Console.WriteLine( "RankCalculator запущен. Ожидаю задачи..." );

var consumer = new RabbitMQ.Client.Events.EventingBasicConsumer( channel );
consumer.Received += ( sender, ea ) =>
{
    try
    {
        string id = Encoding.UTF8.GetString( ea.Body.ToArray() ).Trim();

        if ( string.IsNullOrWhiteSpace( id ) )
            throw new Exception( "Пустой id" );

        string textKey = "TEXT-" + id;
        var textVal = db.StringGet( textKey );

        if ( !textVal.HasValue )
            throw new Exception( $"Не найден текст в Redis по ключу {textKey}" );

        string text = textVal!;

        double rank = CalculateRank( text );

        db.StringSet( "RANK-" + id, rank.ToString( CultureInfo.InvariantCulture ) );

        PublishRankCalculatedEvent( channel, id, rank );

        Console.WriteLine( $"OK id={id} rank={rank}" );

        channel.BasicAck( ea.DeliveryTag, multiple: false );
    }
    catch ( Exception ex )
    {
        Console.WriteLine( "ERROR: " + ex.Message );

        channel.BasicNack( ea.DeliveryTag, multiple: false, requeue: true );
    }
};

channel.BasicConsume(
    queue: QueueName,
    autoAck: false,
    consumerTag: "",
    noLocal: false,
    exclusive: false,
    arguments: null,
    consumer: consumer );

Console.ReadLine();

static string GetEnv( string key, string fallback )
{
    return Environment.GetEnvironmentVariable( key ) ?? fallback;
}

static string GetRequiredEnv( string key )
{
    return Environment.GetEnvironmentVariable( key )
        ?? throw new InvalidOperationException( $"Environment variable {key} is not configured." );
}

static double CalculateRank( string text )
{
    if ( text.Length == 0 )
        return 0.0;

    int nonLetters = 0;
    foreach ( char c in text )
        if ( !char.IsLetter( c ) )
            nonLetters++;

    return Math.Round( nonLetters / ( double )text.Length, 4 );
}

static void PublishRankCalculatedEvent( IModel channel, string id, double rank )
{
    var message = new
    {
        EventType = "RankCalculated",
        Id = id,
        Rank = rank
    };

    string json = JsonSerializer.Serialize( message );
    byte[] body = Encoding.UTF8.GetBytes( json );

    var props = channel.CreateBasicProperties();
    props.Persistent = true;
    props.ContentType = "application/json";

    channel.BasicPublish(
        exchange: EventsExchangeName,
        routingKey: RankCalculatedRoutingKey,
        mandatory: false,
        basicProperties: props,
        body: body );
}