using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IBroadcastService
{
    BroadcastState State { get; }
    void SetMatch(string matchId);
    void UpdateScore(ScoreState score);
    void UpdateClock(int matchClockSeconds, int currentPeriod);
    void ShowScoreBug();
    void HideScoreBug();
    void ShowGraphic(string graphicType, int? durationMs = null);
    void HideGraphic();
    void SetOutputMode(BroadcastOutputMode mode);
    void SetTemplate(string templateId);
    event Action<BroadcastState> StateChanged;
}
