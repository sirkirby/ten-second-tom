namespace TenSecondTom.Shared.Contracts;

/// <summary>
/// Handler interface for processing CQRS requests (commands and queries).
/// Defines the contract for command and query handlers throughout the application.
/// </summary>
/// <typeparam name="TRequest">The type of request to handle. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of response to return.</typeparam>
/// <remarks>
/// <para>
/// This interface enables the CQRS pattern by separating read and write operations:
/// </para>
/// <list type="bullet">
/// <item><description><b>Command Handlers</b>: Process state-changing operations (mutations, writes)</description></item>
/// <item><description><b>Query Handlers</b>: Process read operations without side effects</description></item>
/// </list>
/// <para>
/// Handlers should be registered in the DI container within their feature's DependencyInjection.cs file.
/// </para>
/// <para>
/// <b>Example Command Handler:</b>
/// </para>
/// <code>
/// public sealed class CreateUserCommandHandler 
///     : IRequestHandler&lt;CreateUserCommand, Result&lt;User&gt;&gt;
/// {
///     public async Task&lt;Result&lt;User&gt;&gt; Handle(
///         CreateUserCommand request, 
///         CancellationToken cancellationToken)
///     {
///         // Command processing logic
///     }
/// }
/// </code>
/// <para>
/// <b>Example Query Handler:</b>
/// </para>
/// <code>
/// public sealed class GetUserQueryHandler 
///     : IRequestHandler&lt;GetUserQuery, Result&lt;UserDto&gt;&gt;
/// {
///     public async Task&lt;Result&lt;UserDto&gt;&gt; Handle(
///         GetUserQuery request, 
///         CancellationToken cancellationToken)
///     {
///         // Query processing logic
///     }
/// }
/// </code>
/// </remarks>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request and returns the appropriate response.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="cancellationToken">
    /// Token to cancel the operation. Handlers should check this token periodically
    /// during long-running operations and throw <see cref="OperationCanceledException"/> when cancellation is requested.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the response.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.</exception>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

