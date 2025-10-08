# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in Ten Second Tom, please report it by emailing the maintainers directly. Do not open a public issue.

**Email**: [Contact repository maintainers via GitHub]

We aim to respond to security reports within 48 hours and will work with you to understand and address the issue.

---

## Authentication & Key Management

### SSH Key Authentication (v1.0)

Ten Second Tom uses SSH key-based authentication for local session management. This provides secure, password-less authentication using standard cryptographic keys.

**Supported Key Types**:
- Ed25519 (preferred): `~/.ssh/id_ed25519`
- RSA 2048+ bits: `~/.ssh/id_rsa`

### Lost SSH Key Recovery

If you lose access to your SSH key, follow this recovery procedure:

#### Manual Recovery Process (v1.0)

**IMPORTANT**: In version 1.0, SSH key recovery requires manual intervention by repository maintainers.

1. **Verify Your Identity**
   - Contact the Ten Second Tom maintainers via GitHub Issues (private communication)
   - Provide proof of ownership:
     - GitHub account associated with your usage
     - Approximate date range of last successful login
     - Sample memory entry content (non-sensitive) to verify data ownership
   
2. **Key Rotation Request**
   - Generate a new SSH key pair on your system:
     ```bash
     ssh-keygen -t ed25519 -C "your_email@example.com"
     ```
   - Provide your new public key fingerprint to maintainers

3. **Session Reset**
   - Maintainers will provide instructions to manually reset your session
   - Remove old session file: `rm ~/.tom/session.json`
   - Re-authenticate with new SSH key: `tom login`

4. **Data Recovery**
   - Your memory entries remain intact in `.memory/` directory
   - New session will have access to all existing memory entries
   - No data loss occurs during key rotation

#### Future Enhancements (v2.0+)

Planned improvements for self-service recovery:
- Security questions during initial setup
- Recovery codes (one-time use tokens)
- Multi-factor authentication options
- Automated key rotation workflow

### Session Security

**Session Duration**: Persistent until explicit logout
- Sessions do not expire automatically
- Session tokens stored in `~/.tom/session.json` (local file system)
- Session token format: SHA256 hash of SSH key fingerprint + timestamp

**Best Practices**:
1. Always run `tom logout` when finished using shared computers
2. Protect your SSH key with a strong passphrase
3. Regularly rotate SSH keys (every 6-12 months)
4. Monitor session file permissions: `chmod 600 ~/.tom/session.json`

### Cryptographic Implementation

#### Ed25519 Signature Verification

**Library**: NSec.Cryptography 25.4.0+  
**Algorithm**: Ed25519 (RFC 8032)  
**Purpose**: SSH agent authentication signature verification

Ten Second Tom uses NSec.Cryptography for cryptographic verification of Ed25519 signatures during SSH agent authentication. This implementation provides:

**Security Properties**:
- **RFC 8032 Compliance**: Implements the official Ed25519 standard
- **Audited Foundation**: Built on libsodium (C library audited for 10+ years)
- **Constant-Time Operations**: Prevents timing-based side-channel attacks
- **128-bit Security Level**: Industry-standard cryptographic strength
- **No Signature Malleability**: Ed25519 signatures cannot be forged or modified
- **Tamper Detection**: Any modification to signature is immediately detected

**Validation Requirements**:
- Signature length: Exactly 64 bytes
- Public key length: Exactly 32 bytes (extracted from SSH format)
- Cryptographic verification: All signatures must pass NSec verification
- No fallback modes: Simplified validation is never used

**Implementation Details**:
- Signature verification: `NSec.Cryptography.SignatureAlgorithm.Ed25519.Verify()`
- Public key import: SSH wire format → raw 32-byte Ed25519 key
- Error handling: ArgumentException, CryptographicException caught and logged
- Security events: Failed verifications logged at Warning level for audit

**Performance**: Signature verification completes in < 1ms (libsodium optimized implementation).

**Dependency Chain**:
- NSec.Cryptography 25.4.0 (MIT License)
  - libsodium 1.0.20.1 (embedded, ISC License)

Failed signature verifications are logged as security events for audit and monitoring purposes.

---

## Data Security

### Local Storage

All memory entries are stored locally in `.memory/` directory as markdown files. This data is:
- **Not encrypted at rest** in v1.0 (file system encryption recommended)
- **Never transmitted** over network (all processing is local)
- **Protected by file system permissions** (recommended: `chmod 700 .memory/`)

**Recommendations**:
1. Enable full-disk encryption (FileVault on macOS, BitLocker on Windows)
2. Set restrictive permissions on `.memory/` directory
3. Exclude `.memory/` from cloud sync if it contains sensitive information
4. Regularly backup `.memory/` directory to encrypted storage

### LLM API Communication

When using OpenAI or Anthropic APIs:
- **API keys stored securely** using .NET User Secrets (development) or environment variables (production)
- **TLS/HTTPS used** for all API communication
- **User input transmitted** to LLM providers for summarization
- **No data persistence** by Ten Second Tom on remote servers (subject to LLM provider policies)

**Privacy Considerations**:
- Review OpenAI's data usage policy: https://openai.com/policies/privacy-policy
- Review Anthropic's data usage policy: https://www.anthropic.com/privacy
- Consider using local LLM providers for sensitive content (future enhancement)

---

## Secrets Management

### Development

Use .NET User Secrets for development:
```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key-here"
dotnet user-secrets set "Anthropic:ApiKey" "your-api-key-here"
```

### Production & Installed Application

Use environment variables. This is the recommended approach for both production servers and for users who have installed the application via a package manager (e.g., Homebrew, Winget).
```bash
export TENSECONDTOM__OPENAI__APIKEY="your-api-key-here"
export TENSECONDTOM__ANTHROPIC__APIKEY="your-api-key-here"
```

**Never commit secrets** to version control. The `.gitignore` file excludes:
- `appsettings.Development.json` (if it contains secrets)
- `.memory/` directory (user data)
- `session.json` (authentication tokens)

---

## Security Checklist

Before deploying or sharing Ten Second Tom:

- [ ] SSH key is passphrase-protected
- [ ] Session file permissions set to 600 (`chmod 600 ~/.tom/session.json`)
- [ ] Memory directory permissions set to 700 (`chmod 700 .memory/`)
- [ ] Full-disk encryption enabled on local machine
- [ ] LLM API keys stored in User Secrets or environment variables
- [ ] `.memory/` directory excluded from public cloud sync (if applicable)
- [ ] Regular backups of `.memory/` to encrypted storage
- [ ] Logout performed after each session on shared computers

---

## Known Security Considerations

### v1.0 Limitations

1. **No encryption at rest**: Memory entries stored as plain text markdown files
   - Mitigation: Use full-disk encryption
   
2. **Session persistence**: Sessions do not auto-expire
   - Mitigation: Always logout when finished
   
3. **Manual key recovery**: Lost SSH keys require maintainer intervention
   - Mitigation: Backup SSH keys securely
   
4. **LLM data transmission**: User input sent to third-party APIs
   - Mitigation: Review provider privacy policies before use

### Planned Security Enhancements (v2.0+)

- Optional encryption at rest (using SSH key for encryption)
- Session expiration configuration
- Self-service key recovery with security questions
- Local LLM provider support (no external API calls)
- Audit logging for security events

---

## Contact

For security concerns, questions, or private vulnerability reports:
- **GitHub Issues**: Use "Security" label for non-sensitive questions
- **Private Report**: Contact maintainers directly via GitHub profile

**Last Updated**: October 2, 2025  
**Version**: 1.0.0
