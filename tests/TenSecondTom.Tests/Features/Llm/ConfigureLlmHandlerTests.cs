using System;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Llm;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Abstractions.Validation;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Llm;

public sealed class ConfigureLlmHandlerTests
{
    [Fact]
    public async Task Handle_WithCommandLineOverrides_UpdatesConfiguration()
    {
        var sectionStore = new Mock<IConfigurationSectionStore>();
        sectionStore
            .Setup(s => s.ReadSectionAsync<LlmOptions>(LlmOptions.SectionPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LlmOptions>.Success(new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                Model = "gpt-4o-mini",
                ApiKey = "old-key",
                MaxInputTokens = 1000
            }));

        sectionStore
            .Setup(s => s.WriteSectionAsync(
                LlmOptions.SectionPath,
                It.IsAny<LlmOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("config.json"));

        var wizard = new Mock<ISetupWizardUI>();
        wizard.Setup(w => w.ShowSuccess(It.IsAny<string>()));
        wizard.Setup(w => w.ShowStatus(It.IsAny<string>()));

        var validator = new Mock<IApiKeyValidator>();
        validator.Setup(v => v.Provider).Returns(LlmProvider.OpenAI);
        validator.Setup(v => v.ValidateFormatAsync(It.IsAny<string>()))
            .ReturnsAsync(ApiValidationResult.Success(TimeSpan.Zero));

        var handler = new ConfigureLlm.Handler(
            sectionStore.Object,
            wizard.Object,
            Mock.Of<IHttpClientFactory>(),
            new[] { validator.Object },
            Mock.Of<ILogger<ConfigureLlm.Handler>>());

        var command = new ConfigureLlm.Command
        {
            Force = true,
            ProviderOverride = LlmProvider.Anthropic,
            ModelOverride = LlmConstants.AnthropicModels.ClaudeSonnet,
            ApiKeyOverride = "anthropic-key",
            MaxInputTokensOverride = 200000
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Provider.Should().Be(LlmProvider.Anthropic);
        result.Value!.Model.Should().Be(LlmConstants.AnthropicModels.ClaudeSonnet);
        result.Value!.ApiKey.Should().Be("anthropic-key");
        result.Value!.MaxInputTokens.Should().Be(200000);

        sectionStore.Verify(s => s.WriteSectionAsync(
            LlmOptions.SectionPath,
            It.Is<LlmOptions>(o =>
                o.Provider == LlmProvider.Anthropic &&
                o.Model == LlmConstants.AnthropicModels.ClaudeSonnet &&
                o.ApiKey == "anthropic-key" &&
                o.MaxInputTokens == 200000),
            It.IsAny<CancellationToken>()),
            Times.Once);

        wizard.Verify(w => w.ShowSuccess(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidModelOverride_ReturnsFailure()
    {
        var sectionStore = new Mock<IConfigurationSectionStore>();
        sectionStore
            .Setup(s => s.ReadSectionAsync<LlmOptions>(LlmOptions.SectionPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LlmOptions>.Success(new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                Model = "gpt-4o-mini",
                ApiKey = "existing-key",
                MaxInputTokens = 1000
            }));

        var wizard = new Mock<ISetupWizardUI>();

        var handler = new ConfigureLlm.Handler(
            sectionStore.Object,
            wizard.Object,
            Mock.Of<IHttpClientFactory>(),
            Array.Empty<IApiKeyValidator>(),
            Mock.Of<ILogger<ConfigureLlm.Handler>>());

        var command = new ConfigureLlm.Command
        {
            Force = true,
            ProviderOverride = LlmProvider.OpenAI,
            ModelOverride = "claude-3.5"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Model 'claude-3.5' is not available for OpenAI");
        sectionStore.Verify(s => s.WriteSectionAsync(
            It.IsAny<string>(),
            It.IsAny<LlmOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
        wizard.Verify(w => w.ShowSuccess(It.IsAny<string>()), Times.Never);
    }
}

