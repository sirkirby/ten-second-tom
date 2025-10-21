namespace TenSecondTom.Shared.Contracts;

/// <summary>
/// Marker interface for CQRS requests (commands and queries).
/// Indicates this request returns a specific response type.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
/// <remarks>
/// This interface is used to implement the CQRS pattern across all feature slices.
/// Commands represent state-changing operations, while queries represent read operations.
/// Both implement this interface to enable generic handler resolution.
/// </remarks>
public interface IRequest<out TResponse>
{
}

