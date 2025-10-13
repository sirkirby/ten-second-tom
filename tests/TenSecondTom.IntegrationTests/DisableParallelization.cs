using Xunit;

// Disables parallel test execution for this assembly to prevent race conditions
// when integration tests interact with shared filesystem-based resources (e.g., User Secrets store).
// Integration tests that write to ~/.microsoft/usersecrets/ten-second-tom-secrets/ need to run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
