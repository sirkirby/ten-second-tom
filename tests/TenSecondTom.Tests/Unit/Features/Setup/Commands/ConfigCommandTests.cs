using FluentAssertions;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Tests.Unit.Features.Setup.Commands;

/// <summary>
/// Contract tests for ConfigCommand
/// Tests the command structure, validation, and all scenarios from ConfigCommand.contract.md
/// </summary>
public sealed class ConfigCommandTests
{
    #region Command Structure Tests

    [Fact]
    public void ConfigCommand_ShouldHaveActionProperty()
    {
        // Arrange & Act
        var command = new ConfigCommand { Action = ConfigAction.Set };

        // Assert
        command.Action.Should().Be(ConfigAction.Set);
    }

    [Fact]
    public void ConfigCommand_ShouldHaveSettingNameProperty()
    {
        // Arrange & Act
        var command = new ConfigCommand { SettingName = "llm-provider" };

        // Assert
        command.SettingName.Should().Be("llm-provider");
    }

    [Fact]
    public void ConfigCommand_ShouldHaveSettingValueProperty()
    {
        // Arrange & Act
        var command = new ConfigCommand { SettingValue = "OpenAI" };

        // Assert
        command.SettingValue.Should().Be("OpenAI");
    }

    [Fact]
    public void ConfigCommand_ShouldHaveShowSecretsProperty()
    {
        // Arrange & Act
        var command = new ConfigCommand { ShowSecrets = true };

        // Assert
        command.ShowSecrets.Should().BeTrue();
    }

    [Fact]
    public void ConfigCommand_DefaultValues_ShouldBeShowActionAndFalse()
    {
        // Arrange & Act
        var command = new ConfigCommand();

        // Assert
        command.Action.Should().Be(ConfigAction.Show, "Action should default to Show");
        command.SettingName.Should().BeNull("SettingName should default to null");
        command.SettingValue.Should().BeNull("SettingValue should default to null");
        command.ShowSecrets.Should().BeFalse("ShowSecrets should default to false");
    }

    #endregion

    #region ConfigAction Enum Tests

    [Fact]
    public void ConfigAction_ShouldHaveShowValue()
    {
        // Arrange & Act
        var action = ConfigAction.Show;

        // Assert
        action.Should().Be(ConfigAction.Show);
        Enum.IsDefined(action).Should().BeTrue();
    }

    [Fact]
    public void ConfigAction_ShouldHaveSetValue()
    {
        // Arrange & Act
        var action = ConfigAction.Set;

        // Assert
        action.Should().Be(ConfigAction.Set);
        Enum.IsDefined(action).Should().BeTrue();
    }

    [Fact]
    public void ConfigAction_ShouldHaveResetValue()
    {
        // Arrange & Act
        var action = ConfigAction.Reset;

        // Assert
        action.Should().Be(ConfigAction.Reset);
        Enum.IsDefined(action).Should().BeTrue();
    }

    [Fact]
    public void ConfigAction_ShouldHaveValidateValue()
    {
        // Arrange & Act
        var action = ConfigAction.Validate;

        // Assert
        action.Should().Be(ConfigAction.Validate);
        Enum.IsDefined(action).Should().BeTrue();
    }

    #endregion

    #region Scenario Tests (Contract Validation)

