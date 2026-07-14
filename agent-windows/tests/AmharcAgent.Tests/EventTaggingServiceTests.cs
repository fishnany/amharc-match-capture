using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;
using FluentAssertions;
using Moq;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainMatch = AmharcAgent.Core.Domain.Match;

namespace AmharcAgent.Tests;

public class EventTaggingServiceTests
{
    private static DomainMatch MakeMatch() => new()
    {
        MatchId = "m1", HomeTeam = "Clare", AwayTeam = "Galway",
        HomeGoals = 0, HomePoints = 0, AwayGoals = 0, AwayPoints = 0
    };

    [Fact]
    public async Task CreateEvent_Point_Home_IncrementsHomePoints()
    {
        var match = MakeMatch();
        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetByIdAsync("m1", default)).ReturnsAsync(match);
        matches.Setup(m => m.UpdateAsync(It.IsAny<DomainMatch>(), default)).ReturnsAsync(match);
        events.Setup(e => e.CreateAsync(
        It.IsAny<MatchEvent>(),
        It.IsAny<CancellationToken>()))
    .Returns((MatchEvent ev, CancellationToken _) =>
        Task.FromResult(ev));

        var sut = new EventTaggingService(events.Object, matches.Object,
            NullLogger<EventTaggingService>.Instance);

        var opts = new CreateEventOptions("m1", "point", EventTeam.Home, null, 1, 120, 125,
            EventSource.OperatorUi);
        var result = await sut.CreateEventAsync(opts, default);

        result.EventType.Should().Be("point");
        result.Team.Should().Be(EventTeam.Home);

        // Both timestamps must be present and independent
        result.MatchClockSeconds.Should().Be(120);
        result.RecordingElapsedSeconds.Should().Be(125,
            "RecordingElapsedSeconds must be stored independently from MatchClockSeconds");
    }

    [Fact]
    public async Task CreateEvent_DualClockValues_StoredIndependently()
    {
        // The critical test: match clock = 600s, recording = 650s (50s of half-time has elapsed in recording but not match clock)
        var match = MakeMatch();
        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetByIdAsync("m1", default)).ReturnsAsync(match);
        matches.Setup(m => m.UpdateAsync(It.IsAny<DomainMatch>(), default)).ReturnsAsync(match);
        events.Setup(e => e.CreateAsync(
        It.IsAny<MatchEvent>(),
        It.IsAny<CancellationToken>()))
    .Returns((MatchEvent ev, CancellationToken _) =>
        Task.FromResult(ev));

        var sut = new EventTaggingService(events.Object, matches.Object,
            NullLogger<EventTaggingService>.Instance);

        var opts = new CreateEventOptions("m1", "goal", EventTeam.Away, null, 2, 600, 650,
            EventSource.StreamDeck);
        var result = await sut.CreateEventAsync(opts, default);

        result.MatchClockSeconds.Should().Be(600);
        result.RecordingElapsedSeconds.Should().Be(650);
        result.MatchClockSeconds.Should().NotBe(result.RecordingElapsedSeconds,
            "dual-clock values should differ when half-time has elapsed in recording time");
    }

    [Fact]
    public async Task ExportEventsCsv_ContainsDualClockColumns()
    {
        var match = MakeMatch();
        var evt = new MatchEvent
        {
            EventId = "e1", MatchId = "m1", EventType = "point",
            MatchClockSeconds = 300, RecordingElapsedSeconds = 340,
            Period = 1, Source = EventSource.OperatorUi,
            SystemTimestamp = DateTimeOffset.UtcNow
        };
        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();
        events.Setup(e => e.GetByMatchIdAsync("m1", default))
            .ReturnsAsync(new List<MatchEvent> { evt });

        var sut = new EventTaggingService(events.Object, matches.Object,
            NullLogger<EventTaggingService>.Instance);

        var csv = await sut.ExportEventsCsvAsync("m1", default);
        csv.Should().Contain("MatchClockSeconds");
        csv.Should().Contain("RecordingElapsedSeconds");
        csv.Should().Contain("300");
        csv.Should().Contain("340");
    }
}
