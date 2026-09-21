using AmharcAgent.Core.Domain;
using AmharcAgent.Data;
using AmharcAgent.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AmharcAgent.Tests;

public class MatchClockStateStoreTests : IDisposable
{
    private readonly string _databasePath;
    private readonly DbContextOptions<AmharcDbContext> _options;
    private readonly IDbContextFactory<AmharcDbContext> _factory;
    private readonly MatchClockStateStore _sut;

    public MatchClockStateStoreTests()
    {
        _databasePath =
            Path.Combine(
                Path.GetTempPath(),
                $"amharc-match-clock-store-{Guid.NewGuid():N}.db");

        _options =
            new DbContextOptionsBuilder<AmharcDbContext>()
                .UseSqlite($"Data Source={_databasePath};Pooling=False")
                .Options;

        _factory =
            new TestDbContextFactory(_options);

        using var db =
            new AmharcDbContext(_options);

        db.Database.EnsureCreated();

        _sut =
            new MatchClockStateStore(_factory);
    }

    [Fact]
    public async Task SaveAsync_ExistingState_UpdatesPeriodStartBoundary()
    {
        var periodOne =
            new MatchClockRuntimeState
            {
                MatchId = "match-1",
                MatchClockSeconds = 2100,
                RecordingElapsedSeconds = 2300,
                IsRunning = false,
                CurrentPeriod = 1,
                PeriodStartTotalMatchElapsedSeconds = 0,
                ClockMode = "count-up",
                PersistedAt = DateTimeOffset.UtcNow
            };

        await _sut.SaveAsync(periodOne);

        var initial =
            await _sut.LoadAsync("match-1");

        initial.Should().NotBeNull();
        initial!.CurrentPeriod.Should().Be(1);
        initial.PeriodStartTotalMatchElapsedSeconds.Should().Be(0);

        var periodTwo =
            new MatchClockRuntimeState
            {
                MatchId = "match-1",
                MatchClockSeconds = 2100,
                RecordingElapsedSeconds = 2400,
                IsRunning = true,
                CurrentPeriod = 2,
                PeriodStartTotalMatchElapsedSeconds = 2100,
                ClockMode = "count-up",
                PersistedAt = DateTimeOffset.UtcNow
            };

        await _sut.SaveAsync(periodTwo);

        // Load through the repository again. MatchClockStateStore creates
        // a fresh DbContext for every operation, so this verifies persisted
        // SQLite state rather than an EF tracked entity.
        var persisted =
            await _sut.LoadAsync("match-1");

        persisted.Should().NotBeNull();
        persisted!.CurrentPeriod.Should().Be(2);
        persisted.MatchClockSeconds.Should().Be(2100);
        persisted.PeriodStartTotalMatchElapsedSeconds.Should().Be(2100);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<AmharcDbContext> options)
        : IDbContextFactory<AmharcDbContext>
    {
        public AmharcDbContext CreateDbContext() =>
            new(options);

        public Task<AmharcDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new AmharcDbContext(options));
    }
}
