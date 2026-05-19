using StackExchange.Redis;

namespace Valuator.Services;

public class RedisShardRouter : IDisposable
{
    private const string ShardMapKey = "SHARDMAP";

    private readonly IConnectionMultiplexer _mainRedis;
    private readonly Dictionary<string, IConnectionMultiplexer> _shardRedis;

    public RedisShardRouter()
    {
        _mainRedis = ConnectionMultiplexer.Connect( GetEnv( "DB_MAIN", "localhost:6000" ) );

        _shardRedis = new Dictionary<string, IConnectionMultiplexer>
        {
            [ "RU" ] = ConnectionMultiplexer.Connect( GetEnv( "DB_RU", "localhost:6001" ) ),
            [ "EU" ] = ConnectionMultiplexer.Connect( GetEnv( "DB_EU", "localhost:6002" ) ),
            [ "ASIA" ] = ConnectionMultiplexer.Connect( GetEnv( "DB_ASIA", "localhost:6003" ) )
        };
    }

    public string GetRegionByCountry( string country )
    {
        return country switch
        {
            "Russia" => "RU",
            "France" => "EU",
            "Germany" => "EU",
            "UAE" => "ASIA",
            "India" => "ASIA",
            _ => throw new ArgumentException( $"Неизвестная страна: {country}" )
        };
    }

    public IDatabase GetMainDb()
    {
        return _mainRedis.GetDatabase();
    }

    public IDatabase GetShardDb( string region )
    {
        if ( !_shardRedis.TryGetValue( region, out var redis ) )
            throw new ArgumentException( $"Неизвестный регион: {region}" );

        return redis.GetDatabase();
    }

    public void SaveShardMap( string id, string region ) 
    {
        GetMainDb().HashSet( ShardMapKey, id, region );
    }

    public string? LookupRegionById( string id )
    {
        var value = GetMainDb().HashGet( ShardMapKey, id );

        if ( !value.HasValue )
            return null;

        return value.ToString();
    }

    public IDatabase GetShardDbByTextId( string id, out string region )
    {
        region = LookupRegionById( id )
            ?? throw new Exception( $"Не найден ShardKey для id={id}" );

        return GetShardDb( region );
    }

    private static string GetEnv( string name, string defaultValue )
    {
        string? value = Environment.GetEnvironmentVariable( name );
        return string.IsNullOrWhiteSpace( value ) ? defaultValue : value;
    }

    public void Dispose() 
    {
        _mainRedis.Dispose();

        foreach ( var redis in _shardRedis.Values )
            redis.Dispose();
    }
}