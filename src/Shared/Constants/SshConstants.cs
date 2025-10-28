namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Constants for SSH agent configuration and authentication.
/// </summary>
public static class SshConstants
{
    /// <summary>
    /// Default SSH agent provider mode.
    /// Auto-detect will try to find the best available agent (1Password, ssh-agent, etc.).
    /// </summary>
    public const string DefaultAgentProvider = "Auto";

    /// <summary>
    /// SSH key type identifiers used in public key formats.
    /// </summary>
    public static class KeyTypes
    {
        /// <summary>
        /// ED25519 key type (recommended for modern systems).
        /// </summary>
        public const string Ed25519 = "ssh-ed25519";

        /// <summary>
        /// RSA key type (widely supported legacy format).
        /// </summary>
        public const string Rsa = "ssh-rsa";

        /// <summary>
        /// DSA key type (deprecated, legacy systems only).
        /// </summary>
        public const string Dsa = "ssh-dss";

        /// <summary>
        /// ECDSA NIST P-256 curve key type.
        /// </summary>
        public const string EcdsaNistP256 = "ecdsa-sha2-nistp256";

        /// <summary>
        /// ECDSA NIST P-384 curve key type.
        /// </summary>
        public const string EcdsaNistP384 = "ecdsa-sha2-nistp384";

        /// <summary>
        /// ECDSA NIST P-521 curve key type.
        /// </summary>
        public const string EcdsaNistP521 = "ecdsa-sha2-nistp521";
    }

    /// <summary>
    /// SSH key type prefixes used for detection.
    /// </summary>
    public static class KeyPrefixes
    {
        /// <summary>
        /// Prefix for most SSH key types (ssh-ed25519, ssh-rsa, ssh-dss).
        /// </summary>
        public const string Ssh = "ssh-";

        /// <summary>
        /// Prefix for ECDSA key types (ecdsa-sha2-nistp256, etc.).
        /// </summary>
        public const string Ecdsa = "ecdsa-";
    }
}
