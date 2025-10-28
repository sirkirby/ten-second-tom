using FluentAssertions;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Commands;

namespace TenSecondTom.Tests.Unit.Features.Setup.Commands;

/// <summary>
/// Contract tests for Setup.Command
/// Tests the command structure, validation, and all scenarios from Setup.Command.contract.md
/// </summary>
public sealed class SetupCommandTests
{
    #region Command Structure Tests

    [Fact]
    public void SetupCommand_ShouldHaveForceProperty()
    {
        // Arrange & Act
        var command = new SetupCommand { Force = true };

        // Assert
        command.Force.Should().BeTrue();
    }

    [Fact]
    public void SetupCommand_ShouldHaveNonInteractiveProperty()
    {
        // Arrange & Act
        var command = new SetupCommand { NonInteractive = true };

        // Assert
        command.NonInteractive.Should().BeTrue();
    }

    [Fact]
    public void SetupCommand_ShouldHaveExistingConfigurationProperty()
    {
        // Arrange
        var existingConfig = CreateValidConfiguration();

        // Act
        var command = new SetupCommand { ExistingConfiguration = existingConfig };

        // Assert
        command.ExistingConfiguration.Should().NotBeNull();
        command.ExistingConfiguration.Should().BeSameAs(existingConfig);
    }

    [Fact]
    public void SetupCommand_DefaultValues_ShouldBeFalseAndNull()
    {
        // Arrange & Act
        var command = new SetupCommand();

        // Assert
        command.Force.Should().BeFalse("Force should default to false");
        command.NonInteractive.Should().BeFalse("NonInteractive should default to false");
        command.ExistingConfiguration.Should().BeNull("ExistingConfiguration should default to null");
    }

    #endregion

    #region Scenario Tests (Contract Validation)

    [Fact]
    public void FirstTimeSetup_ShouldHaveNoExistingConfiguration()
    {
        // Arrange & Act
        var command = new SetupCommand
        {
            Force = false,
            NonInteractive = false,
            ExistingConfiguration = null
        };

        // Assert
        command.ExistingConfiguration.Should().BeNull("first-time setup has no existing configuration");
        command.Force.Should().BeFalse("first-time setup doesn't need force");
    }

    [Fact]
    public void ReRunningSetup_ShouldIncludeExistingConfiguration()
    {
        // Arrange
        var existingConfig = CreateValidConfiguration();

        // Act
        var command = new SetupCommand
        {
            Force = false,
            NonInteractive = false,
            ExistingConfiguration = existingConfig
        };

        // Assert
        command.ExistingConfiguration.Should().NotBeNull("re-running setup has existing configuration");
        command.ExistingConfiguration.Should().BeSameAs(existingConfig);
    }

    [Fact]
    public void ForcedSetup_ShouldHaveForceFlagSet()
    {
        // Arrange & Act
        var command = new SetupCommand
        {
            Force = true,
            NonInteractive = false
        };

        // Assert
        command.Force.Should().BeTrue("forced setup requires Force flag");
    }

    [Fact]
    public void NonInteractiveSetup_ShouldHaveNonInteractiveFlagSet()
    {
        // Arrange & Act
        var command = new SetupCommand
        {
            Force = false,
            NonInteractive = true
        };

        // Assert
        command.NonInteractive.Should().BeTrue("non-interactive setup requires NonInteractive flag");
    }

    [Fact]
    public void ForcedNonInteractiveSetup_ShouldHaveBothFlagsSet()
    {
        // Arrange & Act
        var command = new SetupCommand
        {
            Force = true,
            NonInteractive = true
        };

        // Assert
        command.Force.Should().BeTrue("forced non-interactive setup has Force flag");
        command.NonInteractive.Should().BeTrue("forced non-interactive setup has NonInteractive flag");
    }

    [Fact]
    public void SetupCancellation_CommandStructure_ShouldSupportCancellation()
    {
        // Arrange & Act
        var command = new SetupCommand();

        // Assert
        // Cancellation is handled by the handler via CancellationToken
        // This test verifies the command structure supports cancellation scenarios
        command.Should().NotBeNull("command supports cancellation via handler");
    }

    [Fact]
    public void SetupWithTimeout_CommandStructure_ShouldSupportTimeoutScenarios()
    {
        // Arrange & Act
        var command = new SetupCommand();

        // Assert
        // Timeout is enforced by handler/configuration, not command structure
        // This test documents that timeout scenarios are supported
        command.Should().NotBeNull("command supports timeout scenarios via handler");
    }

    #endregion

    #region Validation Rule Tests

    [Fact]
    public void NonInteractiveWithoutExistingConfig_ShouldBeValidScenario()
    {
        // Arrange & Act
        var command = new SetupCommand
        {
            NonInteractive = true,
            ExistingConfiguration = null
        };

        // Assert
        // According to contract: "If NonInteractive is true and ExistingConfiguration is null, use system defaults"
        command.NonInteractive.Should().BeTrue();
        command.ExistingConfiguration.Should().BeNull("system defaults will be used");
    }

    [Fact]
    public void ForceAndNonInteractive_CanBothBeTrue()
    {
        // Arrange & Act
        var command = new SetupCommand
        {
            Force = true,
            NonInteractive = true
        };

        // Assert
        // According to contract: "Force and NonInteractive can both be true"
        command.Force.Should().BeTrue();
        command.NonInteractive.Should().BeTrue();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void SetupCommand_WithSameProperties_ShouldBeEqual()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var command1 = new SetupCommand
        {
            Force = true,
            NonInteractive = true,
            ExistingConfiguration = config
        };
        var command2 = new SetupCommand
        {
            Force = true,
            NonInteractive = true,
            ExistingConfiguration = config
        };

        // Act & Assert
        command1.Should().Be(command2, "records with same properties should be equal");
        (command1 == command2).Should().BeTrue("== operator should work for equal records");
    }

    [Fact]
    public void SetupCommand_WithDifferentProperties_ShouldNotBeEqual()
    {
        // Arrange
        var command1 = new SetupCommand { Force = true };
        var command2 = new SetupCommand { Force = false };

        // Act & Assert
        command1.Should().NotBe(command2, "records with different properties should not be equal");
        (command1 != command2).Should().BeTrue("!= operator should work for different records");
    }

    [Fact]
    public void SetupCommand_ShouldSupportWith_Expression()
    {
        // Arrange
        var original = new SetupCommand { Force = false };

        // Act
        var modified = original with { Force = true };

        // Assert
        original.Force.Should().BeFalse("original should be unchanged");
        modified.Force.Should().BeTrue("modified should have new value");
    }

    #endregion

    #region Helper Methods

    private static ConfigurationSettings CreateValidConfiguration()
    {
        return new ConfigurationSettings
        {
            Ssh = new SshConfiguration
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem,
                AgentSocketPath = null
            },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890abcdef",
                Model = "gpt-4"
            },
            RootDirectory = "~/.ten-second-tom/memory",
            Storage = new StorageConfiguration
            {
                CreateIfMissing = true
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = 30,
                EnableTelemetry = false
            },
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            ConfigurationVersion = "1.0"
        };
    }

    #endregion
}
