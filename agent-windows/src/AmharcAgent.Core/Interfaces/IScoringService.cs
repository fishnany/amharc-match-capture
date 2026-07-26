using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IScoringService
{
    ScoreState GetState(Match match);
    ScoreSnapshot GetSnapshot(Match match);
    void Apply(Match match, EventTeam team, string scoreType, int delta = 1);
}
