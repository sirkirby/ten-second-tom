namespace TenSecondTom.Infrastructure.Auth.Constants;

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
    /// SSH agent socket path segments for different platforms and providers.
    /// </summary>
    public static class AgentPaths
    {
        /// <summary>
        /// macOS: 1Password SSH agent socket path components.
        /// Full path: ~/Library/Group Containers/2BUA8C4S2C.com.1password/t/agent.sock
        /// </summary>
        public static class OnePassword
        {
            /// <summary>
            /// macOS: Directory name for Group Containers.
            /// </summary>
            public const string MacGroupContainers = "Library/Group Containers";

            /// <summary>
            /// macOS: 1Password container identifier.
            /// </summary>
            public const string MacContainerId = "2BUA8C4S2C.com.1password";

            /// <summary>
            /// macOS: Subdirectory within container.
            /// </summary>
            public const string MacSubdirectory = "t";

            /// <summary>
            /// macOS: Socket filename.
            /// </summary>
            public const string MacSocketFile = "agent.sock";

            /// <summary>
            /// Linux: Directory name for 1Password agent.
            /// </summary>
            public const string LinuxDirectory = ".1password";

            /// <summary>
            /// Linux: Socket filename.
            /// </summary>
            public const string LinuxSocketFile = "agent.sock";

            /// <summary>
            /// Windows: Uses standard SSH agent pipe via SSH_AUTH_SOCK environment variable.
            /// </summary>
            public const string WindowsPipe = "\\\\.\\pipe\\openssh-ssh-agent";
        }

        /// <summary>
        /// macOS: Secretive SSH agent socket path components.
        /// Full path: ~/Library/Containers/com.maxgoedjen.Secretive.SecretAgent/Data/socket.ssh
        /// Note: Secretive is macOS-only.
        /// </summary>
        public static class Secretive
        {
            /// <summary>
            /// macOS: Directory name for Containers.
            /// </summary>
            public const string MacContainers = "Library/Containers";

            /// <summary>
            /// macOS: Secretive container identifier.
            /// </summary>
            public const string MacContainerId = "com.maxgoedjen.Secretive.SecretAgent";

            /// <summary>
            /// macOS: Data subdirectory.
            /// </summary>
            public const string MacDataDirectory = "Data";

            /// <summary>
            /// macOS: Socket filename.
            /// </summary>
            public const string MacSocketFile = "socket.ssh";
        }

        /// <summary>
        /// System SSH agent environment variable name.
        /// Platform-agnostic: uses SSH_AUTH_SOCK on Unix systems, SSH_AUTH_SOCK or named pipe on Windows.
        /// </summary>
        public const string SystemAgentEnvVar = "SSH_AUTH_SOCK";
    }

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
