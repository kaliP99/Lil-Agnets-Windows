# Security Policy

We take the security of Lil' Agents Windows seriously. Since this application runs on local desktop environments, processes local files, and interacts with local or cloud API endpoints, maintaining a secure runtime environment is critical.

This document describes how to report vulnerabilities and what versions are currently supported.

---

## Supported Versions

Only the latest release version of Lil' Agents Windows is actively supported with security updates.

| Version | Supported          |
| ------- | ------------------ |
| v1.0.x  | ✅ Yes             |
| < v1.0  | ❌ No              |

---

## Reporting a Vulnerability

**Please do not open a public GitHub issue for security-related bugs.**

If you discover a security vulnerability (such as API key leakage, buffer overflows in document parsers, or arbitrary code execution paths in script runners), please report it responsibly:

1. **Email us**: Send a detailed email to security-report@example.com (or open a private security draft under the repository's Security tab if enabled).
2. **Details to include**:
   * A clear description of the vulnerability.
   * Steps to reproduce (including configuration files, document inputs, or payloads).
   * Potential impact (e.g., local file disclosure, key interception).
   * Any suggested remediations.

### Our Commitment
Once a report is received, our triage team will:
* Acknowledge receipt within **48 hours**.
* Provide a status update and estimated patch timeline within **7 days**.
* Keep you informed as a patch is tested and readied for release.
* Credit you in the release notes (unless you request anonymity).

Please allow us reasonable time to investigate and resolve the issue before publishing details of the vulnerability.
