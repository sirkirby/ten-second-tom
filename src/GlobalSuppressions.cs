// This file is used to configure code analysis
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Make internal types visible to test projects
[assembly: InternalsVisibleTo("TenSecondTom.Tests")]
[assembly: InternalsVisibleTo("TenSecondTom.IntegrationTests")]

// Namespace naming - "Shared" is intentional and commonly used in .NET projects
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Shared", Justification = "Shared is a standard namespace pattern in .NET")]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Shared.Results", Justification = "Shared is a standard namespace pattern in .NET")]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Shared.Models", Justification = "Shared is a standard namespace pattern in .NET")]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Shared.OutputFormatters", Justification = "Shared is a standard namespace pattern in .NET")]

// Public API - these types are designed to be public for CLI use
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.MemoryEntry", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.MemoryEntryMetadata", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.DailyEntry", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.DailySummary", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.TodoItem", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.WeeklyEntry", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.WeeklySummary", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.DateRange", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.PromptTemplate", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.TemplateType", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.UserSession", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.StorageConfiguration", Justification = "Public API for domain models")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.Models.RetentionPolicy", Justification = "Public API for domain models")]

// Output formatters
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Shared.OutputFormatters.JsonOutputFormatter", Justification = "Public API for CLI JSON output")]

// Infrastructure interfaces
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Prompts.IPromptTemplateLoader", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Prompts.EmbeddedPromptTemplateLoader", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.IAuthenticationService", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.SshKeyAuthenticationService", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.ISshAgentClient", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.SshAgentAuthenticationService", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.SshAgentClient", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.AuthenticationServiceFactory", Justification = "Public factory for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Logging.LoggingConfiguration", Justification = "Public utility class for bootstrapping")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Cli.OutputContext", Justification = "Public context class for CLI commands")]

// SSH Agent authentication - logging and exception handling suppressions
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.SshAgentAuthenticationService", Justification = "Logging clarity preferred for authentication operations")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.SshAgentClient", Justification = "Logging clarity preferred for agent communication")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.AuthenticationServiceFactory", Justification = "Logging clarity preferred for factory decisions")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Infrastructure.Auth.SshAgentAuthenticationService.AuthenticateAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Shared.Results.Result{TenSecondTom.Shared.Models.UserSession}}", Justification = "Must handle all exceptions to return Result<T> instead of throwing")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Infrastructure.Auth.SshAgentClient.ConnectAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Boolean}", Justification = "Must handle all connection errors gracefully")]

// Setup Feature - Public API suppressions
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Setup feature types need to be public for DI and testing")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Configuration", Justification = "Configuration types need to be public for DI")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Auth.SshProviders", Justification = "SSH provider types need to be public for DI")]

// Setup Feature - Logging suppressions (simple logging calls preferred over delegates in setup/config code)
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Simple logging preferred in setup wizard for clarity")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Configuration", Justification = "Simple logging preferred in configuration code")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Auth.SshProviders", Justification = "Simple logging preferred in SSH detection")]

// Setup Feature - Exception handling (must catch all to return Result<T>)
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Setup operations must handle all exceptions and return Result<T>")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Configuration", Justification = "Configuration operations must handle all exceptions gracefully")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Auth.SshProviders", Justification = "SSH detection must handle all exceptions gracefully")]

// Setup Feature - ConfigureAwait suppressions (not needed in console app)
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Console application - no synchronization context")]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Configuration", Justification = "Console application - no synchronization context")]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Auth.SshProviders", Justification = "Console application - no synchronization context")]

// Setup Feature - Parameter validation (validated by FluentValidation at handler level)
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Validated by FluentValidation pipeline")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Configuration.ConfigurationChecker", Justification = "Parameters validated by caller")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Configuration.UserSecretsStorageService", Justification = "Parameters validated by caller")]

