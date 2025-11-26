using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Notifications.Channels;
using TenSecondTom.Infrastructure.Notifications.Channels.OS;
using TenSecondTom.Infrastructure.Notifications.Security;
using TenSecondTom.Shared.Abstractions.Notifications;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Infrastructure.Notifications;

/// <summary>
/// Extension methods for registering Notification infrastructure services.
/// </summary>
public static class NotificationFeatureExtensions
{
    /// <summary>
    /// Adds Notification infrastructure services to the service collection.
    /// Registers notification channels (OS native), core notification service, and security services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Registered Services:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>NotificationOptions - Configuration for notification behavior</item>
    /// <item>SecurityOptions - Configuration for notification token security</item>
    /// <item>INotificationTokenService - Secure token generation for interactive actions</item>
    /// <item>INotificationChannel (multiple) - Platform-specific notification providers</item>
    /// <item>INotificationService - Core notification routing and management</item>
    /// </list>
    /// <para>
    /// <strong>Channel Selection:</strong>
    /// </para>
    /// <para>
    /// Multiple channels are registered, but only platform-compatible channels will be
    /// available at runtime. The NotificationService selects the first available channel
    /// when sending notifications.
    /// </para>
    /// <para>
    /// Current channels:
    /// </para>
    /// <list type="bullet">
    /// <item>MacOsNotificationProvider - macOS native notifications (osascript)</item>
    /// <item>WindowsNotificationProvider - Runtime stub (unavailable in net10.0 build)</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddNotificationFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register feature-owned configuration using Options Pattern
        services.AddOptions<NotificationOptions>()
            .BindConfiguration(NotificationOptions.SectionPath)
            .ValidateOnStart();

        services.AddOptions<SecurityOptions>()
            .BindConfiguration(SecurityOptions.SectionPath)
            .ValidateOnStart();

        // Register notification token service for securing interactive actions
        services.AddSingleton<INotificationTokenService, NotificationTokenService>();

        // Register notification channels (platform-specific providers)
        // All channels are registered, but availability is determined at runtime
        services.AddSingleton<INotificationChannel, MacOsNotificationProvider>();
        services.AddSingleton<INotificationChannel, WindowsNotificationProvider>();

        // Register core notification service
        // This service routes notifications to available channels
        services.AddSingleton<INotificationService, NotificationService>();

        return services;
    }
}
