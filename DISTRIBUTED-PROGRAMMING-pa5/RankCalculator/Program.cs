using System.Globalization;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

const string QueueName = "rank_tasks";

const string EventsExchangeName = "app.events";
const string RankCalculatedRoutingKey = "rank.calculated";

var redis = ConnectionMultiplexer.Connect( "localhost:6379" );
var db = redis.GetDatabase();

using var connection = new ConnectionFactory
{
    HostName = "localhost",
    Port = 5672,
    DispatchConsumersAsync = true // разрешает асинхронную обработку сообщений
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

// чтобы несколько RankCalculator делили задачи равномерно
channel.BasicQos( prefetchSize: 0, prefetchCount: 1, global: false );

Console.WriteLine( "RankCalculator запущен. Ожидаю задачи..." );

var consumer = new AsyncEventingBasicConsumer( channel ); // consumer, который умеет обрабатывать сообщения асинхронно
consumer.Received += async ( _, ea ) => // обработчик должен быть асинхронным
{
    try
    {
        string id = Encoding.UTF8.GetString( ea.Body.ToArray() ).Trim();

        if ( string.IsNullOrWhiteSpace( id ) )
            throw new Exception( "Пустой id" );

        TimeSpan interval = TimeSpan.FromSeconds( Random.Shared.Next( 3, 16 ) ); // добавление задержки
        Console.WriteLine( $"Waiting {interval}" );
        await Task.Delay( interval );

        string textKey = "TEXT-" + id;
        var textVal = db.StringGet( textKey );

        if ( !textVal.HasValue )
            throw new Exception( $"Не найден текст в Redis по ключу {textKey}" );

        string text = textVal!;

        double rank = CalculateRank( text );

        db.StringSet( "RANK-" + id, rank.ToString( CultureInfo.InvariantCulture ) );

        PublishRankCalculatedEvent( channel, id, rank ); // публикация события

        Console.WriteLine( $"OK id={id} rank={rank}" );

        // Подтверждаем только после успешной записи rank и публикации события
        channel.BasicAck( ea.DeliveryTag, multiple: false );
    }
    catch ( Exception ex )
    {
        Console.WriteLine( "ERROR: " + ex.Message );

        // Возвращаем задачу в очередь, чтобы попробовать позже
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
        mandatory: false, // не требовать обязательной доставки в очередь
        basicProperties: props,
        body: body );
}