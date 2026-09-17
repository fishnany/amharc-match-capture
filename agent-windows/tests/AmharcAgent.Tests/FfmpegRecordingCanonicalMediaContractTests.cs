using AmharcAgent.Core.Interfaces;
using AmharcAgent.Infrastructure.Recording;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class FfmpegRecordingCanonicalMediaContractTests
{
    [Fact]
    public void RecordingService_DependsOnCanonicalMediaSource_NotCameraAdapter()
    {
        var constructor = typeof(FfmpegRecordingService)
            .GetConstructors()
            .Single();

        var parameterTypes = constructor
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IStreamReceiverMediaSource), parameterTypes);
        Assert.DoesNotContain(typeof(ICameraAdapter), parameterTypes);
    }

    [Fact]
    public void CanonicalMediaLease_IsCredentialFreeStreamBoundary()
    {
        var streamProperty = typeof(IStreamReceiverMediaLease)
            .GetProperty(nameof(IStreamReceiverMediaLease.Stream));

        Assert.NotNull(streamProperty);
        Assert.Equal(typeof(Stream), streamProperty!.PropertyType);
    }

    [Fact]
    public void RecordingService_RequestsLossIntolerantCanonicalLease()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "agent-windows",
                "src",
                "AmharcAgent.Infrastructure",
                "Recording",
                "FfmpegRecordingService.cs"));

        Assert.Equal(
            2,
            source.Split(
                "AcquireAsync(ct, lossIntolerant: true)",
                StringSplitOptions.None).Length - 1);
    }
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(
                    Path.Combine(
                        directory.FullName,
                        ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }}