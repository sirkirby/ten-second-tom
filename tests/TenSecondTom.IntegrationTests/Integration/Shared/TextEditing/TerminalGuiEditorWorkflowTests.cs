using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Shared.TextEditing.Models;
using TenSecondTom.Shared.TextEditing.Services;

namespace TenSecondTom.IntegrationTests.Integration.TextEditingWorkflows;

/// <summary>
/// Integration tests for TerminalGuiTextEditor workflows and edge cases.
/// </summary>
/// <remarks>
/// Note: These tests simulate behavior but cannot actually invoke Terminal.Gui
/// in a CI environment. For full interactive testing, see MANUAL_TESTS.md.
/// 
/// These tests verify:
/// - Input sanitization integration
/// - Large content handling
/// - Configuration scenarios
/// - Error handling paths
/// </remarks>
public sealed class TerminalGuiEditorWorkflowTests
{
    private readonly InputSanitizer _sanitizer;
    private readonly Mock<ILogger<TerminalGuiTextEditor>> _mockLogger;

    public TerminalGuiEditorWorkflowTests()
    {
        _sanitizer = new InputSanitizer();
        _mockLogger = new Mock<ILogger<TerminalGuiTextEditor>>();
    }

    /// <summary>
    /// T034: Test large paste operations (5,000 characters)
    /// </summary>
    /// <remarks>
    /// This test verifies that the InputSanitizer can handle large content
    /// efficiently. The actual Terminal.Gui paste behavior is tested manually
    /// in MANUAL_TESTS.md TC-014 (10,000 chars).
    /// 
    /// Performance target: Less than 200ms for 5,000 character content (SC-002)
    /// </remarks>
    [Fact]
    public void Sanitize_LargePasteContent_CompletesQuickly()
    {
        // Arrange: Create 5,000 character content with blank lines and formatting
        var paragraph = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                       "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. " +
                       "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.\n\n";
        
        // Repeat to get ~5,000 characters
        var largeContent = string.Concat(Enumerable.Repeat(paragraph, 30)); // ~5,100 chars
        largeContent.Length.Should().BeGreaterThan(5000, "test data should be at least 5,000 characters");

        // Act: Measure sanitization time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = _sanitizer.Sanitize(largeContent);
        stopwatch.Stop();

        // Assert: Performance
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200, 
            "sanitization should complete in <200ms per SC-002");
        
