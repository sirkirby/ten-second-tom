// This file is used to configure code analysis for test projects
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Make internals visible to Moq for mocking internal interfaces
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

// Test method naming - underscores are standard in test method names for readability
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Tests", Justification = "Test methods use underscores for readability")]

// Ed25519SignatureVerificationTests - test vectors will be used once NSec implementation is complete
[assembly: SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Scope = "type", Target = "~T:TenSecondTom.Tests.Infrastructure.Auth.Ed25519SignatureVerificationTests", Justification = "Test stub - variables used once implementation complete")]
[assembly: SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Scope = "type", Target = "~T:TenSecondTom.Tests.Infrastructure.Auth.Ed25519SignatureVerificationTests", Justification = "Test stub - RFC 8032 test vectors used once implementation complete")]
[assembly: SuppressMessage("Performance", "CA1823:Avoid unused private fields", Scope = "type", Target = "~T:TenSecondTom.Tests.Infrastructure.Auth.Ed25519SignatureVerificationTests", Justification = "Test stub - RFC 8032 test vectors used once implementation complete")]
