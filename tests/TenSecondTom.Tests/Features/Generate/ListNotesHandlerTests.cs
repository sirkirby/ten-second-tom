using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.IO.Abstractions.TestingHelpers;
using TenSecondTom.Features.Generate;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Tests.Features.Generate;

public sealed class ListNotesHandlerTests
{
    private readonly Mock<IOptions<StorageOptions>> _storageOptions = new();
    private readonly string _rootDirectory = "/test/memory";

    public ListNotesHandlerTests()
    {
        _storageOptions
            .Setup(o => o.Value)
            .Returns(new StorageOptions { RootDirectory = _rootDirectory });
    }

    [Fact]
    public async Task Handle_ConvertsUtcFrontMatterToLocalTime()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var notesDirectory = $"{_rootDirectory}/{DirectoryNames.Note}";
        fileSystem.AddDirectory(notesDirectory);

        var timestampUtc = new DateTimeOffset(2025, 10, 24, 8, 30, 0, TimeSpan.Zero);
        var content = $@"---
date: {timestampUtc:O}
---
Note body";

        fileSystem.AddFile($"{notesDirectory}/note-1.md", new MockFileData(content));

        var handler = new ListNotes.Handler(
            fileSystem,
            _storageOptions.Object,
            Mock.Of<ILogger<ListNotes.Handler>>());

        // Act
        var result = await handler.Handle(new ListNotes.Query(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var note = result.Value.Should().ContainSingle().Subject;
        note.LastModified.Should().Be(timestampUtc.ToLocalTime());
    }
}
