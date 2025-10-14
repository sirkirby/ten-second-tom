using FluentAssertions;
using FluentValidation.TestHelper;
using TenSecondTom.Shared.TextEditing.Models;
using TenSecondTom.Shared.TextEditing.Validation;

namespace TenSecondTom.Tests.Unit.Shared.TextEditing.Validation;

public sealed class EditorConfigurationValidatorTests
{
    private readonly EditorConfigurationValidator _validator = new();

    [Fact]
    public void Validate_DefaultConfiguration_IsValid()
    {
        // Arrange
        var config = EditorConfiguration.Default;

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_MaxContentLength_MustBePositive(int invalidLength)
    {
        // Arrange
        var config = new EditorConfiguration { MaxContentLength = invalidLength };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxContentLength);
    }

    [Fact]
    public void Validate_MaxContentLength_MustNotExceed1MB()
    {
        // Arrange
        var config = new EditorConfiguration { MaxContentLength = 1_000_001 }; // 1 char over limit

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxContentLength);
    }

    [Fact]
    public void Validate_MaxContentLength_MustBeAtLeast100Chars()
    {
        // Arrange
        var config = new EditorConfiguration { MaxContentLength = 50 }; // Too small to be useful

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxContentLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxLineCount_MustBePositive(int invalidCount)
    {
        // Arrange
        var config = new EditorConfiguration { MaxLineCount = invalidCount };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxLineCount);
    }

    [Fact]
    public void Validate_MaxLineCount_MustBeAtLeast10Lines()
    {
        // Arrange
        var config = new EditorConfiguration { MaxLineCount = 5 }; // Too small

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxLineCount);
    }

    [Fact]
    public void Validate_MaxLineCount_MustNotExceed100K()
    {
        // Arrange
        var config = new EditorConfiguration { MaxLineCount = 100_001 }; // Over limit

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxLineCount);
    }

    [Fact]
    public void Validate_PreviewLineLimit_ZeroIsValid()
    {
        // Arrange (0 means "show all lines")
        var config = new EditorConfiguration { PreviewLineLimit = 0 };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PreviewLineLimit);
    }

    [Fact]
    public void Validate_PreviewLineLimit_CannotBeNegative()
    {
        // Arrange
        var config = new EditorConfiguration { PreviewLineLimit = -1 };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PreviewLineLimit);
    }

    [Fact]
    public void Validate_PreviewLineLimit_CannotExceed1000()
    {
        // Arrange
        var config = new EditorConfiguration { PreviewLineLimit = 1001 };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PreviewLineLimit);
    }

    [Fact]
    public void Validate_Title_CanBeNull()
    {
        // Arrange
        var config = new EditorConfiguration { Title = null };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_Title_CannotExceed200Chars()
    {
        // Arrange
        var longTitle = new string('A', 201); // 201 characters
        var config = new EditorConfiguration { Title = longTitle };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_Title_200CharsIsValid()
    {
        // Arrange
        var maxTitle = new string('A', 200); // Exactly 200 characters
        var config = new EditorConfiguration { Title = maxTitle };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_ReasonableConfiguration_PassesAllChecks()
    {
        // Arrange: Typical use case
        var config = new EditorConfiguration
        {
            MaxContentLength = 5000,
            MaxLineCount = 100,
            PreviewLineLimit = 10,
            ShowHints = true,
            SanitizeInput = true,
            Title = "Enter your journal entry:"
        };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EdgeCaseMinimums_AreValid()
    {
        // Arrange: Minimum valid values
        var config = new EditorConfiguration
        {
            MaxContentLength = 100,  // Minimum allowed
            MaxLineCount = 10,       // Minimum allowed
            PreviewLineLimit = 0     // Show all
        };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EdgeCaseMaximums_AreValid()
    {
        // Arrange: Maximum valid values
        var config = new EditorConfiguration
        {
            MaxContentLength = 1_000_000,  // 1MB
            MaxLineCount = 100_000,        // 100K lines
            PreviewLineLimit = 1000        // Max preview
        };

        // Act
        var result = _validator.TestValidate(config);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

}

