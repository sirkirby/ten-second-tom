using FluentAssertions;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Shared.Models;

/// <summary>
/// Unit tests for the TemplateMetadata record.
/// Tests verify validation rules for template metadata including required fields,
/// length constraints, and enum validation.
/// </summary>
public sealed class TemplateMetadataTests
{
    [Fact]
    public void Validate_WithValidMetadata_ShouldReturnSuccess()
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title"
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithAllValidOptionalFields_ShouldReturnSuccess()
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Weekly,
            Title = "Complete Template",
            Description = "A comprehensive template description",
            Version = "1.0.0",
            Author = "John Doe",
            CreatedDate = DateTime.UtcNow,
            Tags = new[] { "productivity", "daily", "journal" }
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithMissingTitle_ShouldReturnFailure(string? title)
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = title!
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Title is required");
    }

    [Fact]
    public void Validate_WithTitleTooLong_ShouldReturnFailure()
    {
        // Arrange
        var longTitle = new string('A', 201); // 201 characters
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = longTitle
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Title must be 200 characters or less");
    }

    [Fact]
    public void Validate_WithTitleExactly200Characters_ShouldReturnSuccess()
    {
        // Arrange
        var maxTitle = new string('A', 200); // Exactly 200 characters
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = maxTitle
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithInvalidTemplateType_ShouldReturnFailure()
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            TemplateType = (TemplateType)999, // Invalid enum value
            Title = "Valid Title"
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Invalid template type: 999");
    }

    [Fact]
    public void Validate_WithDescriptionTooLong_ShouldReturnFailure()
    {
        // Arrange
        var longDescription = new string('A', 501); // 501 characters
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Description = longDescription
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Description must be 500 characters or less");
    }

    [Fact]
    public void Validate_WithDescriptionExactly500Characters_ShouldReturnSuccess()
    {
        // Arrange
        var maxDescription = new string('A', 500); // Exactly 500 characters
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Description = maxDescription
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithAuthorTooLong_ShouldReturnFailure()
    {
        // Arrange
        var longAuthor = new string('A', 101); // 101 characters
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Author = longAuthor
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Author must be 100 characters or less");
    }

    [Fact]
    public void Validate_WithAuthorExactly100Characters_ShouldReturnSuccess()
    {
        // Arrange
        var maxAuthor = new string('A', 100); // Exactly 100 characters
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Author = maxAuthor
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithTooManyTags_ShouldReturnFailure()
    {
        // Arrange
        var tooManyTags = Enumerable.Range(1, 21).Select(i => $"tag{i}").ToArray(); // 21 tags
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Tags = tooManyTags
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Maximum 20 tags allowed");
    }

    [Fact]
    public void Validate_WithExactly20Tags_ShouldReturnSuccess()
    {
        // Arrange
        var maxTags = Enumerable.Range(1, 20).Select(i => $"tag{i}").ToArray(); // Exactly 20 tags
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Tags = maxTags
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithTagTooLong_ShouldReturnFailure()
    {
        // Arrange
        var longTag = new string('A', 51); // 51 characters
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Tags = new[] { "valid-tag", longTag }
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Each tag must be 50 characters or less");
    }

    [Fact]
    public void Validate_WithTagExactly50Characters_ShouldReturnSuccess()
    {
        // Arrange
        var maxTag = new string('A', 50); // Exactly 50 characters
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Tags = new[] { maxTag }
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.1.3")]
    [InlineData("10.20.30")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("latest")]
    public void Validate_WithAnyVersion_ShouldReturnSuccess(string version)
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Version = version
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        // Version format is advisory only - should not fail validation
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            TemplateType = (TemplateType)999, // Invalid enum
            Title = new string('A', 201), // Too long
            Description = new string('B', 501), // Too long
            Author = new string('C', 101), // Too long
            Tags = Enumerable.Range(1, 21).Select(i => new string('D', 51)).ToArray() // Too many, each too long
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("Invalid template type"));
        errors.Should().Contain("Title must be 200 characters or less");
        errors.Should().Contain("Description must be 500 characters or less");
        errors.Should().Contain("Author must be 100 characters or less");
        errors.Should().Contain("Maximum 20 tags allowed");
        errors.Should().Contain("Each tag must be 50 characters or less");
    }

    [Fact]
    public void Validate_WithNullOptionalFields_ShouldReturnSuccess()
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Description = null,
            Version = null,
            Author = null,
            CreatedDate = null,
            Tags = null
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithEmptyTagsArray_ShouldReturnSuccess()
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            TemplateType = TemplateType.Daily,
            Title = "Valid Title",
            Tags = Array.Empty<string>()
        };

        // Act
        var errors = metadata.Validate();

        // Assert
        errors.Should().BeEmpty();
    }
}
