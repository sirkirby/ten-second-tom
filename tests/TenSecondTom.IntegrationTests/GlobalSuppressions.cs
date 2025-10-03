using System.Diagnostics.CodeAnalysis;

// CA1515: Making test helpers internal would limit their reusability across test projects
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Test helpers are public for reusability", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.IntegrationTests.TestHelpers")]

// CA1707: Test method naming convention uses underscores for readability
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention", Scope = "namespaceanddescendants", Target = "~N:TenSecondTom.IntegrationTests")]

// CA1031: Catching general exceptions in Dispose is acceptable for cleanup
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Ignore cleanup errors in test infrastructure", Scope = "member", Target = "~M:TenSecondTom.IntegrationTests.TestHelpers.TemporaryTestDirectory.Dispose")]
