using Xunit;

// Disables parallel test execution for this assembly to prevent race conditions
// when tests interact with shared filesystem-based resources (e.g., User Secrets store).
[assembly: CollectionBehavior(DisableTestParallelization = true)]