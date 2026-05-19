using Microsoft.AspNetCore.SignalR;

namespace Valuator;

public class SummaryHub : Hub
{
    public Task Subscribe( string id )
    {
        if ( string.IsNullOrWhiteSpace( id ) )
            return Task.CompletedTask;

        return Groups.AddToGroupAsync( Context.ConnectionId, GetGroupName( id ) );
    }

    public static string GetGroupName( string id ) => $"summary-{id}";
}