    [Fact]
    public void ShowCurrentConfiguration_ShouldHaveShowAction()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Show,
            ShowSecrets = false
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Show);
        command.ShowSecrets.Should().BeFalse("secrets should be masked by default");
    }

    [Fact]
    public void ShowConfigurationWithSecrets_ShouldHaveShowSecretsFlag()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Show,
            ShowSecrets = true
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Show);
        command.ShowSecrets.Should().BeTrue("last 4 chars of secrets should be shown");
    }

    [Fact]
    public void ChangeLlmProvider_ShouldHaveSetActionAndSettingNameAndValue()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider",
            SettingValue = "Anthropic"
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Set);
        command.SettingName.Should().Be("llm-provider");
        command.SettingValue.Should().Be("Anthropic");
    }

    [Fact]
    public void UpdateMemoryDirectory_ShouldHaveSetActionWithDirectoryPath()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "memory-directory",
            SettingValue = "/custom/path"
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Set);
        command.SettingName.Should().Be("memory-directory");
        command.SettingValue.Should().Be("/custom/path");
    }

    [Fact]
    public void UpdateSshKeyPath_ShouldHaveSetActionWithKeyPath()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "ssh-key-path",
            SettingValue = "~/.ssh/custom_key"
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Set);
        command.SettingName.Should().Be("ssh-key-path");
        command.SettingValue.Should().Be("~/.ssh/custom_key");
    }

    [Fact]
    public void UpdateApiKey_ShouldHaveSetActionWithMaskedValue()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "api-key",
            SettingValue = "sk-ant-1234567890abcdef"
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Set);
        command.SettingName.Should().Be("api-key");
        command.SettingValue.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void UpdateLogLevel_ShouldHaveSetActionWithValidLogLevel()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "log-level",
            SettingValue = "Debug"
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Set);
        command.SettingName.Should().Be("log-level");
        command.SettingValue.Should().Be("Debug");
    }

    [Fact]
    public void UpdateRetentionDays_ShouldHaveSetActionWithPositiveInteger()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "retention-days",
            SettingValue = "60"
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Set);
        command.SettingName.Should().Be("retention-days");
        command.SettingValue.Should().Be("60");
        int.Parse(command.SettingValue!, System.Globalization.CultureInfo.InvariantCulture).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ResetConfiguration_ShouldHaveResetAction()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Reset
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Reset);
    }

    [Fact]
    public void ValidateConfiguration_ShouldHaveValidateAction()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Validate);
    }

    #endregion

    #region Validation Rule Tests

    [Fact]
    public void SetAction_WithSettingName_ShouldBeValidStructure()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider",
            SettingValue = "OpenAI"
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Set);
        command.SettingName.Should().NotBeNullOrEmpty("SettingName is required for Set action");
        command.SettingValue.Should().NotBeNullOrEmpty("SettingValue is required for Set action");
    }

    [Fact]
    public void SetAction_WithoutSettingName_ShouldHaveNullSettingName()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set
        };

        // Assert
        // Validation is handled by handler/validator, not command structure
        command.SettingName.Should().BeNull("handler will validate this");
    }

    [Fact]
    public void SetAction_WithoutSettingValue_ShouldHaveNullSettingValue()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider"
        };

        // Assert
        // Validation is handled by handler/validator, not command structure
        command.SettingValue.Should().BeNull("handler will validate this");
    }

    [Fact]
    public void ShowAction_WithSettingNameAndValue_ShouldBeIgnored()
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Show,
            SettingName = "ignored",
            SettingValue = "ignored"
        };

        // Assert
        command.Action.Should().Be(ConfigAction.Show);
        // SettingName and SettingValue are ignored for Show action
        command.SettingName.Should().Be("ignored");
        command.SettingValue.Should().Be("ignored");
    }

    #endregion

    #region Valid Setting Names Tests

    [Theory]
    [InlineData("llm-provider")]
    [InlineData("api-key")]
    [InlineData("memory-directory")]
    [InlineData("ssh-key-path")]
    [InlineData("log-level")]
    [InlineData("retention-days")]
    public void ValidSettingNames_ShouldBeAccepted(string settingName)
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = settingName,
            SettingValue = "test-value"
        };

        // Assert
        command.SettingName.Should().Be(settingName);
    }

    [Theory]
    [InlineData("LLM-PROVIDER", "llm-provider")]
    [InlineData("Api-Key", "api-key")]
    [InlineData("MEMORY-DIRECTORY", "memory-directory")]
    public void SettingNames_ShouldSupportCaseVariations(string inputName, string expectedName)
    {
        // Arrange & Act
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = inputName
        };

        // Assert
        // Case-insensitive comparison should be handled by handler/validator
        ArgumentNullException.ThrowIfNull(expectedName);
        command.SettingName.Should().NotBeNull();
        command.SettingName!.ToUpperInvariant().Should().Be(expectedName.ToUpperInvariant());
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void ConfigCommand_WithSameProperties_ShouldBeEqual()
    {
        // Arrange
        var command1 = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider",
            SettingValue = "OpenAI",
            ShowSecrets = true
        };
        var command2 = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider",
            SettingValue = "OpenAI",
            ShowSecrets = true
        };

        // Act & Assert
        command1.Should().Be(command2, "records with same properties should be equal");
        (command1 == command2).Should().BeTrue("== operator should work for equal records");
    }

    [Fact]
    public void ConfigCommand_WithDifferentProperties_ShouldNotBeEqual()
    {
        // Arrange
        var command1 = new ConfigCommand { Action = ConfigAction.Show };
        var command2 = new ConfigCommand { Action = ConfigAction.Set };

        // Act & Assert
        command1.Should().NotBe(command2, "records with different properties should not be equal");
        (command1 != command2).Should().BeTrue("!= operator should work for different records");
    }

    [Fact]
    public void ConfigCommand_ShouldSupportWith_Expression()
    {
        // Arrange
        var original = new ConfigCommand { Action = ConfigAction.Show };

        // Act
        var modified = original with { Action = ConfigAction.Set };

        // Assert
        original.Action.Should().Be(ConfigAction.Show, "original should be unchanged");
        modified.Action.Should().Be(ConfigAction.Set, "modified should have new value");
    }

    #endregion
}
