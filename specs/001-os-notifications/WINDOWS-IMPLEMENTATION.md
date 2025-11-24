# Windows Notifications Implementation Guide

**Status**: Deferred - macOS implemented first
**Created**: 2025-11-23
**Target**: Future release after macOS validation

## Overview

This document describes the **multi-targeting approach required for Windows notification support**. The initial implementation focuses on **macOS only with single binary (net9.0)** to reduce complexity. When ready to add Windows support, follow this guide.

---

## Why Multi-Targeting is Required for Windows

### The Challenge

**Windows interactive Toast notifications require Windows Runtime (WinRT) APIs:**
- `Microsoft.Toolkit.Uwp.Notifications` package (v7.1.3+)
- `Windows.UI.Notifications` namespace
- Toast notification activation callbacks
- Action button support

**These APIs are ONLY available when targeting `net9.0-windows10.0.19041.0` or later.**

### Alternative Approaches Considered

| Approach | Feasibility | Why Not Used |
|----------|-------------|--------------|
| P/Invoke to native Windows APIs | ❌ Complex | Requires manual COM interop, no action button support |
| PowerShell `New-BurntToastNotification` | ❌ Limited | External process, no callback mechanism, requires module install |
| Windows Forms NotifyIcon | ❌ Wrong paradigm | System tray balloons, not Action Center notifications |
| WinRT without multi-targeting | ❌ Not possible | WinRT APIs unavailable in net9.0 TFM |

**Conclusion**: Multi-targeting is the **only viable path** for full Windows notification support.

---

## Multi-Targeting Implementation Plan

### Step 1: Update Project File

**File**: `src/TenSecondTom.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>

    <!-- BEFORE: Single TFM -->
    <!-- <TargetFramework>net9.0</TargetFramework> -->

    <!-- AFTER: Multi-targeting -->
    <TargetFrameworks>net9.0;net9.0-windows10.0.19041.0</TargetFrameworks>

    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UserSecretsId>ten-second-tom-secrets</UserSecretsId>

    <!-- Define platform constants for conditional compilation -->
    <DefineConstants Condition="$(TargetFramework.Contains('windows'))">$(DefineConstants);WINDOWS</DefineConstants>
    <DefineConstants Condition="!$(TargetFramework.Contains('windows'))">$(DefineConstants);MACOS</DefineConstants>
  </PropertyGroup>

  <!-- Existing packages (all TFMs) -->
  <ItemGroup>
    <PackageReference Include="MediatR" Version="13.1.0" />
    <PackageReference Include="FluentValidation" Version="12.0.0" />
    <!-- ... other packages ... -->
  </ItemGroup>

  <!-- Windows-specific package (conditional) -->
  <ItemGroup Condition="$(TargetFramework.Contains('windows'))">
    <PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.1.3" />
  </ItemGroup>
</Project>
```

### Step 2: Implement Windows Notification Provider

**File**: `src/Infrastructure/Notifications/Channels/OS/WindowsNotificationProvider.cs`

```csharp
namespace TenSecondTom.Infrastructure.Notifications.Channels.OS;

#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

/// <summary>
/// Windows notification channel using Toast Notifications (Windows 10/11).
/// </summary>
public sealed class WindowsNotificationProvider(
    IOptions<NotificationOptions> options,
    INotificationTokenService tokenService,
    ILogger<WindowsNotificationProvider> logger) : INotificationChannel
{
    public string ChannelId => "os-windows";
    public string DisplayName => "Windows Toast Notifications";

    public NotificationChannelCapabilities Capabilities => new()
    {
        SupportsInteractiveActions = true,  // ✅ Action buttons
        SupportsRichMedia = false,
        SupportsPriority = true,
        SupportsCallbacks = true,           // ✅ Activation callbacks
        MaxTitleLength = 100,
        MaxMessageLength = 500
    };

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if Toast notifications are available
            var notifier = ToastNotificationManagerCompat.CreateToastNotifier();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Windows Toast notifications unavailable");
            return Task.FromResult(false);
        }
    }

    public Task<Result> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toastContent = new ToastContentBuilder()
                .AddText(notification.Title)
                .AddText(notification.Message);

            // Add action buttons if interactive
            if (notification.Actions.Any())
            {
                foreach (var action in notification.Actions)
                {
                    // Generate signed token
                    var token = tokenService.GenerateToken(notification.Id, action.Id);

                    toastContent.AddButton(new ToastButton()
                        .SetContent(action.Label)
                        .AddArgument("action", action.Id)
                        .AddArgument("token", token)
                        .AddArgument("notificationId", notification.Id.ToString())
                        .SetBackgroundActivation());
                }
            }

            // Map priority to Windows toast scenario
            if (notification.Priority >= NotificationPriority.High)
            {
                toastContent.SetToastScenario(ToastScenario.Urgent);
            }

            // Show notification
            toastContent.Show();

            logger.LogInformation(
                "Windows Toast notification sent: {NotificationId} - {Title}",
                notification.Id, notification.Title);

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Windows Toast notification");
            return Task.FromResult(Result.Failure($"Windows notification failed: {ex.Message}"));
        }
    }

    public Task<Result> DismissAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        try
        {
            ToastNotificationManagerCompat.History.Remove(notificationId.ToString());
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dismiss Windows notification {NotificationId}", notificationId);
            return Task.FromResult(Result.Failure($"Dismiss failed: {ex.Message}"));
        }
    }
}
#else
// Compile-time stub for non-Windows builds
public sealed class WindowsNotificationProvider(
    ILogger<WindowsNotificationProvider> logger) : INotificationChannel
{
    public string ChannelId => "os-windows";
    public string DisplayName => "Windows Toast Notifications (Unavailable)";

    public NotificationChannelCapabilities Capabilities => new()
    {
        SupportsInteractiveActions = false,
        SupportsRichMedia = false,
        SupportsPriority = false,
        SupportsCallbacks = false,
        MaxTitleLength = 0,
        MaxMessageLength = 0
    };

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Windows notifications unavailable (not compiled for Windows TFM)");
        return Task.FromResult(false);
    }

    public Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure("Windows notifications require net9.0-windows TFM"));
    }

    public Task<Result> DismissAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure("Windows notifications unavailable"));
    }
}
#endif
```

