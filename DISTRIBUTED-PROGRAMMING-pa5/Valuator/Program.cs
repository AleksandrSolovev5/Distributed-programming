using RabbitMQ.Client;
using StackExchange.Redis;

namespace Valuator;

public class Program
{
    public static void Main( string[] args )
    {
        var builder = WebApplication.CreateBuilder( args );

        builder.Services.AddRazorPages();
        builder.Services.AddSignalR();

        // Redis
        builder.Services.AddSingleton<IConnectionMultiplexer>( _ =>
            ConnectionMultiplexer.Connect( "localhost:6379" ) );

        
        builder.Services.AddSingleton<IConnection>( _ =>
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672
            };

            return factory.CreateConnection();
        } );

      
        builder.Services.AddHostedService<RankEventsBridge>();

        var app = builder.Build();

        if ( !app.Environment.IsDevelopment() )
            app.UseExceptionHandler( "/Error" );

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        app.MapRazorPages();
        app.MapHub<SummaryHub>( "/summaryHub" );

        app.Run();
    }
}