using StackExchange.Redis;

namespace Valuator;

public class Program
{
    public static void Main( string[] args )
    {
        var builder = WebApplication.CreateBuilder( args );

        builder.Services.AddRazorPages();

        // Redis
        builder.Services.AddSingleton<IConnectionMultiplexer>( _ =>
            ConnectionMultiplexer.Connect( "localhost:6379" ) );
        // RabbitMQ
        builder.Services.AddSingleton( _ =>
        {
            var factory = new RabbitMQ.Client.ConnectionFactory { HostName = "localhost", Port = 5672 };
            return factory.CreateConnection();
        } );

        var app = builder.Build();

        if ( !app.Environment.IsDevelopment() )
            app.UseExceptionHandler( "/Error" );

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.Run();
    }
}