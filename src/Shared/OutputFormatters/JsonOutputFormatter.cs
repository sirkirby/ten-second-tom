using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.OutputFormatters;

/// <summary>
/// Provides JSON output formatting for CLI commands.
/// Enables programmatic consumption of command results.
/// </summary>
public static class JsonOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            JsonOutputContext.Default,
            new DefaultJsonTypeInfoResolver())
    };

    /// <summary>
    /// Formats a successful command result as JSON.
    /// </summary>
    /// <param name="commandName">Name of the command that executed.</param>
    /// <param name="data">Result data to include in the output.</param>
    /// <param name="timestamp">Timestamp of the command execution.</param>
    /// <returns>JSON string representing the successful result.</returns>
    public static string FormatSuccess(string commandName, object? data, DateTimeOffset timestamp)
    {
        var output = new JsonOutput
        {
            Success = true,
            Timestamp = timestamp,
            Command = commandName,
            Data = data,
            Error = null
        };

        return JsonSerializer.Serialize(output, JsonOptions);
    }

    /// <summary>
    /// Formats a failed command result as JSON.
    /// </summary>
    /// <param name="commandName">Name of the command that executed.</param>
    /// <param name="errorMessage">Error message describing the failure.</param>
    /// <param name="timestamp">Timestamp of the command execution.</param>
    /// <returns>JSON string representing the failed result.</returns>
    public static string FormatFailure(string commandName, string? errorMessage, DateTimeOffset timestamp)
    {
        var output = new JsonOutput
        {
            Success = false,
            Timestamp = timestamp,
            Command = commandName,
            Data = null,
            Error = errorMessage
        };

        return JsonSerializer.Serialize(output, JsonOptions);
    }

    /// <summary>
    /// Formats a Result object as JSON.
    /// </summary>
    /// <typeparam name="T">Type of the result data.</typeparam>
    /// <param name="commandName">Name of the command that executed.</param>
    /// <param name="result">Result object to format.</param>
    /// <param name="timestamp">Timestamp of the command execution.</param>
    /// <returns>JSON string representing the result.</returns>
    public static string FormatFromResult<T>(string commandName, Result<T> result, DateTimeOffset timestamp)
    {
        if (result.IsSuccess)
        {
            return FormatSuccess(commandName, result.Value, timestamp);
        }
        else
        {
            return FormatFailure(commandName, result.Error, timestamp);
        }
    }

    /// <summary>
    /// Represents the JSON output structure for CLI commands.
    /// </summary>
    internal sealed class JsonOutput
    {
        /// <summary>
        /// Gets or sets a value indicating whether the command succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the ISO8601 timestamp of the command execution.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the name of the command that executed.
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or sets the result data (null if command failed).
        /// </summary>
        public object? Data { get; set; }

        /// <summary>
        /// Gets or sets the error message (null if command succeeded).
        /// </summary>
        public string? Error { get; set; }
    }
}
