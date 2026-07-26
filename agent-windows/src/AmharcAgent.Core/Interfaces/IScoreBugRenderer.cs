using AmharcAgent.Core.Broadcast;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Renders the canonical AMHARC Phase 1.1 score bug.</summary>
public interface IScoreBugRenderer
{
    string RenderSvg(ScoreBugViewModel model);
}