### Step 3: Implement Activation Handler

**File**: `src/Infrastructure/Notifications/NotificationActivationHandler.cs`

```csharp
namespace TenSecondTom.Infrastructure.Notifications;

#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;

/// <summary>
/// Handles Windows Toast notification activation events (button clicks).
/// </summary>
public sealed class NotificationActivationHandler(
    INotificationTokenService tokenService,
    IMediator mediator,
    ILogger<NotificationActivationHandler> logger)
{
    public void Initialize()
    {
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        logger.LogInformation("Windows Toast activation handler initialized");
    }

    private async void OnToastActivated(ToastNotificationActivatedEventArgsCompat args)
    {
        try
        {
            var arguments = ToastArguments.Parse(args.Argument);

            if (!arguments.TryGetValue("action", out var actionId) ||
                !arguments.TryGetValue("token", out var token) ||
                !arguments.TryGetValue("notificationId", out var notificationIdStr) ||
                !Guid.TryParse(notificationIdStr, out var notificationId))
            {
                logger.LogWarning("Invalid Toast activation arguments");
                return;
            }

            // Validate token
            var validationResult = await tokenService.ValidateTokenAsync(
                token, notificationId, actionId);

            if (!validationResult.IsSuccess)
            {
                logger.LogWarning(
                    "Invalid notification token for action {ActionId}: {Error}",
                    actionId, validationResult.Error);
                return;
            }

            // Route to handler via MediatR
            await mediator.Send(new HandleNotificationAction.Command(notificationId, actionId));

            logger.LogInformation(
                "Toast notification action handled: {ActionId} for {NotificationId}",
                actionId, notificationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Toast activation");
        }
    }
}
#else
// No-op stub for non-Windows builds
public sealed class NotificationActivationHandler(
    ILogger<NotificationActivationHandler> logger)
{
    public void Initialize()
    {
        logger.LogDebug("Windows Toast activation handler unavailable (not compiled for Windows TFM)");
    }
}
#endif
```

### Step 4: Update DI Registration

**File**: `src/Infrastructure/Notifications/DependencyInjection.cs`

```csharp
public static IServiceCollection AddNotificationFeature(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Register options (all platforms)
    services.AddOptions<NotificationOptions>()
        .BindConfiguration(NotificationOptions.SectionName)
        .ValidateOnStart();

    services.AddOptions<SecurityOptions>()
        .BindConfiguration(SecurityOptions.SectionName)
        .ValidateOnStart();

    // Register token service (all platforms)
    services.AddSingleton<INotificationTokenService, NotificationTokenService>();

    // Register platform-specific channels
#if WINDOWS
    services.AddSingleton<INotificationChannel, WindowsNotificationProvider>();
    services.AddSingleton<NotificationActivationHandler>();
#else
    services.AddSingleton<INotificationChannel, MacOsNotificationProvider>();
#endif

    // Register core service (all platforms)
    services.AddSingleton<INotificationService, NotificationService>();

    return services;
}
```

### Step 5: Update Program.cs

**File**: `src/Program.cs`