// Setup Feature - Globalization (not applicable for CLI tools)
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "CLI tool - globalization not required")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Configuration", Justification = "CLI tool - globalization not required")]
[assembly: SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Ordinal comparison is implicit for paths")]
[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Lowercase normalization appropriate for settings keys")]
[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Configuration", Justification = "Lowercase normalization appropriate for config keys")]
[assembly: SuppressMessage("Globalization", "CA1310:Specify StringComparison for correctness", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Infrastructure.Auth.SshProviders", Justification = "Ordinal comparison is implicit for SSH keys")]

// Setup Feature - Minor suppressions
[assembly: SuppressMessage("Design", "CA1805:Do not initialize unnecessarily", Scope = "member", Target = "~P:TenSecondTom.Features.Setup.Models.OptionalConfiguration.EnableTelemetry", Justification = "Explicit false initialization for clarity")]
[assembly: SuppressMessage("Usage", "CA2263:Prefer generic overload", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Both overloads are equivalent")]
[assembly: SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Performance difference negligible in UI code")]
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "Spectre.Console uses synchronous prompts")]
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features.Setup", Justification = "HTTP clients managed by HttpClientFactory, other resources have correct lifetime")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Infrastructure.Auth.SshAgentClient.SignDataAsync(System.Byte[],System.Byte[],System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Byte[]}", Justification = "Must handle all signing errors gracefully")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Infrastructure.Auth.SshAgentClient.Dispose", Justification = "Dispose must not throw exceptions")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Scope = "member", Target = "~M:TenSecondTom.Infrastructure.Auth.SshAgentClient.SignDataAsync(System.Byte[],System.Byte[],System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Byte[]}", Justification = "Null validation handled by conditional checks")]

// CQRS marker interfaces - empty by design
[assembly: SuppressMessage("Design", "CA1040:Avoid empty interfaces", Scope = "type", Target = "~T:TenSecondTom.Features.ThisWeek.Commands.IRequest`1", Justification = "Marker interface for CQRS pattern")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.ThisWeek.Commands.IRequest`1", Justification = "Public interface for CQRS pattern")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.ThisWeek.Commands.IRequestHandler`2", Justification = "Public interface for CQRS pattern")]

// Logging performance - acceptable tradeoff for clarity in handlers
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.Handle(TenSecondTom.Features.ThisWeek.Commands.CreateWeeklyReviewCommand,System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Shared.Results.Result{TenSecondTom.Shared.Models.WeeklyEntry}}", Justification = "Logging clarity preferred over performance optimization")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "member", Target = "~M:TenSecondTom.Features.Search.Handlers.SearchMemoriesQueryHandler.Handle(TenSecondTom.Features.Search.Queries.SearchMemoriesQuery,System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Shared.Results.Result{System.Collections.Generic.IReadOnlyList{TenSecondTom.Shared.Models.MemoryEntry}}}", Justification = "Logging clarity preferred over performance optimization")]

// Exception handling - necessary for resilient LLM response parsing
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.Handle(TenSecondTom.Features.ThisWeek.Commands.CreateWeeklyReviewCommand,System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Shared.Results.Result{TenSecondTom.Shared.Models.WeeklyEntry}}", Justification = "Must handle all exceptions to return Result<T> instead of throwing")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.ParseWeeklySummary(System.String)~TenSecondTom.Shared.Results.Result{TenSecondTom.Shared.Models.WeeklySummary}", Justification = "Must handle all parsing errors gracefully")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Infrastructure.Auth.SshAgentClient.RequestIdentitiesAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Boolean}", Justification = "SSH agent protocol requires catching all exceptions for graceful degradation")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Infrastructure.Auth.SshAgentClient.ConnectAsync(TenSecondTom.Infrastructure.Auth.SshAgentProvider,System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Boolean}", Justification = "SSH agent protocol requires catching all exceptions for graceful degradation")]

// Public API - SSH agent provider types
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.SshAgentProvider", Justification = "Public enum for configuration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.SshAgentProviderResolver", Justification = "Public utility class for provider resolution")]

// Culture-specific formatting - intentional for user-facing dates
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.AggregateDailyEntries(System.Collections.Generic.IReadOnlyList{TenSecondTom.Shared.Models.MemoryEntry})~System.String", Justification = "User-facing date format should respect current culture")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.RenderPrompt(TenSecondTom.Shared.Models.PromptTemplate,System.String,TenSecondTom.Shared.Models.DateRange,System.Int32)~System.String", Justification = "User-facing date format should respect current culture")]

// Shell feature - public API for DI registration and REPL functionality
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Models.ShellSession", Justification = "Public API for shell feature")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Models.SessionStatus", Justification = "Public API for shell feature")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Models.CommandHistoryEntry", Justification = "Public API for shell feature")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Models.CommandMetadata", Justification = "Public API for shell feature")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Models.AutocompleteSuggestion", Justification = "Public API for shell feature")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Models.CommandResult", Justification = "Public API for shell feature")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.IAutocompleteEngine", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.AutocompleteEngine", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.ISessionManager", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.SessionManager", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.ICommandRouter", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.CommandRouter", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.IReplLoop", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.ReplLoop", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.IOutputPaginator", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.OutputPaginator", Justification = "Public implementation for DI registration")]

// Shell feature - properties and arrays
[assembly: SuppressMessage("Performance", "CA1819:Properties should not return arrays", Scope = "member", Target = "~P:TenSecondTom.Features.Shell.Models.CommandMetadata.Aliases", Justification = "Array is intentional for collection initializers in static catalog")]

// Shell feature - string methods
[assembly: SuppressMessage("Globalization", "CA1307:The behavior of 'string.Contains(char)' could vary based on the current user's locale settings", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Models.CommandMetadata.IsValid~System.Boolean", Justification = "Char contains is culture-invariant, no StringComparison overload needed")]

// Shell feature - logging performance
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.ReplLoop", Justification = "Logging clarity preferred for shell operations")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.CommandRouter", Justification = "Logging clarity preferred for command routing")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "type", Target = "~T:TenSecondTom.Features.Shell.Services.OutputPaginator", Justification = "Logging clarity preferred for pagination")]

// Shell feature - exception handling
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.ReplLoop.RunAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Int32}", Justification = "REPL loop must handle all exceptions gracefully to maintain interactive session")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.CommandRouter.RouteAsync(System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Features.Shell.Models.CommandResult}", Justification = "Router must handle all exceptions to return CommandResult instead of throwing")]

// Shell feature - static members
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.ReplLoop.DisplayBanner", Justification = "Instance method for consistency with other display methods")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.ReplLoop.ReadInput~System.String", Justification = "Instance method for potential future use of session context")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.ReplLoop.DisplayResult(TenSecondTom.Features.Shell.Models.CommandResult)", Justification = "Instance method for consistency with other display methods")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.OutputPaginator.DisplayPagedAsync(System.Collections.Generic.List{System.String},System.Int32,System.Threading.CancellationToken)~System.Threading.Tasks.Task", Justification = "Instance method may need logger in future")]

// Shell feature - cancellation token propagation
[assembly: SuppressMessage("Reliability", "CA2016:Forward the CancellationToken parameter", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.CommandRouter.RouteAsync(System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Features.Shell.Models.CommandResult}", Justification = "System.CommandLine InvokeAsync handles cancellation internally via Console.CancelKeyPress")]






