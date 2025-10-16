using FluentAssertions;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Unit.Models;

/// <summary>
/// Unit tests for PromptTemplate and related types.
/// Tests template structure, validation, and variable substitution patterns.
/// </summary>
public sealed class PromptTemplateTests
{
    [Fact]
    public void Create_WithValidTemplate_ShouldSucceed()
    {
        // Arrange & Act
        var template = new PromptTemplate
        {
            TemplateId = "daily-summary-v1",
            Content = "Summarize the following day: {{USER_INPUT}}",
            TemplateType = TemplateType.Daily,
            Source = TemplateSource.Embedded
        };

        // Assert
        template.Should().NotBeNull();
        template.TemplateId.Should().Be("daily-summary-v1");
        template.Content.Should().Contain("{{USER_INPUT}}");
        template.TemplateType.Should().Be(TemplateType.Daily);
    }

    [Fact]
    public void TemplateId_ShouldFollowKebabCaseConvention()
    {
        // Arrange & Act
        var template = new PromptTemplate
        {
            TemplateId = "weekly-review-v2",
            Content = "Content",
            TemplateType = TemplateType.Weekly,
            Source = TemplateSource.Embedded
        };

        // Assert
        template.TemplateId.Should().MatchRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$");
    }

    [Fact]
    public void Content_CanContainMultipleVariables()
    {
        // Arrange & Act
        var template = new PromptTemplate
        {
            TemplateId = "multi-var-template",
            Content = "User: {{USER_INPUT}}\nContext: {{CONTEXT}}\nDate: {{DATE}}",
            TemplateType = TemplateType.Daily,
            Source = TemplateSource.Embedded
        };

        // Assert
        template.Content.Should().Contain("{{USER_INPUT}}");
        template.Content.Should().Contain("{{CONTEXT}}");
        template.Content.Should().Contain("{{DATE}}");
    }

    [Fact]
    public void Content_VariablesShouldBeUppercaseWithUnderscores()
    {
        // Arrange & Act
        var template = new PromptTemplate
        {
            TemplateId = "test-template",
            Content = "This is a test with {{VARIABLE_NAME}} and {{ANOTHER_VAR}}",
            TemplateType = TemplateType.Daily,
            Source = TemplateSource.Embedded
        };

        // Assert
        // Variables should match pattern: {{UPPERCASE_WITH_UNDERSCORES}}
        template.Content.Should().MatchRegex(@"\{\{[A-Z_]+\}\}");
    }

    [Fact]
    public void TemplateType_DailySummary_ShouldBeValid()
    {
        // Arrange & Act
        var templateType = TemplateType.Daily;

        // Assert
        templateType.Should().Be(TemplateType.Daily);
    }

    [Fact]
    public void TemplateType_WeeklySummary_ShouldBeValid()
    {
        // Arrange & Act
        var templateType = TemplateType.Weekly;

        // Assert
        templateType.Should().Be(TemplateType.Weekly);
    }

    [Fact]
    public void TemplateType_SystemPrompt_ShouldBeValid()
    {
        // Arrange & Act
        var templateType = TemplateType.SystemPrompt;

        // Assert
        templateType.Should().Be(TemplateType.SystemPrompt);
    }

    [Fact]
    public void PromptTemplate_IsImmutable_PropertiesAreInitOnly()
    {
        // This test verifies that PromptTemplate is a record with init-only properties.
        // The compiler enforces immutability, so this test documents the design.
        
        // Arrange
        var template = new PromptTemplate
        {
            TemplateId = "immutable-test",
            Content = "Original content",
            TemplateType = TemplateType.Daily,
            Source = TemplateSource.Embedded
        };

        // Act - Create a modified copy using 'with' expression
        var modifiedTemplate = template with { Content = "Modified content" };

        // Assert
        template.Content.Should().Be("Original content");
        modifiedTemplate.Content.Should().Be("Modified content");
        template.Should().NotBe(modifiedTemplate);
    }

    [Fact]
    public void Content_CanBeMultiline()
    {
        // Arrange & Act
        var template = new PromptTemplate
        {
            TemplateId = "multiline-template",
            Content = """
                You are a helpful assistant.

                User input: {{USER_INPUT}}

                Please provide a summary.
                """,
            TemplateType = TemplateType.SystemPrompt,
            Source = TemplateSource.Embedded
        };

        // Assert
        template.Content.Should().Contain("\n");
        template.Content.Should().Contain("{{USER_INPUT}}");
    }

    [Fact]
    public void Content_WithNoVariables_ShouldBeValid()
    {
        // Arrange & Act
        var template = new PromptTemplate
        {
            TemplateId = "no-vars-template",
            Content = "This is a static prompt with no variables.",
            TemplateType = TemplateType.SystemPrompt,
            Source = TemplateSource.Embedded
        };

        // Assert
        template.Content.Should().NotContain("{{");
        template.Content.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("{{USER_INPUT}}", true)]
    [InlineData("{{CONTEXT}}", true)]
    [InlineData("{{DATE}}", true)]
    [InlineData("{{PREVIOUS_ENTRIES}}", true)]
    [InlineData("{{ INVALID_SPACING }}", false)] // Spaces inside braces
    [InlineData("{{lowercase}}", false)] // Must be uppercase
    public void Content_VariableFormat_ShouldMatchPattern(string variable, bool shouldMatch)
    {
        // Arrange
        var validPattern = @"^\{\{[A-Z_]+\}\}$";

        // Act
        bool matches = System.Text.RegularExpressions.Regex.IsMatch(variable, validPattern);

        // Assert
        matches.Should().Be(shouldMatch);
    }

    [Fact]
    public void PromptTemplate_WithDescription_ShouldStoreMetadata()
    {
        // Arrange & Act
        var template = new PromptTemplate
        {
            TemplateId = "with-description",
            Content = "Content",
            TemplateType = TemplateType.Daily,
            Description = "This template is used for daily summaries.",
            Source = TemplateSource.Embedded
        };

        // Assert
        template.Description.Should().Be("This template is used for daily summaries.");
    }

    [Fact]
    public void PromptTemplate_Description_CanBeNull()
    {
        // Arrange & Act
        var template = new PromptTemplate
        {
            TemplateId = "no-description",
            Content = "Content",
            TemplateType = TemplateType.Daily,
            Description = null,
            Source = TemplateSource.Embedded
        };

        // Assert
        template.Description.Should().BeNull();
    }
}
