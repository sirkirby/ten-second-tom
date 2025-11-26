using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Shell.Models;
using TenSecondTom.Features.Shell.Services;
using Xunit;

namespace TenSecondTom.Tests.Features.Shell;

/// <summary>
/// Unit tests for EnhancedInputReader with mocked IConsoleKeyReader for testability.
/// </summary>
public sealed class EnhancedInputReaderTests
{
    private readonly Mock<IConsoleKeyReader> _mockKeyReader;
    private readonly Mock<IAutocompleteEngine> _mockAutocompleteEngine;
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<ILogger<EnhancedInputReader>> _mockLogger;

    public EnhancedInputReaderTests()
    {
        _mockKeyReader = new Mock<IConsoleKeyReader>();
        _mockAutocompleteEngine = new Mock<IAutocompleteEngine>();
        _mockSessionManager = new Mock<ISessionManager>();
        _mockLogger = new Mock<ILogger<EnhancedInputReader>>();

        // Default setup: interactive terminal available
        _mockKeyReader.Setup(k => k.IsInputRedirected).Returns(false);

        // Setup output methods for testing (no-op implementations)
        _mockKeyReader.Setup(k => k.WindowWidth).Returns(120);
        _mockKeyReader.Setup(k => k.CursorTop).Returns(0);
        _mockKeyReader.Setup(k => k.Write(It.IsAny<string>()));
        _mockKeyReader.Setup(k => k.Write(It.IsAny<char>()));
        _mockKeyReader.Setup(k => k.WriteLine());
        _mockKeyReader.Setup(k => k.SetCursorPosition(It.IsAny<int>(), It.IsAny<int>()));
        _mockKeyReader.Setup(k => k.WriteMarkup(It.IsAny<string>()));
    }

    private EnhancedInputReader CreateReader() => new(
        _mockKeyReader.Object,
        _mockAutocompleteEngine.Object,
        _mockSessionManager.Object,
        _mockLogger.Object);

    #region IsAvailable Tests

    [Fact]
    public void IsAvailable_InteractiveTerminal_ReturnsTrue()
    {
        // Arrange
        _mockKeyReader.Setup(k => k.IsInputRedirected).Returns(false);
        var reader = CreateReader();

        // Act
        var result = reader.IsAvailable();

        // Assert
        result.Should().BeTrue("interactive terminal should be available");
    }

    [Fact]
    public void IsAvailable_RedirectedInput_ReturnsFalse()
    {
        // Arrange
        _mockKeyReader.Setup(k => k.IsInputRedirected).Returns(true);
        var reader = CreateReader();

        // Act
        var result = reader.IsAvailable();

        // Assert
        result.Should().BeFalse("redirected input should not be available for enhanced reading");
    }

    #endregion

    #region User Story 1: Escape Key Tests

    [Fact]
    public async Task ReadInputAsync_EscapeKey_ReturnsNull()
    {
        // Arrange
        var reader = CreateReader();
        SetupKeySequence(CreateEscapeKey());

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull("Escape key should cancel input and return null");
    }

    [Fact]
    public async Task ReadInputAsync_CtrlBracket_ReturnsNull()
    {
        // Arrange
        var reader = CreateReader();
        // Ctrl+[ sends ASCII 27 (same as Escape)
        SetupKeySequence(new ConsoleKeyInfo('\x1b', ConsoleKey.Oem4, false, false, true));

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull("Ctrl+[ (ASCII 27) should cancel input and return null");
    }

