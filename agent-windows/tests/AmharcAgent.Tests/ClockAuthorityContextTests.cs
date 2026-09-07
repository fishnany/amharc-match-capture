using AmharcAgent.Infrastructure.Clock;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public class ClockAuthorityContextTests
{
    [Fact]
    public void Constructor_EstablishesCanonicalCaptureAuthority()
    {
        var context =
            new ClockAuthorityContext();

        context.Authority.SourceApplication
            .Should()
            .Be("amharc-match-capture");

        context.Authority.InstanceId
            .Should()
            .NotBeNullOrWhiteSpace();

        Guid.TryParse(
                context.Authority.InstanceId,
                out _)
            .Should()
            .BeTrue();

        context.AuthorityEpoch
            .Should()
            .Be(0);
    }

    [Fact]
    public void NewContexts_EstablishDistinctAuthorityInstances()
    {
        var first =
            new ClockAuthorityContext();

        var second =
            new ClockAuthorityContext();

        first.Authority.InstanceId
            .Should()
            .NotBe(second.Authority.InstanceId);

        first.AuthorityEpoch
            .Should()
            .Be(0);

        second.AuthorityEpoch
            .Should()
            .Be(0);
    }

    [Fact]
    public void NextSequence_IncreasesMonotonicallyWithinEpoch()
    {
        var context =
            new ClockAuthorityContext();

        var first =
            context.NextSequence();

        var second =
            context.NextSequence();

        var third =
            context.NextSequence();

        first.Should().Be(1);
        second.Should().Be(2);
        third.Should().Be(3);

        context.AuthorityEpoch
            .Should()
            .Be(0);
    }
}
