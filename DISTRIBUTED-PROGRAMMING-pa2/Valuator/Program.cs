using StackExchange.Redis;

namespace Valuator;

public class Program
{
    public static void Main( string[] args )
    {
        var builder = WebApplication.CreateBuilder( args );

        builder.Services.AddRazorPages();

        var redisConnString = "localhost:6379";

        builder.Services.AddSingleton<IConnectionMultiplexer>( _ =>
            ConnectionMultiplexer.Connect( redisConnString ) );

        var app = builder.Build();

        if ( !app.Environment.IsDevelopment() )
        {
            app.UseExceptionHandler( "/Error" );
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.Run();
    }
}
