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



