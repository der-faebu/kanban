using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kanban.Hubs;

// BoardView.razor's HubConnection is opened from server-side Blazor Server component code,
// which makes it a loopback client connecting to this same process rather than a
// browser-authenticated request — the Identity cookie scheme (this app's default) never
// applies to it. It authenticates instead with a short-lived JWT minted by IJwtTokenService.
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BoardSyncHub : Hub
{
    public async Task JoinBoardGroup(int boardId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"board-{boardId}");
    }

    public async Task LeaveBoardGroup(int boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"board-{boardId}");
    }
}
