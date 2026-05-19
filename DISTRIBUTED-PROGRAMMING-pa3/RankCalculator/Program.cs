using System.Globalization;
using System.Text;
using StackExchange.Redis;

const string QueueName = "rank_tasks";

var redis = ConnectionMultiplexer.Connect( "localhost:6379" );
var db = redis.GetDatabase();

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

// чтобы несколько RankCalculator делили задачи равномерно
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

        Console.WriteLine( $"OK id={id} rank={rank}" );

        // Подтверждаем только после успешной записи rank
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

Console.WriteLine( "Нажми Enter для выхода." );
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