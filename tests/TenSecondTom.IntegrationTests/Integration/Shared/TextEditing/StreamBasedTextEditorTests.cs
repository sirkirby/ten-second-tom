using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TenSecondTom.Shared.TextEditing.Models;
using TenSecondTom.Shared.TextEditing.Services;

namespace TenSecondTom.IntegrationTests.Integration.TextEditing;

/// <summary>
/// Integration tests for StreamBasedTextEditor with simulated console input.
/// </summary>
public sealed class StreamBasedTextEditorTests
{
    [Fact]
    public async Task EditAsync_WithSimulatedInput_ReturnsContentWhenSaved()
    {
        // Arrange
        var sanitizer = new InputSanitizer();
        var logger = NullLogger<StreamBasedTextEditor>.Instance;
        var editor = new StreamBasedTextEditor(sanitizer, logger);

        // Simulate piped input: 3 lines followed by EOF
        // In piped input mode, content is auto-saved after EOF (no prompt)
        var inputLines = new[]
        {
            "Line 1",
            "Line 2",
            "Line 3"
        };

        using var inputReader = new StringReader(string.Join(Environment.NewLine, inputLines));
        using var outputWriter = new StringWriter();

        var originalIn = Console.In;
        var originalOut = Console.Out;

        try
        {
            Console.SetIn(inputReader);
            Console.SetOut(outputWriter);

            // Act
            var result = await editor.EditAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSaved.Should().BeTrue();
            result.Content.Should().Contain("Line 1");
            result.Content.Should().Contain("Line 2");
            result.Content.Should().Contain("Line 3");
            result.Metadata.CharacterCount.Should().BeGreaterThan(0);
            result.Metadata.LineCount.Should().Be(3);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task EditAsync_WithCancellation_ReturnsCancelledResult()
    {
        // Arrange
        var sanitizer = new InputSanitizer();
        var logger = NullLogger<StreamBasedTextEditor>.Instance;
        var editor = new StreamBasedTextEditor(sanitizer, logger);

        // Simulate cancellation via cancellation token
        using var cts = new CancellationTokenSource();
        var inputLines = new[]
        {
            "Some content",
            "More content"
        };

        using var inputReader = new StringReader(string.Join(Environment.NewLine, inputLines));
        using var outputWriter = new StringWriter();

        var originalIn = Console.In;
        var originalOut = Console.Out;

        try
        {
            Console.SetIn(inputReader);
            Console.SetOut(outputWriter);

            // Cancel immediately
            await cts.CancelAsync();

            // Act
            var result = await editor.EditAsync(cancellationToken: cts.Token);

            // Assert
            result.Should().NotBeNull();
            result.IsCancelled.Should().BeTrue();
            result.Content.Should().BeEmpty();
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task EditAsync_WithInitialContent_PreservesContent()
    {
        // Arrange
        var sanitizer = new InputSanitizer();
        var logger = NullLogger<StreamBasedTextEditor>.Instance;
        var editor = new StreamBasedTextEditor(sanitizer, logger);

        var initialContent = "Initial line 1\nInitial line 2";

        // Simulate adding one more line, then EOF and save
        var inputLines = new[]
        {
            "Additional line",
            null, // EOF (Ctrl+D)
            "y"   // Save
        };

        using var inputReader = new StringReader(string.Join(Environment.NewLine, inputLines));
        using var outputWriter = new StringWriter();

        var originalIn = Console.In;
        var originalOut = Console.Out;

        try
        {
            Console.SetIn(inputReader);
            Console.SetOut(outputWriter);

            // Act
            var result = await editor.EditAsync(initialContent);

            // Assert
            result.Should().NotBeNull();
            result.IsSaved.Should().BeTrue();
            result.Content.Should().Contain("Initial line 1");
            result.Content.Should().Contain("Initial line 2");
            result.Content.Should().Contain("Additional line");
            result.Metadata.WasModified.Should().BeTrue();
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task EditAsync_WithAnsiEscapeSequences_SanitizesContent()
    {
        // Arrange
        var sanitizer = new InputSanitizer();
        var logger = NullLogger<StreamBasedTextEditor>.Instance;
        var editor = new StreamBasedTextEditor(sanitizer, logger);
        var config = EditorConfiguration.Default with { SanitizeInput = true };

        // Simulate input with ANSI codes
        var inputLines = new[]
        {
            "Normal text",
            "\x1B[31mRed text\x1B[0m", // ANSI escape sequence
            null, // EOF
            "y"   // Save
        };

        using var inputReader = new StringReader(string.Join(Environment.NewLine, inputLines));
        using var outputWriter = new StringWriter();

        var originalIn = Console.In;
        var originalOut = Console.Out;

        try
        {
            Console.SetIn(inputReader);
            Console.SetOut(outputWriter);

            // Act
            var result = await editor.EditAsync(configuration: config);

            // Assert
            result.Should().NotBeNull();
            result.IsSaved.Should().BeTrue();
            result.Content.Should().Contain("Normal text");
            result.Content.Should().Contain("Red text");
            result.Content.Should().NotContain("\x1B[31m"); // ANSI should be stripped
            result.Content.Should().NotContain("\x1B[0m");
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task EditAsync_WithEmptyInput_ReturnsEmptyContent()
    {
        // Arrange
        var sanitizer = new InputSanitizer();
        var logger = NullLogger<StreamBasedTextEditor>.Instance;
        var editor = new StreamBasedTextEditor(sanitizer, logger);

        // Simulate immediate EOF (no input)
        // In piped mode, auto-saves even empty content
        using var inputReader = new StringReader(string.Empty);
        using var outputWriter = new StringWriter();

        var originalIn = Console.In;
        var originalOut = Console.Out;

        try
        {
            Console.SetIn(inputReader);
            Console.SetOut(outputWriter);

            // Act
            var result = await editor.EditAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSaved.Should().BeTrue();
            result.Content.Should().BeEmpty();
            result.Metadata.CharacterCount.Should().Be(0);
            // Note: Even empty content counts as 1 line (an empty line)
            result.Metadata.LineCount.Should().Be(1);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }
}