        // Assert: Content integrity
        result.Content.Should().Be(largeContent, "clean content should be preserved");
        result.WasSanitized.Should().BeFalse("no ANSI codes to remove");
        result.Content.Should().Contain("\n\n", "blank lines should be preserved");
    }

    /// <summary>
    /// T034: Test large paste with ANSI codes (worst case performance)
    /// </summary>
    [Fact]
    public void Sanitize_LargePasteWithAnsiCodes_StripsCodesQuickly()
    {
        // Arrange: Create 5,000 character content with embedded ANSI codes
        var coloredParagraph = "\x1B[31mRed text here\x1B[0m normal text " +
                              "\x1B[1m\x1B[32mbold green\x1B[0m more text.\n";
        
        var largeContentWithAnsi = string.Concat(Enumerable.Repeat(coloredParagraph, 100)); // ~5,000 chars
        largeContentWithAnsi.Length.Should().BeGreaterThan(5000);

        // Act: Measure sanitization time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = _sanitizer.Sanitize(largeContentWithAnsi);
        stopwatch.Stop();

        // Assert: Performance (allow slightly more time due to regex processing)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300, 
            "ANSI stripping should complete in <300ms for 5,000 chars");
        
        // Assert: Content cleaned
        result.Content.Should().NotContain("\x1B", "all ANSI escape sequences should be removed");
        result.WasSanitized.Should().BeTrue("ANSI codes were present and removed");
        result.RemovedCount.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// T032: Test explicit blank line preservation in multi-paragraph content
    /// </summary>
    [Fact]
    public void Sanitize_MultiParagraphWithBlankLines_PreservesFormatting()
    {
        // Arrange: 5-paragraph content with blank lines between
        var multiParagraphContent = 
            "Paragraph one with some content here.\n" +
            "It has multiple lines in it.\n" +
            "\n" +  // Blank line
            "Paragraph two is separate.\n" +
            "\n" +  // Blank line
            "Paragraph three.\n" +
            "\n" +  // Blank line
            "Paragraph four continues.\n" +
            "\n" +  // Blank line
            "Paragraph five is the last one.";

        // Act
        var result = _sanitizer.Sanitize(multiParagraphContent);

        // Assert: All blank lines preserved
        result.Content.Should().Be(multiParagraphContent);
        result.Content.Split("\n\n").Length.Should().Be(5, 
            "should have 5 paragraphs separated by blank lines");
        result.WasSanitized.Should().BeFalse();
    }

    /// <summary>
    /// Test configuration with custom settings
    /// </summary>
    [Fact]
    public void EditorConfiguration_CustomSettings_WorkCorrectly()
    {
        // Arrange
        var customConfig = new EditorConfiguration
        {
            Title = "Custom Editor Title",
            ShowHints = true,
            SanitizeInput = true,
            MaxContentLength = 1000,
            MaxLineCount = 100,
            PreviewLineLimit = 5
        };

        // Assert: Configuration properties
        customConfig.Title.Should().Be("Custom Editor Title");
        customConfig.ShowHints.Should().BeTrue();
        customConfig.SanitizeInput.Should().BeTrue();
        customConfig.MaxContentLength.Should().Be(1000);
        customConfig.MaxLineCount.Should().Be(100);
        customConfig.PreviewLineLimit.Should().Be(5);
    }

    /// <summary>
    /// Test default configuration values
    /// </summary>
    [Fact]
    public void EditorConfiguration_Default_HasReasonableValues()
    {
        // Arrange & Act
        var defaultConfig = EditorConfiguration.Default;

        // Assert: Sensible defaults
        defaultConfig.ShowHints.Should().BeTrue("hints should be shown by default");
        defaultConfig.SanitizeInput.Should().BeTrue("input should be sanitized by default");
        defaultConfig.MaxContentLength.Should().Be(50_000, "should support up to 50K characters");
        defaultConfig.MaxLineCount.Should().Be(1_000, "should support up to 1K lines");
        defaultConfig.PreviewLineLimit.Should().Be(10, "preview should show first 10 lines");
    }

    /// <summary>
    /// Test content with various Unicode characters (emoji, accents, RTL)
    /// </summary>
    [Fact]
    public void Sanitize_UnicodeContent_PreservesAllCharacters()
    {
        // Arrange: Mix of emoji, accented characters, and RTL text
        var unicodeContent = 
            "Hello 👋 World 🌍\n" +
            "Café, naïve, résumé\n" +
            "日本語 (Japanese)\n" +
            "العربية (Arabic)\n" +
            "Emoji: 😊 🎉 🚀 ✨";

        // Act
        var result = _sanitizer.Sanitize(unicodeContent);

        // Assert: All Unicode preserved
        result.Content.Should().Be(unicodeContent);
        result.Content.Should().Contain("👋");
        result.Content.Should().Contain("🌍");
        result.Content.Should().Contain("é");
        result.Content.Should().Contain("日本語");
        result.Content.Should().Contain("العربية");
        result.WasSanitized.Should().BeFalse();
    }

    /// <summary>
    /// Test that consecutive blank lines are preserved (important for formatting)
    /// </summary>
    [Fact]
    public void Sanitize_ConsecutiveBlankLines_PreservesAll()
    {
        // Arrange: Content with multiple consecutive blank lines
        var contentWithMultipleBlankLines = 
            "Paragraph 1\n" +
            "\n" +
            "\n" +
            "\n" +
            "Paragraph 2 after 3 blank lines";

        // Act
        var result = _sanitizer.Sanitize(contentWithMultipleBlankLines);

        // Assert: All blank lines preserved
        result.Content.Should().Be(contentWithMultipleBlankLines);
        result.Content.Should().Contain("\n\n\n\n", "three consecutive blank lines should be preserved");
    }

    /// <summary>
    /// Test content with tabs and mixed whitespace
    /// </summary>
    [Fact]
    public void Sanitize_TabsAndMixedWhitespace_PreservesFormatting()
    {
        // Arrange: Code-like content with tabs and indentation
        var codeContent = 
            "function example() {\n" +
            "\tif (condition) {\n" +
            "\t\treturn true;\n" +
            "\t}\n" +
            "\treturn false;\n" +
            "}";

        // Act
        var result = _sanitizer.Sanitize(codeContent);

        // Assert: Tabs preserved
        result.Content.Should().Be(codeContent);
        result.Content.Should().Contain("\t", "tabs should be preserved");
    }

    /// <summary>
    /// Test empty content handling
    /// </summary>
    [Fact]
    public void Sanitize_EmptyContent_HandlesGracefully()
    {
        // Act
        var result = _sanitizer.Sanitize(string.Empty);

        // Assert
        result.Content.Should().BeEmpty();
        result.WasSanitized.Should().BeFalse();
        result.OriginalLength.Should().Be(0);
        result.RemovedCount.Should().Be(0);
    }

    /// <summary>
    /// T038: Test pre-filled content workflow for editing existing entries
    /// </summary>
    /// <remarks>
    /// This test demonstrates the reusability pattern for future features
    /// like editing existing entries from /search results.
    /// 
    /// The editor accepts initialContent and allows users to modify it,
    /// simulating the workflow: search → select entry → edit → save
    /// </remarks>
    [Fact]
    public void PrefilledContent_CanBeEditedAndSaved()
    {
        // Arrange: Simulate existing entry content
        var existingContent = "Original entry from yesterday.\nIt has multiple lines.\nThis is the third line.";
        
        // Verify content is substantial enough for the test
        existingContent.Length.Should().BeGreaterThan(50);
        existingContent.Split('\n').Length.Should().Be(3);

        // Act: Simulate editing - sanitizer would process any user modifications
        var result = _sanitizer.Sanitize(existingContent);

        // Assert: Content passes through unchanged if no ANSI codes
        result.Content.Should().Be(existingContent);
        result.WasSanitized.Should().BeFalse();
        
        // Verify multi-line structure preserved
        result.Content.Split('\n').Length.Should().Be(3);
        result.Content.Should().Contain("Original entry");
        result.Content.Should().Contain("multiple lines");
        result.Content.Should().Contain("third line");
    }

    /// <summary>
    /// T038: Test pre-filled content with user modifications
    /// </summary>
    [Fact]
    public void PrefilledContent_WithModifications_SanitizesCorrectly()
    {
        // Arrange: Existing content with user adding ANSI codes (e.g., pasted from terminal)
        var existingContent = "Original line 1\nOriginal line 2";
        var modifiedContent = existingContent + "\n\x1B[31mNew line with color\x1B[0m";
        
        // Act: Sanitize the modified content
        var result = _sanitizer.Sanitize(modifiedContent);

        // Assert: ANSI codes stripped but all text preserved
        result.Content.Should().Contain("Original line 1");
        result.Content.Should().Contain("Original line 2");
        result.Content.Should().Contain("New line with color");
        result.Content.Should().NotContain("\x1B");
        result.WasSanitized.Should().BeTrue();
    }

    /// <summary>
    /// T038: Test that TextEditingSession correctly tracks initial content
    /// </summary>
    [Fact]
    public void TextEditingSession_WithInitialContent_TracksCorrectly()
    {
        // Arrange
        var initialContent = "Pre-filled content for editing";
        
        // Act: Create session with initial content (simulates editor startup)
        var session = new TextEditingSession(initialContent);
        
        // Assert: Session initializes correctly
        session.SessionId.Should().NotBeEmpty();
        session.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.InitialContent.Should().Be(initialContent);
        session.CurrentContent.Should().Be(initialContent, "content should start as initial content");
        session.HasChanges.Should().BeFalse("no changes yet");
        
        // Simulate user edits
        var modifiedContent = initialContent + "\nUser added this line.";
        session.UpdateContent(modifiedContent);
        session.HasChanges.Should().BeTrue("content was modified");
        
        session.Complete(EditorOutcome.Saved);
        
        // Verify session completed successfully
        session.IsActive.Should().BeFalse("session should be completed");
        session.Outcome.Should().Be(EditorOutcome.Saved);
        session.EndedAt.Should().NotBeNull();
    }

    /// <summary>
    /// T038: Test EditorConfiguration works with pre-filled content scenarios
    /// </summary>
    [Fact]
    public void EditorConfiguration_WithPrefilledContent_RespectsLimits()
    {
        // Arrange: Custom configuration for editing existing entries
        var config = new EditorConfiguration
        {
            Title = "Edit Entry",
            MaxContentLength = 1000,
            MaxLineCount = 50,
            PreviewLineLimit = 5,
            SanitizeInput = true,
            ShowHints = true
        };

        // Assert: Configuration suitable for edit scenarios
        config.Title.Should().Be("Edit Entry");
        config.MaxContentLength.Should().Be(1000);
        config.MaxLineCount.Should().Be(50);
        config.SanitizeInput.Should().BeTrue("editing scenarios need sanitization");
        config.ShowHints.Should().BeTrue("users need hints when editing");
    }
}

