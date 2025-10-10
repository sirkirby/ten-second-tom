namespace TenSecondTom.Shared.Secrets;

internal static class SecretsHelper
{
    /// <summary>
    /// Gets the User Secrets path for the specified secrets ID.
    /// This method works in self-contained/trimmed binaries without relying on assembly reflection.
    /// </summary>
    /// <param name="userSecretsId">The User Secrets ID.</param>
    /// <returns>Full path to the secrets.json file.</returns>
    public static string GetUserSecretsPath(string userSecretsId)
    {
        string userSecretsBasePath;

        if (OperatingSystem.IsWindows())
        {
            // Windows: %APPDATA%\Microsoft\UserSecrets\{userSecretsId}\secrets.json
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            userSecretsBasePath = Path.Combine(appData, "Microsoft", "UserSecrets");
        }
        else
        {
            // macOS/Linux: ~/.microsoft/usersecrets/{userSecretsId}/secrets.json
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            userSecretsBasePath = Path.Combine(home, ".microsoft", "usersecrets");
        }

        return Path.Combine(userSecretsBasePath, userSecretsId, "secrets.json");
    }
}