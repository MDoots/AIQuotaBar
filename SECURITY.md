# Security Policy

The **AIQuotaBar** project takes security and privacy seriously. Because AIQuotaBar interacts with local AI tooling environments, maintaining rigorous isolation, credential safety, and process integrity is essential.

---

## 1. Supported Versions

Security fixes and patches are provided for the latest public preview or release build:

| Version | Supported |
| :--- | :--- |
| Latest Release / Preview (>= 1.0.0) | :white_check_mark: |
| Older Previews (< 1.0.0) | :x: |

Users are encouraged to run the most recent release before reporting issues. Older preview builds do not receive backported patches.

---

## 2. Reporting a Vulnerability

If you discover a security vulnerability in AIQuotaBar:

1. **Do NOT open a public GitHub issue.** Public issues are visible to everyone and should never contain sensitive vulnerability details, tokens, or credentials.
2. **Use GitHub Private Vulnerability Reporting:** Once the repository is publicly accessible, navigate to the **Security** tab of this repository on GitHub and select **"Report a vulnerability"** to submit an advisory securely and privately.
3. If Private Vulnerability Reporting is unavailable, please hold submission until the repository owner enables private reporting, or contact the maintainer through private repository coordination.

### What to Include in Your Report
* Description of the vulnerability and potential impact.
* Step-by-step reproduction steps or minimal proof-of-concept.
* Affected operating system and environment details.
* Any proposed mitigation or patch if available.

---

## 3. Sensitive Focus Areas

We specifically prioritize investigations into:

* **Credential & Token Safety:** Any unintended exposure, parsing, logging, or leakage of authentication tokens, API keys, or provider session files.
* **Process & Command Injection:** Unsafe parameter construction or argument escaping when invoking local provider CLI tools or app-servers.
* **Executable Resolution & PATH Hijacking:** Insecure binary resolution that could allow arbitrary binary execution from untrusted local working directories.
* **Path Traversal & Settings Security:** Unsanitized file system operations when reading or writing `%LOCALAPPDATA%\AIQuotaBar\settings.json`.
* **Startup Persistence Abuse:** Exploits involving the Windows Registry `Run` key or startup manager.

---

## 4. Privacy & Telemetry Invariant

AIQuotaBar has no remote backend, telemetry, or analytics service. It makes no outbound network connections on its own behalf. All provider data is gathered exclusively through local IPC with the user's officially installed provider tools.
