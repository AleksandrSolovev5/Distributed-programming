using StackExchange.Redis;

namespace Valuator.Services;

public sealed class UserService
{
    private readonly IDatabase _db;

    public UserService( IConnectionMultiplexer redis )
    {
        _db = redis.GetDatabase();
    }

    public async Task<RegisterResult> RegisterAsync( string login, string password )
    {
        login = login.Trim();

        if ( string.IsNullOrWhiteSpace( login ) )
            return RegisterResult.Fail( "Введите логин" );

        if ( login.Length < 3 )
            return RegisterResult.Fail( "Логин должен быть не короче 3 символов" );

        if ( string.IsNullOrWhiteSpace( password ) )
            return RegisterResult.Fail( "Введите пароль" );

        if ( password.Length < 4 )
            return RegisterResult.Fail( "Пароль должен быть не короче 4 символов" );

        string normalizedLogin = NormalizeLogin( login );
        string userId = Guid.NewGuid().ToString();

        string loginKey = GetLoginKey( normalizedLogin );

        bool loginReserved = await _db.StringSetAsync(
            key: loginKey,
            value: userId,
            when: When.NotExists );

        if ( !loginReserved )
            return RegisterResult.Fail( "Пользователь с таким логином уже существует" );

        string passwordHash = PasswordHasher.HashPassword( password );

        await _db.StringSetAsync( GetUserLoginKey( userId ), login );
        await _db.StringSetAsync( GetUserPasswordHashKey( userId ), passwordHash );

        return RegisterResult.Ok( new UserAccount
        {
            Id = userId,
            Login = login
        } );
    }

    public async Task<UserAccount?> ValidateLoginAsync( string login, string password )
    {
        login = login.Trim();

        if ( string.IsNullOrWhiteSpace( login ) || string.IsNullOrWhiteSpace( password ) )
            return null;

        string normalizedLogin = NormalizeLogin( login );

        RedisValue userIdValue = await _db.StringGetAsync( GetLoginKey( normalizedLogin ) );

        if ( !userIdValue.HasValue )
            return null;

        string userId = userIdValue!;

        RedisValue savedLoginValue = await _db.StringGetAsync( GetUserLoginKey( userId ) );
        RedisValue passwordHashValue = await _db.StringGetAsync( GetUserPasswordHashKey( userId ) );

        if ( !savedLoginValue.HasValue || !passwordHashValue.HasValue )
            return null;

        bool passwordOk = PasswordHasher.VerifyPassword( password, passwordHashValue! );

        if ( !passwordOk )
            return null;

        return new UserAccount
        {
            Id = userId,
            Login = savedLoginValue!
        };
    }

    private static string NormalizeLogin( string login )
    {
        return login.Trim().ToLowerInvariant();
    }

    private static string GetLoginKey( string normalizedLogin )
    {
        return "USER-LOGIN-" + normalizedLogin;
    }

    private static string GetUserLoginKey( string userId )
    {
        return "USER-" + userId + "-LOGIN";
    }

    private static string GetUserPasswordHashKey( string userId )
    {
        return "USER-" + userId + "-PASSWORD-HASH";
    }
}

public sealed class RegisterResult
{
    public bool Success { get; private set; }
    public string ErrorMessage { get; private set; } = "";
    public UserAccount? User { get; private set; }

    public static RegisterResult Ok( UserAccount user )
    {
        return new RegisterResult
        {
            Success = true,
            User = user
        };
    }

    public static RegisterResult Fail( string errorMessage )
    {
        return new RegisterResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}