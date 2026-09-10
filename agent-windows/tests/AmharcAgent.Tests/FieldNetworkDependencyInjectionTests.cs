using Xunit;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Infrastructure.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AmharcAgent.Tests;

public sealed class FieldNetworkDependencyInjectionTests
{
    [Fact]
    public void AddAmharcInfrastructure_ResolvesFieldNetworkServiceAndRemediationOrchestrator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAmharcInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = false
            });

        var fieldNetwork =
            provider.GetRequiredService<IFieldNetworkService>();
        var orchestrator =
            provider.GetRequiredService<IFieldNetworkRemediationOrchestrator>();

        fieldNetwork.Should().NotBeNull();
        orchestrator.Should().NotBeNull();
    }
}