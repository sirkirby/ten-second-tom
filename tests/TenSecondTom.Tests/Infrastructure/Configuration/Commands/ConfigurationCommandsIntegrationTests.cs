using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Configuration.Commands;
using TenSecondTom.Infrastructure.DependencyInjection;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Infrastructure.Configuration.Commands;

/// <summary>
/// Integration tests for configuration CQRS commands.
/// Verifies MediatR can discover and execute Infrastructure command handlers.
/// </summary>
public sealed class ConfigurationCommandsIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly string _tempConfigPath;

    public ConfigurationCommandsIntegrationTests()
    {
        // Create a temporary config file path
        _tempConfigPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.json");

        // Build minimal service collection with MediatR and Infrastructure services
        var services = new ServiceCollection();

        // Add minimal configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TenSecondTom:ConfigDirectory"] = Path.GetDirectoryName(_tempConfigPath),
            ["TenSecondTom:ConfigFileName"] = Path.GetFileName(_tempConfigPath)
        });
        var configuration = configBuilder.Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Add logging
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        // Add configuration infrastructure
        services.AddSingleton<IConfigurationSectionStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConfigurationSectionStore>>();
            var config = sp.GetRequiredService<IConfiguration>();
            return new ConfigurationSectionStore(logger, config, _tempConfigPath);
        });

        // Add MediatR with assembly scanning (discovers our command handlers)
        services.AddApplicationServices();

        // Manually register generic handler for ReadConfigurationSection<TestConfiguration>
        // (generic nested handlers aren't auto-discovered by MediatR assembly scanning)
        services.AddTransient(
            typeof(IRequestHandler<,>).MakeGenericType(
                typeof(ReadConfigurationSection<TestConfiguration>.Query),
                typeof(Result<TestConfiguration>)),
            typeof(ReadConfigurationSection<TestConfiguration>.Handler));

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task UpdateAndReadConfigurationSection_RoundTrip_Success()
    {
        // Arrange
        var sectionPath = "TenSecondTom:TestSection";
        var originalConfig = new TestConfiguration
        {
            Name = "Test Config",
            Value = 42,
            IsEnabled = true
        };

        // Act - Write configuration
        var updateCommand = new UpdateConfigurationSection.Command(sectionPath, originalConfig);
        var updateResult = await _mediator.Send(updateCommand);

        // Assert - Write succeeded
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value.Should().Be(_tempConfigPath);

        // Act - Read configuration back
        var readQuery = new ReadConfigurationSection<TestConfiguration>.Query(sectionPath);
        var readResult = await _mediator.Send(readQuery);

        // Assert - Read succeeded and matches original
        readResult.IsSuccess.Should().BeTrue();
        readResult.Value.Should().BeEquivalentTo(originalConfig);
    }

    [Fact]
    public async Task ReadConfigurationSection_NonExistentSection_ReturnsDefault()
    {
        // Arrange
        var sectionPath = "TenSecondTom:NonExistent";
        var query = new ReadConfigurationSection<TestConfiguration>.Query(sectionPath);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(string.Empty); // Default value
        result.Value.Value.Should().Be(0); // Default value
        result.Value.IsEnabled.Should().BeFalse(); // Default value
    }

    [Fact]
    public async Task UpdateConfigurationSection_MultipleSections_PreservesOthers()
    {
        // Arrange
        var section1Path = "TenSecondTom:Section1";
        var section2Path = "TenSecondTom:Section2";

        var config1 = new TestConfiguration { Name = "Config 1", Value = 1 };
        var config2 = new TestConfiguration { Name = "Config 2", Value = 2 };

        // Act - Write first section
        var command1 = new UpdateConfigurationSection.Command(section1Path, config1);
        await _mediator.Send(command1);

        // Act - Write second section
        var command2 = new UpdateConfigurationSection.Command(section2Path, config2);
        await _mediator.Send(command2);

        // Assert - Both sections exist and are preserved
        var query1 = new ReadConfigurationSection<TestConfiguration>.Query(section1Path);
        var result1 = await _mediator.Send(query1);
        result1.IsSuccess.Should().BeTrue();
        result1.Value.Should().BeEquivalentTo(config1);

        var query2 = new ReadConfigurationSection<TestConfiguration>.Query(section2Path);
        var result2 = await _mediator.Send(query2);
        result2.IsSuccess.Should().BeTrue();
        result2.Value.Should().BeEquivalentTo(config2);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();

        // Clean up temp config file
        if (File.Exists(_tempConfigPath))
        {
            File.Delete(_tempConfigPath);
        }
    }

    /// <summary>
    /// Test configuration class for integration tests.
    /// </summary>
    public sealed class TestConfiguration
    {
        public string Name { get; init; } = string.Empty;
        public int Value { get; init; }
        public bool IsEnabled { get; init; }
    }
}
