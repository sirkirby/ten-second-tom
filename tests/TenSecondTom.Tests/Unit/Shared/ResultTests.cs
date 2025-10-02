using FluentAssertions;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Shared;

/// <summary>
/// Unit tests for the Result{T} type.
/// Tests verify success/failure states, value access, error handling, and conversions.
/// </summary>
public sealed class ResultTests
{
    [Fact]
    public void Success_WithValue_ShouldContainValue()
    {
        // Arrange
        const string expectedValue = "test value";

        // Act
        Result<string> result = Result<string>.Success(expectedValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(expectedValue);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_WithError_ShouldContainError()
    {
        // Arrange
        const string expectedError = "Something went wrong";

        // Act
        Result<string> result = Result<string>.Failure(expectedError);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
    }

    [Fact]
    public void Failure_AccessingValue_ShouldThrowInvalidOperationException()
    {
        // Arrange
        Result<string> result = Result<string>.Failure("Error");

        // Act
        Action act = () => _ = result.Value;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot access Value of a failed result*");
    }

    [Fact]
    public void IsSuccess_OnSuccessResult_ShouldBeTrue()
    {
        // Arrange & Act
        Result<int> result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void IsFailure_OnFailureResult_ShouldBeTrue()
    {
        // Arrange & Act
        Result<int> result = Result<int>.Failure("Error");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldCreateSuccessResult()
    {
        // Arrange
        const string value = "test";

        // Act
        Result<string> result = value;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void ImplicitConversion_FromError_ShouldCreateFailureResult()
    {
        // Arrange
        const string error = "Error message";

        // Act
        Result<string> result = Result<string>.Failure(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Match_OnSuccess_ShouldExecuteSuccessFunction()
    {
        // Arrange
        Result<int> result = Result<int>.Success(10);
        string? executedPath = null;

        // Act
        int output = result.Match(
            onSuccess: value =>
            {
                executedPath = "success";
                return value * 2;
            },
            onFailure: error =>
            {
                executedPath = "failure";
                return 0;
            }
        );

        // Assert
        executedPath.Should().Be("success");
        output.Should().Be(20);
    }

    [Fact]
    public void Match_OnFailure_ShouldExecuteFailureFunction()
    {
        // Arrange
        Result<int> result = Result<int>.Failure("Error occurred");
        string? executedPath = null;

        // Act
        int output = result.Match(
            onSuccess: value =>
            {
                executedPath = "success";
                return value * 2;
            },
            onFailure: error =>
            {
                executedPath = "failure";
                return -1;
            }
        );

        // Assert
        executedPath.Should().Be("failure");
        output.Should().Be(-1);
    }

    [Fact]
    public void ToString_OnSuccess_ShouldShowSuccessState()
    {
        // Arrange
        Result<string> result = Result<string>.Success("test value");

        // Act
        string stringRepresentation = result.ToString();

        // Assert
        stringRepresentation.Should().Contain("Success");
        stringRepresentation.Should().Contain("test value");
    }

    [Fact]
    public void ToString_OnFailure_ShouldShowFailureState()
    {
        // Arrange
        Result<string> result = Result<string>.Failure("Error message");

        // Act
        string stringRepresentation = result.ToString();

        // Assert
        stringRepresentation.Should().Contain("Failure");
        stringRepresentation.Should().Contain("Error message");
    }

    [Fact]
    public void Failure_WithNullOrEmptyError_ShouldThrowArgumentException()
    {
        // Act
        Action actNull = () => Result<string>.Failure(null!);
        Action actEmpty = () => Result<string>.Failure(string.Empty);
        Action actWhitespace = () => Result<string>.Failure("   ");

        // Assert
        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
        actWhitespace.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Success_WithNullValue_ShouldAllowNullForReferenceTypes()
    {
        // Act
        Result<string?> result = Result<string?>.Success(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Equality_TwoSuccessResultsWithSameValue_ShouldBeEqual()
    {
        // Arrange
        Result<int> result1 = Result<int>.Success(42);
        Result<int> result2 = Result<int>.Success(42);

        // Act & Assert
        result1.Equals(result2).Should().BeTrue();
        (result1 == result2).Should().BeTrue();
        (result1 != result2).Should().BeFalse();
    }

    [Fact]
    public void Equality_TwoFailureResultsWithSameError_ShouldBeEqual()
    {
        // Arrange
        Result<int> result1 = Result<int>.Failure("Error");
        Result<int> result2 = Result<int>.Failure("Error");

        // Act & Assert
        result1.Equals(result2).Should().BeTrue();
        (result1 == result2).Should().BeTrue();
        (result1 != result2).Should().BeFalse();
    }

    [Fact]
    public void Equality_SuccessAndFailure_ShouldNotBeEqual()
    {
        // Arrange
        Result<int> success = Result<int>.Success(42);
        Result<int> failure = Result<int>.Failure("Error");

        // Act & Assert
        success.Equals(failure).Should().BeFalse();
        (success == failure).Should().BeFalse();
        (success != failure).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_ForEqualResults_ShouldBeSame()
    {
        // Arrange
        Result<int> result1 = Result<int>.Success(42);
        Result<int> result2 = Result<int>.Success(42);

        // Act & Assert
        result1.GetHashCode().Should().Be(result2.GetHashCode());
    }
}
