namespace TenSecondTom.Shared.Models;

/// <summary>
/// Defines the urgency level of a notification.
/// Higher priority notifications may be displayed more prominently or persistently.
/// </summary>
public enum NotificationPriority
{
    /// <summary>
    /// Low priority - informational notifications that don't require immediate attention.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority - standard notifications for routine events.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority - important notifications requiring user attention.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical priority - urgent notifications requiring immediate attention.
    /// May trigger additional notification methods or bypass quiet modes.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Represents the lifecycle state of a notification.
/// Tracks notifications from creation through display to final disposition.
/// </summary>
public enum NotificationState
{
    /// <summary>
    /// Notification has been created but not yet displayed.
    /// </summary>
    Pending,

    /// <summary>
    /// Notification has been successfully displayed to the user.
    /// </summary>
    Displayed,

    /// <summary>
    /// User has interacted with the notification (clicked an action button).
    /// </summary>
    ActedUpon,

    /// <summary>
    /// User has explicitly dismissed the notification.
    /// </summary>
    Dismissed,

    /// <summary>
    /// Notification has expired (timeout reached without user interaction).
    /// </summary>
    Expired,

    /// <summary>
    /// Notification failed to display due to an error.
    /// </summary>
    Failed
}

/// <summary>
/// Describes the features and limitations of a notification channel.
/// Used to determine which notifications can be sent through which channels.
/// </summary>
public sealed record NotificationChannelCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the channel supports interactive actions.
    /// </summary>
    public required bool SupportsInteractivity { get; init; }

    /// <summary>
    /// Gets a value indicating whether the channel supports custom timeouts.
    /// </summary>
    public required bool SupportsCustomTimeout { get; init; }

    /// <summary>
    /// Gets a value indicating whether the channel supports custom icons or images.
    /// </summary>
    public required bool SupportsCustomIcon { get; init; }

    /// <summary>
    /// Gets a value indicating whether the channel supports grouping related notifications.
    /// </summary>
    public required bool SupportsGrouping { get; init; }

    /// <summary>
    /// Gets the maximum number of action buttons the channel supports.
    /// Zero indicates no action support.
    /// </summary>
    public required int MaxActions { get; init; }

    /// <summary>
    /// Creates capabilities for the native OS notification system.
    /// </summary>
    /// <returns>Capabilities for macOS/Windows native notifications.</returns>
    public static NotificationChannelCapabilities OSNative() => new()
    {
        SupportsInteractivity = true,
        SupportsCustomTimeout = true,
        SupportsCustomIcon = true,
        SupportsGrouping = true,
        MaxActions = 4
    };

    /// <summary>
    /// Creates capabilities for the Slack notification channel.
    /// </summary>
    /// <returns>Capabilities for Slack notifications.</returns>
    public static NotificationChannelCapabilities Slack() => new()
    {
        SupportsInteractivity = true,
        SupportsCustomTimeout = false,
        SupportsCustomIcon = true,
        SupportsGrouping = false,
        MaxActions = 5
    };
}

/// <summary>
/// Represents an action that can be taken on an interactive notification.
/// Actions appear as buttons in the notification UI.
/// </summary>
public sealed record NotificationAction
{
    /// <summary>
    /// Gets the unique identifier for this action.
    /// Used to identify which action was clicked when the user responds.
    /// </summary>
    public required string ActionId { get; init; }

    /// <summary>
    /// Gets the user-visible label for the action button.
    /// Should be concise (typically 1-2 words).
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the command to execute when this action is triggered.
    /// Format: "command-name arg1 arg2" or "command-name --flag value".
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets the security token embedded in the callback URL.
    /// Used to validate that the callback is authentic and not tampered with.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Creates a new notification action.
    /// </summary>
    /// <param name="actionId">Unique identifier for the action.</param>
    /// <param name="label">User-visible button label.</param>
    /// <param name="command">Command to execute when action is triggered.</param>
    /// <returns>A new notification action instance.</returns>
    public static NotificationAction Create(string actionId, string label, string command) => new()
    {
        ActionId = actionId,
        Label = label,
        Command = command,
        Token = null
    };

    /// <summary>
    /// Creates a copy of this action with a security token attached.
    /// </summary>
    /// <param name="token">Security token to embed in the action.</param>
    /// <returns>A new action instance with the token set.</returns>
    public NotificationAction WithToken(string token) => this with { Token = token };
}

/// <summary>
/// Represents a notification that can be displayed to the user through various channels.
/// Supports both simple text notifications and interactive notifications with action buttons.
/// </summary>
public sealed record Notification
{
    /// <summary>
    /// Gets the unique identifier for this notification.
    /// Used to track notification state and correlate actions with notifications.
    /// </summary>
    public required Guid NotificationId { get; init; }

    /// <summary>
    /// Gets the notification title (heading).
    /// Should be concise and descriptive.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the notification body text (detailed message).
    /// Can be longer and more descriptive than the title.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the priority level of this notification.
    /// </summary>
    public required NotificationPriority Priority { get; init; }

    /// <summary>
    /// Gets the timeout duration in seconds after which the notification will auto-dismiss.
    /// Null indicates the notification should remain visible until user interaction.
    /// </summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Gets the collection of interactive actions available on this notification.
    /// Empty collection indicates a non-interactive notification.
    /// </summary>
    public IReadOnlyList<NotificationAction> Actions { get; init; } = [];

    /// <summary>
    /// Gets the current lifecycle state of this notification.
    /// </summary>
    public NotificationState State { get; init; } = NotificationState.Pending;

    /// <summary>
    /// Gets the timestamp when this notification was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the timestamp when this notification was displayed (if applicable).
    /// </summary>
    public DateTimeOffset? DisplayedAt { get; init; }

    /// <summary>
    /// Gets the grouping key for related notifications.
    /// Notifications with the same group key may be collapsed or displayed together.
    /// </summary>
    public string? GroupKey { get; init; }

    /// <summary>
    /// Creates a basic non-interactive notification.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message body.</param>
    /// <param name="priority">Priority level (defaults to Normal).</param>
    /// <param name="timeoutSeconds">Auto-dismiss timeout in seconds (null for no timeout).</param>
    /// <returns>A new basic notification instance.</returns>
    public static Notification CreateBasic(
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal,
        int? timeoutSeconds = null) => new()
    {
        NotificationId = Guid.NewGuid(),
        Title = title,
        Message = message,
        Priority = priority,
        TimeoutSeconds = timeoutSeconds,
        Actions = [],
        State = NotificationState.Pending
    };

    /// <summary>
    /// Creates an interactive notification with action buttons.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message body.</param>
    /// <param name="actions">Collection of actions to display as buttons.</param>
    /// <param name="priority">Priority level (defaults to Normal).</param>
    /// <param name="timeoutSeconds">Auto-dismiss timeout in seconds (null for no timeout).</param>
    /// <returns>A new interactive notification instance.</returns>
    public static Notification CreateInteractive(
        string title,
        string message,
        IReadOnlyList<NotificationAction> actions,
        NotificationPriority priority = NotificationPriority.Normal,
        int? timeoutSeconds = null) => new()
    {
        NotificationId = Guid.NewGuid(),
        Title = title,
        Message = message,
        Priority = priority,
        TimeoutSeconds = timeoutSeconds,
        Actions = actions,
        State = NotificationState.Pending
    };
}
