using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Scoring;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public class ScoringServiceTests
{
    private readonly ScoringService _sut = new();

    [Theory]
    [InlineData(Sport.Hurling)]
    [InlineData(Sport.Camogie)]
    [InlineData(Sport.LadiesFootball)]
    public void GoalsPointsSports_UseTraditionalFormat(Sport sport)
    {
        var match = MakeMatch(sport);
        match.HomeGoals = 1;
        match.HomePoints = 12;

        var state = _sut.GetState(match);

        state.ScoringModel.Should().Be(ScoringModel.GoalsPoints);
        state.Home.Format(state.ScoringModel).Should().Be("1-12");
        state.Home.Total.Should().Be(15);
    }

    [Fact]
    public void MensFootball_UsesGoalsTwoPointOnePointFormat()
    {
        var match = MakeMatch(Sport.GaelicFootball);
        match.HomeGoals = 2;
        match.HomeTwoPointScores = 3;
        match.HomePoints = 8;

        var state = _sut.GetState(match);

        state.ScoringModel.Should().Be(ScoringModel.GoalsTwoPointOnePoint);
        state.Home.Format(state.ScoringModel).Should().Be("2-3-8");
        state.Home.Total.Should().Be(20);
    }

    [Fact]
    public void TwoPointScore_IsRejectedForLgfa()
    {
        var match = MakeMatch(Sport.LadiesFootball);

        var act = () => _sut.Apply(match, EventTeam.Home, "two-point", 1);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only for men's Gaelic football*");
    }

    [Fact]
    public void TwoPointScore_IncrementsMensFootballTwoPointComponent()
    {
        var match = MakeMatch(Sport.GaelicFootball);

        _sut.Apply(match, EventTeam.Home, "two-point", 1);

        match.HomeTwoPointScores.Should().Be(1);
        match.HomeTotal.Should().Be(2);
    }

    private static Match MakeMatch(Sport sport) => new()
    {
        MatchId = "m1",
        Sport = sport,
        HomeTeam = "Clane",
        AwayTeam = "Naas"
    };
}
