using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace TenSecondTom.Tests.Features.Llm;

public sealed class LlmConfigCommandBuilderTests
{
    [Fact]
    public async Task Invoke_WithListProvidersOption_DoesNotCallMediator()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var command = BuildCommand(mediator.Object);

        var exitCode = await InvokeAsync(command, "--list-providers");

        exitCode.Should().Be(0);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invoke_ListModelsWithoutProvider_ReturnsError()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var command = BuildCommand(mediator.Object);

        var exitCode = await InvokeAsync(command, "--list-models");

        exitCode.Should().Be(1);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invoke_ListModelsWithProvider_Succeeds()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var command = BuildCommand(mediator.Object);

        var exitCode = await InvokeAsync(command, "--list-models --provider anthropic");

        exitCode.Should().Be(0);
        mediator.VerifyNoOtherCalls();
    }

    private static Command BuildCommand(IMediator mediator)
    {
        var services = new ServiceCollection();
        services.AddSingleton(mediator);
        var serviceProvider = services.BuildServiceProvider();

        var builder = new TenSecondTom.Features.Llm.LlmConfigCommandBuilder();
        var jsonOption = new Option<bool>("--json");

        return builder.BuildConfigSubcommand(serviceProvider, jsonOption)!;
    }

    private static Task<int> InvokeAsync(Command command, string args)
    {
        var parseResult = command.Parse(args);
        return parseResult.InvokeAsync();
    }
}

