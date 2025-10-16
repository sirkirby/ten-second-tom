// This file is used to configure code analysis for integration test projects
using System.Diagnostics.CodeAnalysis;

// Test method naming - underscores are standard in test method names for readability
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.IntegrationTests", Justification = "Test methods use underscores for readability")]
