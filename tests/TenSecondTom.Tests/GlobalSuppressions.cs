// This file is used to configure code analysis for test projects
using System.Diagnostics.CodeAnalysis;

// Namespace naming - "Shared" is intentional and standard
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:TenSecondTom.Tests.Unit.Shared", Justification = "Shared is a standard namespace pattern")]

// Test method naming - underscores are standard in test method names for readability
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.Tests", Justification = "Test methods use underscores for readability")]
