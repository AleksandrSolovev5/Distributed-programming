using System.Globalization;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

const string QueueName = "rank_tasks";

const string EventsExchangeName = "valuator.events";
const string RankCalculatedRoutingKey = "rank.calculated";

const string ShardMapKey = "SHARDMAP";

var mainRedis = ConnectionMultiplexer.Connect( GetEnv( "DB_MAIN", "localhost:6000" ) );

var shardRedis = new Dictionary<string, IConnectionMultiplexer>
{
    [ "RU" ] = ConnectionMultiplexer.Connect( GetEnv( "DB_RU", "localhost:6001" ) ),
    [ "EU" ] = ConnectionMultiplexer.Connect( GetEnv( "DB_EU", "localhost:6002" ) ),
    [ "ASIA" ] = ConnectionMultiplexer.Connect( GetEnv( "DB_ASIA", "localhost:6003" ) )
};

using var connection = new RabbitMQ.Client.ConnectionFactory
{
    HostName = "localhost",
    Port = 5672
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
    type: RabbitMQ.Client.ExchangeType.Direct,
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

        string region = LookupRegionById( mainRedis, id );

        Console.WriteLine( $"LOOKUP: {id}, {region}" );

        IDatabase shardDb = GetShardDb( shardRedis, region );

        string textKey = "TEXT-" + id;
        var textVal = shardDb.StringGet( textKey );

        if ( !textVal.HasValue )
            throw new Exception( $"Не найден текст в Redis-сегменте {region} по ключу {textKey}" );

        string text = textVal.ToString();

        double rank = CalculateRank( text );

        shardDb.StringSet( "RANK-" + id, rank.ToString( CultureInfo.InvariantCulture ) );

        PublishRankCalculatedEvent( channel, id, rank );

        Console.WriteLine( $"OK id={id} region={region} rank={rank}" );

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

mainRedis.Dispose();

foreach ( var redis in shardRedis.Values )
    redis.Dispose();

static string LookupRegionById( IConnectionMultiplexer mainRedis, string id )
{
    var mainDb = mainRedis.GetDatabase();
    var regionVal = mainDb.HashGet( ShardMapKey, id );

    if ( !regionVal.HasValue )
        throw new Exception( $"Не найден ShardKey в DB_MAIN для id={id}" );

    return regionVal.ToString();
}

static IDatabase GetShardDb( Dictionary<string, IConnectionMultiplexer> shardRedis, string region )
{
    if ( !shardRedis.TryGetValue( region, out var redis ) )
        throw new Exception( $"Неизвестный регион: {region}" );

    return redis.GetDatabase();
}

static string GetEnv( string name, string defaultValue )
{
    string? value = Environment.GetEnvironmentVariable( name );
    return string.IsNullOrWhiteSpace( value ) ? defaultValue : value;
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

static void PublishRankCalculatedEvent( RabbitMQ.Client.IModel channel, string id, double rank )
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