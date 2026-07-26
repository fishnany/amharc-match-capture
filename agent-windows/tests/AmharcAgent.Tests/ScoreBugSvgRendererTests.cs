using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Broadcast;
using FluentAssertions;

namespace AmharcAgent.Tests;

public sealed class ScoreBugSvgRendererTests
{
    private readonly ScoreBugSvgRenderer _renderer = new();

    [Fact]
    public void Hurling_Renders_GoalsPoints_Total_And_Canonical_Logo_Asset()
    {
        var svg = _renderer.RenderSvg(Model(
            Sport.Hurling,
            ScoringModel.GoalsPoints,
            new TeamScoreState(1, 0, 12),
            new TeamScoreState(0, 0, 15)));

        svg.Should().Contain("1-12");
        svg.Should().Contain("(15)");
        svg.Should().Contain("/branding/amharc-logo-transparent.png");
        svg.Should().NotContain("<text x=\"70\"", "the logo must never be reconstructed as text");
    }

    [Fact]
    public void MensFootball_Renders_GoalsTwoPointOnePoint_And_Total()
    {
        var svg = _renderer.RenderSvg(Model(
            Sport.GaelicFootball,
            ScoringModel.GoalsTwoPointOnePoint,
            new TeamScoreState(2, 3, 8),
            new TeamScoreState(1, 4, 7)));

        svg.Should().Contain("2-3-8");
        svg.Should().Contain("(20)");
        svg.Should().Contain("1-4-7");
        svg.Should().Contain("(18)");
    }

    [Fact]
    public void Discipline_Uses_Red_Persisted_And_Black_Active_Indicators()
    {
        var model = Model(
            Sport.Camogie,
            ScoringModel.GoalsPoints,
            new TeamScoreState(0, 0, 8),
            new TeamScoreState(1, 0, 4)) with
        {
            HomeDiscipline = new TeamDisciplineState(true, false),
            AwayDiscipline = new TeamDisciplineState(false, true)
        };

        var svg = _renderer.RenderSvg(model);
        svg.Should().Contain("#E63946");
        svg.Should().Contain("stroke=\"#8b8b8b\"");
    }

    private static ScoreBugViewModel Model(
        Sport sport,
        ScoringModel scoring,
        TeamScoreState home,
        TeamScoreState away) => new(
            "match-1",
            sport,
            scoring,
            "Clane",
            "CLA",
            home,
            new TeamDisciplineState(false, false),
            "Naas",
            "NAA",
            away,
            3277,
            2,
            MatchStatus.Active,
            false,
            "/branding/amharc-logo-transparent.png",
            BrandAssets.DefaultTheme);
}
