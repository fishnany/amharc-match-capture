using AmharcAgent.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace AmharcAgent.Api.Hubs;

/// <summary>
/// SignalR hub for real-time push to the operator UI.
/// The UI connects on load and joins the relevant match group.
/// Server-push methods are called from controllers/services via IHubContext.
/// </summary>
public class MatchHub : Hub
{
    /// <summary>Join a match group to receive real-time updates for that match.</summary>
    public async Task JoinMatch(string matchId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"match:{matchId}");

    /// <summary>Leave a match group.</summary>
    public async Task LeaveMatch(string matchId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match:{matchId}");

    // Server → client push methods (called via IHubContext<MatchHub>):
    // ClockUpdated(ClockState state)
    // ScoreUpdated(object score)
    // EventCreated(MatchEvent evt)
    // EventDeleted(string eventId)
    // RecordingStateChanged(string state)
    // StreamingStateChanged(string state)
    // CameraStateChanged(string cameraId, string state)
    // SystemHealthChanged(SystemHealth health)
    // StorageWarning(StorageStatus status)
}
