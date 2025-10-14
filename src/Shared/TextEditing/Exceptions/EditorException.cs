namespace TenSecondTom.Shared.TextEditing.Exceptions;

/// <summary>
/// Exception thrown when text editor operations fail.
/// </summary>
public sealed class EditorException : Exception
{
    public EditorException()
    {
    }

    public EditorException(string message) : base(message)
    {
    }

    public EditorException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