```csharp
// Register notification services
builder.Services.AddNotificationFeature(builder.Configuration);

var app = builder.Build();

// Initialize Windows activation handler (if available)
#if WINDOWS
var activationHandler = app.Services.GetRequiredService<NotificationActivationHandler>();
activationHandler.Initialize();
#endif

await app.RunAsync();
```

---

## Build & Publish Impact

### Build Output

With multi-targeting, you'll get **two build outputs**:

```
bin/Release/net9.0/
├── TenSecondTom (macOS/Linux)
└── [dependencies]

bin/Release/net9.0-windows10.0.19041.0/
├── TenSecondTom.exe (Windows)
└── [dependencies + Microsoft.Toolkit.Uwp.Notifications]
```

### Publish Commands

```bash
# macOS/Linux build
dotnet publish -c Release -f net9.0 -r osx-arm64 --self-contained

# Windows build
dotnet publish -c Release -f net9.0-windows10.0.19041.0 -r win-x64 --self-contained
```

### CI/CD Updates

**.github/workflows/build.yml** will need updates:

```yaml
- name: Build for macOS
  run: dotnet build -c Release -f net9.0

- name: Build for Windows
  run: dotnet build -c Release -f net9.0-windows10.0.19041.0

- name: Publish macOS
  run: dotnet publish -c Release -f net9.0 -r osx-arm64 --self-contained

- name: Publish Windows
  run: dotnet publish -c Release -f net9.0-windows10.0.19041.0 -r win-x64 --self-contained
```

---

## Testing Strategy

### Platform-Specific Tests

```csharp
// tests/TenSecondTom.Tests/Infrastructure/Notifications/Channels/WindowsNotificationProviderTests.cs

#if WINDOWS
public sealed class WindowsNotificationProviderTests
{
    [Fact]
    public async Task SendAsync_WithActions_CreatesInteractiveToast()
    {
        // Test Windows-specific functionality
    }
}
#else
public sealed class WindowsNotificationProviderTests
{
    [Fact]
    public void WindowsProvider_OnNonWindowsPlatform_IsUnavailable()
    {
        // Test graceful degradation
    }
}
#endif
```

---

## Migration Checklist

When ready to add Windows support:

- [ ] Update `TenSecondTom.csproj` with `<TargetFrameworks>` (plural)
- [ ] Add `DefineConstants` for WINDOWS/MACOS
- [ ] Add `Microsoft.Toolkit.Uwp.Notifications` package (conditional)
- [ ] Implement `WindowsNotificationProvider` with `#if WINDOWS`
- [ ] Implement `NotificationActivationHandler` with `#if WINDOWS`
- [ ] Update `DependencyInjection.cs` with conditional registration
- [ ] Update `Program.cs` to initialize activation handler
- [ ] Add platform-specific tests with `#if WINDOWS`
- [ ] Update CI/CD workflows for multi-TFM builds
- [ ] Update publish scripts for both TFMs
- [ ] Test on Windows 10/11 with interactive notifications
- [ ] Update documentation (CLAUDE.md, README.md)

---

## Current Implementation (macOS Only)

**Until Windows support is added:**

1. **Single TFM**: `net9.0` (no multi-targeting)
2. **macOS Channel**: Fully implemented with osascript
3. **Windows Channel**: Stub that returns "Unavailable" (runtime detection)
4. **Architecture**: Fully extensible - adding Windows requires NO changes to abstractions
5. **DI**: Runtime detection selects macOS channel on macOS, NoOp on Windows

**File**: `src/Infrastructure/Notifications/Channels/OS/WindowsNotificationProvider.cs` (current stub)

```csharp
// Current implementation (no conditional compilation needed)
public sealed class WindowsNotificationProvider : INotificationChannel
{
    public Task<bool> IsAvailableAsync(...)
    {
        // Runtime detection: Always false until multi-targeting added
        return Task.FromResult(false);
    }

    public Task<Result> SendAsync(...)
    {
        return Task.FromResult(Result.Failure(
            "Windows notifications require Windows-specific build. " +
            "See specs/001-os-notifications/WINDOWS-IMPLEMENTATION.md"));
    }
}
```

---

## Benefits of Current Approach (Single Binary First)

✅ **Simpler initial implementation** - no conditional compilation complexity
✅ **Validate architecture** on macOS before adding Windows
✅ **Single binary** for now - easier CI/CD and distribution
✅ **Fully extensible** - Windows support is additive, not breaking
✅ **Lower risk** - multi-targeting is deferred complexity

When ready for Windows:
- Architecture is proven
- macOS implementation serves as reference
- Multi-targeting is well-documented
- Migration is straightforward

---

**Next Steps**: Complete macOS implementation, validate on production, then revisit Windows multi-targeting when value justifies complexity.
