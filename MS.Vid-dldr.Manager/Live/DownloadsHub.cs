using Microsoft.AspNetCore.SignalR;

namespace MS.Vid_dldr.Manager.Live;

/// <summary>
/// Browsers connect to <see cref="LiveProgressSetup.HubPath"/> and join their download session's group. The server
/// pushes <c>progress</c> (a tick) and <c>changed</c> (re-read the list); clients send nothing else.
/// </summary>
public sealed class DownloadsHub : Hub
{
    public static string Group(string sessionId) => $"session:{sessionId.ToLowerInvariant()}";

    public Task JoinSession(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out _))
            throw new HubException("sessionId must be a GUID");
        return Groups.AddToGroupAsync(Context.ConnectionId, Group(sessionId));
    }

    public Task LeaveSession(string sessionId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(sessionId));
}
