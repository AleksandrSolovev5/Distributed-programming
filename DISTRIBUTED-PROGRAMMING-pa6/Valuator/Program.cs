using Valuator.Services;

namespace Valuator;

public class Program
{
    public static void Main( string[] args )
    {
        var builder = WebApplication.CreateBuilder( args );

        builder.Services.AddRazorPages();

        builder.Services.AddSingleton<RedisShardRouter>();

        builder.Services.AddSingleton( _ =>
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672
            };

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