    [Fact]
    public async Task ReadInputAsync_EscapeAfterTyping_ReturnsNull()
    {
        // Arrange
        var reader = CreateReader();
        SetupKeySequence(
            CreateCharacterKey('/', ConsoleKey.Oem2),
            CreateCharacterKey('h', ConsoleKey.H),
            CreateCharacterKey('e', ConsoleKey.E),
            CreateCharacterKey('l', ConsoleKey.L),
            CreateEscapeKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull("Escape should cancel input even after typing");
    }

    #endregion

    #region User Story 2: Tab Autocomplete Tests

    [Fact]
    public async Task ReadInputAsync_TabKey_CompletesAndCycles()
    {
        // Arrange
        var reader = CreateReader();
        var suggestions = new List<AutocompleteSuggestion>
        {
            new() { CommandName = "/record", HelpText = "Record audio", MatchScore = 95 },
            new() { CommandName = "/recording", HelpText = "View recordings", MatchScore = 90 }
        };
        _mockAutocompleteEngine.Setup(a => a.GetSuggestions("/rec")).Returns(suggestions);

        SetupKeySequence(
            CreateCharacterKey('/', ConsoleKey.Oem2),
            CreateCharacterKey('r', ConsoleKey.R),
            CreateCharacterKey('e', ConsoleKey.E),
            CreateCharacterKey('c', ConsoleKey.C),
            CreateTabKey(),           // First Tab - selects first suggestion "/record"
            CreateTabKey(),           // Second Tab - cycles to "/recording"
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("/recording", "Tab should cycle through suggestions");
        _mockAutocompleteEngine.Verify(a => a.GetSuggestions("/rec"), Times.Once);
    }

    [Fact]
    public async Task ReadInputAsync_TabKeyNoMatches_NoChange()
    {
        // Arrange
        var reader = CreateReader();
        _mockAutocompleteEngine.Setup(a => a.GetSuggestions("/xyz"))
            .Returns(Array.Empty<AutocompleteSuggestion>());

        SetupKeySequence(
            CreateCharacterKey('/', ConsoleKey.Oem2),
            CreateCharacterKey('x', ConsoleKey.X),
            CreateCharacterKey('y', ConsoleKey.Y),
            CreateCharacterKey('z', ConsoleKey.Z),
            CreateTabKey(),           // Tab with no matches - should do nothing
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("/xyz", "Tab with no matches should leave buffer unchanged");
    }

    [Fact]
    public async Task ReadInputAsync_ShiftTab_CyclesBackward()
    {
        // Arrange
        var reader = CreateReader();
        var suggestions = new List<AutocompleteSuggestion>
        {
            new() { CommandName = "/config", HelpText = "Configuration", MatchScore = 95 },
            new() { CommandName = "/connect", HelpText = "Connect", MatchScore = 90 }
        };
        _mockAutocompleteEngine.Setup(a => a.GetSuggestions("/co")).Returns(suggestions);

        SetupKeySequence(
            CreateCharacterKey('/', ConsoleKey.Oem2),
            CreateCharacterKey('c', ConsoleKey.C),
            CreateCharacterKey('o', ConsoleKey.O),
            CreateTabKey(),                    // First Tab - selects "/config"
            CreateTabKey(),                    // Second Tab - cycles to "/connect"
            CreateShiftTabKey(),               // Shift+Tab - cycles back to "/config"
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("/config", "Shift+Tab should cycle backward through suggestions");
    }

    [Fact]
    public async Task ReadInputAsync_TabWithoutSlash_NoAutoComplete()
    {
        // Arrange
        var reader = CreateReader();

        SetupKeySequence(
            CreateCharacterKey('h', ConsoleKey.H),
            CreateCharacterKey('e', ConsoleKey.E),
            CreateCharacterKey('l', ConsoleKey.L),
            CreateCharacterKey('l', ConsoleKey.L),
            CreateCharacterKey('o', ConsoleKey.O),
            CreateTabKey(),           // Tab without '/' prefix - should do nothing
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("hello", "Tab without '/' prefix should not trigger autocomplete");
        _mockAutocompleteEngine.Verify(a => a.GetSuggestions(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region User Story 3: History Navigation Tests

    [Fact]
    public async Task ReadInputAsync_ArrowUpDown_NavigatesHistory()
    {
        // Arrange
        var reader = CreateReader();
        var history = new List<CommandHistoryEntry>
        {
            new() { SequenceNumber = 1, Command = "/help", WasSuccessful = true },
            new() { SequenceNumber = 2, Command = "/config", WasSuccessful = true },
            new() { SequenceNumber = 3, Command = "/search test", WasSuccessful = true }
        };
        _mockSessionManager.Setup(s => s.GetHistory()).Returns(history);

        SetupKeySequence(
            CreateArrowUpKey(),       // Arrow Up - shows "/search test" (newest)
            CreateArrowUpKey(),       // Arrow Up - shows "/config"
            CreateArrowDownKey(),     // Arrow Down - back to "/search test"
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("/search test", "Arrow navigation should navigate through history");
    }

    [Fact]
    public async Task ReadInputAsync_ArrowUpEmptyHistory_NoOp()
    {
        // Arrange
        var reader = CreateReader();
        _mockSessionManager.Setup(s => s.GetHistory()).Returns(Array.Empty<CommandHistoryEntry>());

        SetupKeySequence(
            CreateArrowUpKey(),       // Arrow Up with empty history - should do nothing
            CreateCharacterKey('/', ConsoleKey.Oem2),
            CreateCharacterKey('h', ConsoleKey.H),
            CreateCharacterKey('e', ConsoleKey.E),
            CreateCharacterKey('l', ConsoleKey.L),
            CreateCharacterKey('p', ConsoleKey.P),
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("/help", "Arrow Up with empty history should be no-op");
    }

    [Fact]
    public async Task ReadInputAsync_ArrowDownAtNewest_ReturnsToEmpty()
    {
        // Arrange
        var reader = CreateReader();
        var history = new List<CommandHistoryEntry>
        {
            new() { SequenceNumber = 1, Command = "/help", WasSuccessful = true }
        };
        _mockSessionManager.Setup(s => s.GetHistory()).Returns(history);

        SetupKeySequence(
            CreateCharacterKey('/', ConsoleKey.Oem2),
            CreateCharacterKey('x', ConsoleKey.X),  // Type "/x"
            CreateArrowUpKey(),       // Arrow Up - shows "/help"
            CreateArrowDownKey(),     // Arrow Down at newest - returns to saved buffer "/x"
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("/x", "Arrow Down at newest should return to original buffer");
    }

    [Fact]
    public async Task ReadInputAsync_ArrowUpAtOldest_StaysAtOldest()
    {
        // Arrange
        var reader = CreateReader();
        var history = new List<CommandHistoryEntry>
        {
            new() { SequenceNumber = 1, Command = "/first", WasSuccessful = true },
            new() { SequenceNumber = 2, Command = "/second", WasSuccessful = true }
        };
        _mockSessionManager.Setup(s => s.GetHistory()).Returns(history);

        SetupKeySequence(
            CreateArrowUpKey(),       // Shows "/second" (newest)
            CreateArrowUpKey(),       // Shows "/first" (oldest)
            CreateArrowUpKey(),       // Still at "/first" (no wrap-around)
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("/first", "Arrow Up at oldest should stay at oldest (no wrap-around)");
    }

    #endregion

    #region Basic Input Tests

    [Fact]
    public async Task ReadInputAsync_EnterKey_ReturnsBuffer()
    {
        // Arrange
        var reader = CreateReader();
        SetupKeySequence(
            CreateCharacterKey('h', ConsoleKey.H),
            CreateCharacterKey('e', ConsoleKey.E),
            CreateCharacterKey('l', ConsoleKey.L),
            CreateCharacterKey('l', ConsoleKey.L),
            CreateCharacterKey('o', ConsoleKey.O),
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("hello", "Enter should submit the buffer content");
    }

    [Fact]
    public async Task ReadInputAsync_BackspaceKey_DeletesCharacterBeforeCursor()
    {
        // Arrange
        var reader = CreateReader();
        SetupKeySequence(
            CreateCharacterKey('h', ConsoleKey.H),
            CreateCharacterKey('i', ConsoleKey.I),
            CreateCharacterKey('x', ConsoleKey.X),
            CreateBackspaceKey(),
            CreateEnterKey()
        );

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().Be("hi", "Backspace should delete the character before cursor");
    }

    [Fact]
    public async Task ReadInputAsync_EmptyInput_ReturnsEmptyString()
    {
        // Arrange
        var reader = CreateReader();
        SetupKeySequence(CreateEnterKey());

        // Act
        var result = await reader.ReadInputAsync(CancellationToken.None);

        // Assert
        result.Should().BeEmpty("Enter on empty buffer should return empty string");
    }

    #endregion

    #region Helper Methods

    private void SetupKeySequence(params ConsoleKeyInfo[] keys)
    {
        var keyIndex = 0;

        // KeyAvailable returns true only when there are keys remaining
        _mockKeyReader.Setup(k => k.KeyAvailable)
            .Returns(() => keyIndex < keys.Length);

        // ReadKey returns the next key in sequence
        _mockKeyReader.Setup(k => k.ReadKey(It.IsAny<bool>()))
            .Returns(() =>
            {
                if (keyIndex >= keys.Length)
                    throw new InvalidOperationException("No more keys in sequence");
                return keys[keyIndex++];
            });
    }

    private static ConsoleKeyInfo CreateCharacterKey(char c, ConsoleKey key) =>
        new(c, key, false, false, false);

    private static ConsoleKeyInfo CreateEnterKey() =>
        new('\r', ConsoleKey.Enter, false, false, false);

    private static ConsoleKeyInfo CreateEscapeKey() =>
        new('\x1b', ConsoleKey.Escape, false, false, false);

    private static ConsoleKeyInfo CreateTabKey() =>
        new('\t', ConsoleKey.Tab, false, false, false);

    private static ConsoleKeyInfo CreateShiftTabKey() =>
        new('\t', ConsoleKey.Tab, true, false, false);

    private static ConsoleKeyInfo CreateBackspaceKey() =>
        new('\b', ConsoleKey.Backspace, false, false, false);

    private static ConsoleKeyInfo CreateArrowUpKey() =>
        new('\0', ConsoleKey.UpArrow, false, false, false);

    private static ConsoleKeyInfo CreateArrowDownKey() =>
        new('\0', ConsoleKey.DownArrow, false, false, false);

    #endregion
}
