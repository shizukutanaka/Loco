# Security Policy

## Overview

Loco is designed with security as a top priority. This document outlines the security features, best practices, and how to report security vulnerabilities.

## Security Features

### 1. Input Validation

**All user inputs are validated and sanitized**:
- Command-line arguments are validated
- Configuration files are validated on load
- File paths are checked against security rules
- Process commands are whitelisted

**Configuration**:
```json
{
  "enableInputValidation": true
}
```

### 2. Access Control

**Path-Based Security**:
- **Allowed Paths**: Whitelist of accessible directories
- **Forbidden Paths**: Blacklist of protected directories (overrides allowed paths)

**Default Protection**:
- System directories (C:\Windows, C:\Program Files)
- Sensitive folders (.ssh, credentials, passwords)
- Configuration directories

**Configuration Example**:
```json
{
  "allowedPaths": [
    "C:/Data/Projects",
    "./workspace"
  ],
  "forbiddenPaths": [
    "C:/Windows",
    "C:/Program Files",
    ".ssh",
    "passwords"
  ]
}
```

### 3. Process Execution Security

**Command Whitelisting**:
- Only approved commands can be executed
- Default whitelist: cmd.exe, powershell.exe, dotnet.exe, git.exe, etc.
- Custom executables (.exe, .bat, .cmd) can be executed with proper configuration

**Timeout Enforcement**:
- All process executions have mandatory timeouts
- Default: 300 seconds (configurable)
- Prevents hung processes and resource exhaustion

**Output Limiting**:
- Process output is truncated to prevent memory exhaustion
- Maximum output size: 500 characters per stream

### 4. Rate Limiting

**Protection Against Abuse**:
- Built-in rate limiting per operation
- Default: 100 requests per minute
- Prevents DoS attacks and resource exhaustion

**Configuration**:
```json
{
  "rateLimitPerMinute": 100
}
```

### 5. Audit Logging

**Complete Audit Trail**:
- All operations are logged with timestamps
- Security events are logged with context
- Failed access attempts are recorded
- Sensitive data is masked in logs

**Configuration**:
```json
{
  "enableAuditLogging": true,
  "logDirectory": "C:/Logs/Loco",
  "logRetentionDays": 30
}
```

### 6. Data Protection

**Secure Deletion**:
- DoD 5220.22-M standard (3-pass overwrite)
- Random data overwrite + zero fill
- File metadata is cleared

**Password Security**:
- SHA256 with random salt (16 bytes)
- Salts are stored with hashes
- Constant-time comparison prevents timing attacks

**Secure Token Generation**:
- Cryptographically secure random number generator
- Base64 encoding with URL-safe characters
- Configurable length (default: 32 bytes)

### 7. Resource Limits

**Memory Protection**:
```json
{
  "memoryLimitMB": 512,
  "enableMemoryOptimization": true
}
```

**File Size Limits**:
```json
{
  "maxFileSizeBytes": 1073741824  // 1GB
}
```

**Concurrent Execution Limits**:
```json
{
  "maxConcurrentFlows": 10
}
```

## Security Best Practices

### For Production Deployments

1. **Run with Minimal Privileges**
   - Create a dedicated service account
   - Grant only necessary permissions
   - Avoid running as Administrator

2. **Configure Path Restrictions**
   ```json
   {
     "allowedPaths": ["./data", "./workspace"],
     "forbiddenPaths": [
       "C:/Windows",
       "C:/Program Files",
       ".ssh",
       "credentials"
     ]
   }
   ```

3. **Enable All Security Features**
   ```json
   {
     "enableInputValidation": true,
     "enableAuditLogging": true,
     "rateLimitPerMinute": 100,
     "enableCircuitBreaker": true
   }
   ```

4. **Secure Configuration Files**
   - Store configuration outside web root
   - Set restrictive file permissions (read-only for service account)
   - Use environment variables for sensitive data
   - Never commit credentials to source control

5. **Monitor and Review**
   - Regularly review audit logs
   - Monitor failed access attempts
   - Check resource usage patterns
   - Update and patch regularly

6. **Network Security**
   - Use firewall rules to restrict access
   - Disable unnecessary network features
   - Use HTTPS for web API (if used)
   - Implement network segmentation

### For Development

1. **Use Separate Configurations**
   - Development config with relaxed restrictions
   - Production config with strict security
   - Never use production config in development

2. **Test Security Features**
   - Verify path restrictions work
   - Test rate limiting
   - Validate input sanitization
   - Check audit logging

3. **Code Review Focus**
   - Review all file system operations
   - Check process execution security
   - Validate input handling
   - Verify error messages don't leak information

## Configuration Validation

Run configuration validation before deployment:

```powershell
# Validate configuration
Loco.Cli.exe info

# Check for security issues
# Look for warnings about:
# - Disabled input validation
# - Missing audit logging
# - Unrestricted paths
# - Weak rate limits
```

## Security Checklist for Production

- [ ] Input validation enabled
- [ ] Audit logging enabled
- [ ] Path restrictions configured
- [ ] Rate limiting configured
- [ ] Memory limits set
- [ ] Timeout values configured
- [ ] Circuit breaker enabled
- [ ] Service account created with minimal privileges
- [ ] Configuration file permissions restricted
- [ ] Firewall rules configured
- [ ] Log retention configured
- [ ] Monitoring and alerting set up
- [ ] Backup procedures in place
- [ ] Incident response plan documented

## Known Limitations

1. **Email and Database Actions**: Currently stubs - require additional configuration and security setup
2. **Process Whitelisting**: Custom commands require explicit configuration
3. **Network Operations**: No built-in TLS certificate validation for HTTPS operations

## Reporting Security Vulnerabilities

**DO NOT** report security vulnerabilities through public GitHub issues.

Instead:
1. Document the vulnerability with detailed steps to reproduce
2. Include potential impact assessment
3. Suggest possible mitigations if available
4. Allow reasonable time for patches before disclosure

## Security Updates

- Review and update dependencies regularly
- Monitor .NET security advisories
- Apply Windows security patches
- Update Loco when security patches are released

## Compliance Considerations

### Government Use

Loco is designed with government-grade security in mind:
- DoD 5220.22-M compliant secure deletion
- Complete audit trail
- Strong access controls
- Input validation and sanitization

### Enterprise Use

Suitable for enterprise environments:
- Role-based access control (via path restrictions)
- Comprehensive logging
- Resource management
- Fault tolerance

### Data Protection

- No data is sent to external services
- All operations are local
- Sensitive data can be encrypted at rest
- Secure deletion available

## Additional Resources

- [Configuration Guide](docs/CONFIGURATION.md)
- [User Manual](docs/USER_MANUAL.md)
- [Developer Guide](docs/DEVELOPER.md)

---

**Last Updated**: 2024-01
**Security Contact**: Review security policy for contact information