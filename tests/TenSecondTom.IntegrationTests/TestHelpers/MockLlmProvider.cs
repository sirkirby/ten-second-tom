using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.IntegrationTests.TestHelpers;

/// <summary>
/// Mock LLM provider for testing that returns predictable responses.
/// </summary>
public sealed class MockLlmProvider : ILlmProvider
{
    private readonly Queue<string> _responses;
    private readonly string _defaultResponse;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockLlmProvider"/> class.
    /// </summary>
    /// <param name="responses">Predefined responses to return in sequence.</param>
    /// <param name="defaultResponse">Default response when queue is empty.</param>
    public MockLlmProvider(IEnumerable<string>? responses = null, string? defaultResponse = null)
    {
        _responses = new Queue<string>(responses ?? []);
        _defaultResponse = defaultResponse ?? "Mock LLM response";
    }

    /// <inheritdoc/>
    public string ProviderName => "MockProvider";

    /// <inheritdoc/>
    public string ModelName => "mock-model-1.0";

    /// <inheritdoc/>
    public Task<Result<LlmResponse>> GenerateCompletionAsync(
        string prompt,
        CancellationToken cancellationToken,
        int? maxTokens = null,
        double? temperature = null)
    {
        string response = _responses.Count > 0 ? _responses.Dequeue() : _defaultResponse;
        
        // Simulate token usage - roughly estimate based on response length
        int outputTokens = response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        int inputTokens = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        
        var llmResponse = new LlmResponse
        {
            Content = response,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
        
        return Task.FromResult(Result<LlmResponse>.Success(llmResponse));
    }

    /// <summary>
    /// Adds a response to the queue.
    /// </summary>
    /// <param name="response">The response to add.</param>
    public void AddResponse(string response)
    {
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Creates a mock provider with a standard daily summary JSON response.
    /// </summary>
    /// <returns>A configured mock LLM provider.</returns>
    public static MockLlmProvider WithDailySummaryResponse()
    {
        string jsonResponse = """
        {
          "keyEvents": ["Had productive team meeting", "Made progress on design doc"],
          "themes": ["Collaboration", "Feature development"],
          "todoItems": [
            {"description": "Review pull request", "isCompleted": false},
            {"description": "Finalize architecture", "isCompleted": false}
          ],
          "importantPeople": ["John", "Team members"],
          "notableTasks": ["Design document completion", "Feature planning"]
        }
        """;
        
        return new MockLlmProvider([jsonResponse]);
    }

    /// <summary>
    /// Creates a mock provider with a standard weekly review response.
    /// </summary>
    /// <returns>A configured mock LLM provider.</returns>
    public static MockLlmProvider WithWeeklyReviewResponse()
    {
        string markdownResponse = """
        # Weekly Review - October 3, 2025

        ## Summary
        This week focused on feature development and team collaboration.

        ## Key Achievements
        - Completed design documentation
        - Productive team meetings
        - Code review progress

        ## Themes
        - Collaboration and teamwork
        - Technical architecture
        - Project momentum

        ## Looking Ahead
        - Start implementation phase
        - Continue code reviews
        - Team alignment meetings
        """;
        
        return new MockLlmProvider([markdownResponse]);
    }
}
