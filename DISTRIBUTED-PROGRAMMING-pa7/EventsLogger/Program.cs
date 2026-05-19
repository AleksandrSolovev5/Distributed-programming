using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

const string EventsExchangeName = "valuator.events";
const string RankCalculatedRoutingKey = "rank.calculated";
const string SimilarityCalculatedRoutingKey = "similarity.calculated";

string instanceName = "EventsLogger-" + Environment.ProcessId;

using var connection = new ConnectionFactory
{
    HostName = GetEnv( "RABBITMQ_HOST", "localhost" ),
    Port = int.Parse( GetEnv( "RABBITMQ_PORT", "5673" ) ),
    UserName = GetRequiredEnv( "RABBITMQ_USER" ),
    Password = GetRequiredEnv( "RABBITMQ_PASSWORD" )
}.CreateConnection();

using var channel = connection.CreateModel();

channel.ExchangeDeclare(
    exchange: EventsExchangeName,
    type: ExchangeType.Direct,
    durable: true,
    autoDelete: false,
    arguments: null );

// отдельная очередь для каждого экземпляра логгера
string queueName = channel.QueueDeclare(
    queue: "", // автоматически генерировать уникальное имя
    durable: false,
    exclusive: true, // этой очередью пользуется только текущее соединение
    autoDelete: true,
    arguments: null ).QueueName;

channel.QueueBind( // Привязка очереди к событию RankCalculated
    queue: queueName,
    exchange: EventsExchangeName,
    routingKey: RankCalculatedRoutingKey,
    arguments: null );

channel.QueueBind( // Привязка очереди к событию SimilarityCalculated
    queue: queueName,
    exchange: EventsExchangeName,
    routingKey: SimilarityCalculatedRoutingKey,
    arguments: null );

Console.WriteLine( $"{instanceName} запущен. Очередь: {queueName}" );
Console.WriteLine( "Ожидаю события..." );

var consumer = new RabbitMQ.Client.Events.EventingBasicConsumer( channel );
consumer.Received += ( sender, ea ) => // когда пришло сообщение, выполни этот блок кода
{
    try
    {
        string json = Encoding.UTF8.GetString( ea.Body.ToArray() );
        using JsonDocument doc = JsonDocument.Parse( json );

        string eventType = doc.RootElement.GetProperty( "EventType" ).GetString() ?? "";

        if ( eventType == "RankCalculated" )
        {
            string id = doc.RootElement.GetProperty( "Id" ).GetString() ?? "";
            double rank = doc.RootElement.GetProperty( "Rank" ).GetDouble();

            Console.WriteLine( $"[{instanceName}] [RankCalculated] id={id} rank={rank}" );
        }
        else if ( eventType == "SimilarityCalculated" )
        {
            string id = doc.RootElement.GetProperty( "Id" ).GetString() ?? "";
            int similarity = doc.RootElement.GetProperty( "Similarity" ).GetInt32();

            Console.WriteLine( $"[{instanceName}] [SimilarityCalculated] id={id} similarity={similarity}" );
        }
        else
        {
            Console.WriteLine( $"[{instanceName}] [UnknownEvent] {json}" );
        }
    }
    catch ( Exception ex )
    {
        Console.WriteLine( $"[{instanceName}] ERROR: {ex.Message}" );
    }
};

channel.BasicConsume(
    queue: queueName,
    autoAck: true, // как только сообщение доставлено логгеру, RabbitMQ считает его подтверждённым автоматически
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