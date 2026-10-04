using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Moq;

namespace AmharcAgent.Tests;

/// <summary>Explicit adapter for legacy DTO/dispatcher unit fixtures.
/// These tests do not prove ownership; W1FoundationTests use the actual clock.</summary>
internal static class W1ClockTestBinding
{
    private sealed class Lease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    public static void Attach(Mock<IMatchClockService> clock)
    {
        var bound = clock.As<ISubjectBoundClockService>();
        bound.Setup(x => x.EnterCommandAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IAsyncDisposable>(new Lease()));
        bound.Setup(x => x.StartFor(It.IsAny<string>())).Callback<string>(_ => clock.Object.Start());
        bound.Setup(x => x.EndSubject(It.IsAny<string>())).Callback<string>(_ => clock.Object.Pause());
        bound.Setup(x => x.ReadFor(It.IsAny<string>(), It.IsAny<Func<ClockState, ClockState>>()))
            .Returns((string _, Func<ClockState, ClockState> f) => f(clock.Object.State));
        bound.Setup(x => x.ReadFor(It.IsAny<string>(), It.IsAny<Func<ClockState, ClockSnapshotV1>>()))
            .Returns((string _, Func<ClockState, ClockSnapshotV1> f) => f(clock.Object.State));
    }
}