using Microsoft.AspNetCore.Authentication.Cookies;
using RabbitMQ.Client;
using StackExchange.Redis;
using Valuator.Services;

namespace Valuator;

public class Program
{
    public static void Main( string[] args )
    {
        var builder = WebApplication.CreateBuilder( args );

        builder.Services.AddRazorPages();

        builder.Services.AddAuthentication( CookieAuthenticationDefaults.AuthenticationScheme )
            .AddCookie( options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/AccessDenied";
            } );

        builder.Services.AddAuthorization();

        // Redis: подключение из appsettings.Development.json.
        // Это часть Задания 1: пароль Redis не хранится в коде.
        builder.Services.AddSingleton<IConnectionMultiplexer>( _ =>
        {
            string redisConnectionString = builder.Configuration[ "Redis:ConnectionString" ]
                ?? throw new InvalidOperationException( "Redis connection string is not configured." );

            return ConnectionMultiplexer.Connect( redisConnectionString );
        } );

        // RabbitMQ: подключение из appsettings.Development.json.
        // Это часть Задания 1: логин и пароль RabbitMQ не хранятся в коде.
        builder.Services.AddSingleton<IConnection>( _ =>
        {
            string hostName = builder.Configuration[ "RabbitMQ:HostName" ] ?? "localhost";
            int port = builder.Configuration.GetValue<int?>( "RabbitMQ:Port" ) ?? 5673;

            string userName = builder.Configuration[ "RabbitMQ:UserName" ]
                ?? throw new InvalidOperationException( "RabbitMQ user name is not configured." );

            string password = builder.Configuration[ "RabbitMQ:Password" ]
                ?? throw new InvalidOperationException( "RabbitMQ password is not configured." );

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password
            };

            return factory.CreateConnection();
        } );

        builder.Services.AddScoped<UserService>();

        var app = builder.Build();

        if ( !app.Environment.IsDevelopment() )
        {
            app.UseExceptionHandler( "/Error" );
        }

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}