// This file is used to configure code analysis
using System.Diagnostics.CodeAnalysis;

// Namespace naming - "Shared" is intentional and commonly used in .NET projects
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Shared", Justification = "Shared is a standard namespace pattern in .NET")]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Shared.Results", Justification = "Shared is a standard namespace pattern in .NET")]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Shared.Models", Justification = "Shared is a standard namespace pattern in .NET")]

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

// Infrastructure interfaces
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Prompts.IPromptTemplateLoader", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Prompts.EmbeddedPromptTemplateLoader", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.IAuthenticationService", Justification = "Public interface for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Auth.SshKeyAuthenticationService", Justification = "Public implementation for DI registration")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Infrastructure.Logging.LoggingConfiguration", Justification = "Public utility class for bootstrapping")]

// CQRS marker interfaces - empty by design
[assembly: SuppressMessage("Design", "CA1040:Avoid empty interfaces", Scope = "type", Target = "~T:TenSecondTom.Features.ThisWeek.Commands.IRequest`1", Justification = "Marker interface for CQRS pattern")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.ThisWeek.Commands.IRequest`1", Justification = "Public interface for CQRS pattern")]
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Scope = "type", Target = "~T:TenSecondTom.Features.ThisWeek.Commands.IRequestHandler`2", Justification = "Public interface for CQRS pattern")]

// Logging performance - acceptable tradeoff for clarity in handlers
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.Handle(TenSecondTom.Features.ThisWeek.Commands.CreateWeeklyReviewCommand,System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Shared.Results.Result{TenSecondTom.Shared.Models.WeeklyEntry}}", Justification = "Logging clarity preferred over performance optimization")]

// Exception handling - necessary for resilient LLM response parsing
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.Handle(TenSecondTom.Features.ThisWeek.Commands.CreateWeeklyReviewCommand,System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Shared.Results.Result{TenSecondTom.Shared.Models.WeeklyEntry}}", Justification = "Must handle all exceptions to return Result<T> instead of throwing")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.ParseWeeklySummary(System.String)~TenSecondTom.Shared.Results.Result{TenSecondTom.Shared.Models.WeeklySummary}", Justification = "Must handle all parsing errors gracefully")]

// Culture-specific formatting - intentional for user-facing dates
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.AggregateDailyEntries(System.Collections.Generic.IReadOnlyList{TenSecondTom.Shared.Models.MemoryEntry})~System.String", Justification = "User-facing date format should respect current culture")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Scope = "member", Target = "~M:TenSecondTom.Features.ThisWeek.Handlers.CreateWeeklyReviewHandler.RenderPrompt(TenSecondTom.Shared.Models.PromptTemplate,System.String,TenSecondTom.Shared.Models.DateRange,System.Int32)~System.String", Justification = "User-facing date format should respect current culture")]



