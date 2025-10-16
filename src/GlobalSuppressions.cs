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
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Shared.Constants", Justification = "Shared is a standard namespace pattern in .NET")]

// CQRS marker interfaces - empty by design
[assembly: SuppressMessage("Design", "CA1040:Avoid empty interfaces", Scope = "type", Target = "~T:TenSecondTom.Features.ThisWeek.Commands.IRequest`1", Justification = "Marker interface for CQRS pattern")]
[assembly: SuppressMessage("Design", "CA1040:Avoid empty interfaces", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Features", Justification = "CQRS command/query marker interfaces are empty by design")]

// Shell feature - array properties intentional for collection initializers
[assembly: SuppressMessage("Performance", "CA1819:Properties should not return arrays", Scope = "member", Target = "~P:TenSecondTom.Features.Shell.Models.CommandMetadata.Aliases", Justification = "Array is intentional for collection initializers in static catalog")]

// Setup Feature - explicit false initialization for clarity in configuration
[assembly: SuppressMessage("Design", "CA1805:Do not initialize unnecessarily", Scope = "member", Target = "~P:TenSecondTom.Features.Setup.Models.OptionalConfiguration.EnableTelemetry", Justification = "Explicit false initialization for clarity")]

// Shell/Setup features - instance methods for consistency and future extensibility
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.ReplLoop.DisplayBanner", Justification = "Instance method for consistency with other display methods")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.ReplLoop.ReadInput~System.String", Justification = "Instance method for potential future use of session context")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.ReplLoop.DisplayResult(TenSecondTom.Features.Shell.Models.CommandResult)", Justification = "Instance method for consistency with other display methods")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.OutputPaginator.DisplayPagedAsync(System.Collections.Generic.List{System.String},System.Int32,System.Threading.CancellationToken)~System.Threading.Tasks.Task", Justification = "Instance method may need logger in future")]

// Shell feature - System.CommandLine handles cancellation internally
[assembly: SuppressMessage("Reliability", "CA2016:Forward the CancellationToken parameter", Scope = "member", Target = "~M:TenSecondTom.Features.Shell.Services.CommandRouter.RouteAsync(System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task{TenSecondTom.Features.Shell.Models.CommandResult}", Justification = "System.CommandLine InvokeAsync handles cancellation internally via Console.CancelKeyPress")]
