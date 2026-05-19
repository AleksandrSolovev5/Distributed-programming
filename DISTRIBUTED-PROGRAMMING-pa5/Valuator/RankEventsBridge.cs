using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Valuator;

public class RankEventsBridge : BackgroundService
{
    private const string EventsExchangeName = "app.events";
    private const string RankCalculatedRoutingKey = "rank.calculated";

    private readonly ILogger<RankEventsBridge> _logger;
    private readonly IHubContext<SummaryHub> _hubContext;

    public RankEventsBridge(
        ILogger<RankEventsBridge> logger,
        IHubContext<SummaryHub> hubContext )
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync( CancellationToken stoppingToken )
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            DispatchConsumersAsync = true
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(
            exchange: EventsExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null );

        string queueName = channel.QueueDeclare( // создание временной очереди
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null ).QueueName;

        channel.QueueBind( // Привязывает созданную очередь к app.events
            queue: queueName,
            exchange: EventsExchangeName,
            routingKey: RankCalculatedRoutingKey,
            arguments: null );

        _logger.LogInformation( "RankEventsBridge started. Queue: {QueueName}", queueName );

        var consumer = new AsyncEventingBasicConsumer( channel );
        consumer.Received += async ( _, ea ) =>
        {
            try
            {
                string json = Encoding.UTF8.GetString( ea.Body.ToArray() );
                using JsonDocument doc = JsonDocument.Parse( json );

                string eventType = doc.RootElement.GetProperty( "EventType" ).GetString() ?? string.Empty;// Достаёт поле EventType
                if ( !string.Equals( eventType, "RankCalculated", StringComparison.Ordinal ) )
                    return; // Если пришло сообщение не того типа

                string id = doc.RootElement.GetProperty( "Id" ).GetString() ?? string.Empty; 
                double rank = doc.RootElement.GetProperty( "Rank" ).GetDouble();

                if ( string.IsNullOrWhiteSpace( id ) )
                    return;

                var message = new RankCalculatedMessage
                {
                    Id = id,
                    Rank = rank
                };

                string groupName = SummaryHub.GetGroupName( id );

                await _hubContext.Clients.Group( groupName ) //Отправляет SignalR-сообщение всем клиентам из группы
                    .SendAsync( "RankCalculated", message, stoppingToken );

                _logger.LogInformation( // Пишет в лог, что сообщение успешно переслано в нужную SignalR-группу
                    "Forwarded RankCalculated to SignalR group {GroupName}: id={Id}, rank={Rank}",
                    groupName,
                    id,
                    rank );
            }
            catch ( Exception ex )
            {
                _logger.LogError( ex, "Error while forwarding RankCalculated event to SignalR clients" );
            }
        };

        string consumerTag = channel.BasicConsume( // Запускает чтение сообщений из  временной очереди
            queue: queueName,
            autoAck: true,
            consumer: consumer );

        try
        {
            await Task.Delay( Timeout.Infinite, stoppingToken ); // Держит фоновый сервис активным
        }
        catch ( OperationCanceledException )
        {
        }

        try
        {
            if ( channel.IsOpen ) // отменяет consumer перед завершением
                channel.BasicCancel( consumerTag );
        }
        catch ( Exception ex )
        {
            _logger.LogWarning( ex, "Failed to cancel RabbitMQ consumer cleanly" );
        }
    }

    private class RankCalculatedMessage
    {
        public string Id { get; set; } = string.Empty;
        public double Rank { get; set; }
    }
}