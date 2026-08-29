# Privacy Policy for AIQuotaBar

**Last updated:** August 2026  
**Publisher:** AGIFutures  
**Contact:** marc@agifutures.io  

---

## 1. Overview

AIQuotaBar is a lightweight, local-first Windows desktop widget designed to monitor AI subscription quotas and rate limits across developer tools installed on your computer.

Your privacy and security are fundamental principles of our design.

---

## 2. No Telemetry, Analytics, or Backend

* **Zero Telemetry:** AIQuotaBar contains no analytics, diagnostic beacons, crash reporting SDKs, or tracking code.
* **No Advertising:** AIQuotaBar is completely ad-free.
* **No Cloud Backend:** AGIFutures operates no cloud servers, account systems, or remote telemetry endpoints for AIQuotaBar.
* **No AIQuotaBar Account:** You do not need to register, create an account, or log in with AGIFutures to use AIQuotaBar.

---

## 3. Local-First Architecture & Provider Data

* **Local Communication:** AIQuotaBar checks quota information locally by communicating directly with the official command-line tools or desktop processes already installed and authenticated on your machine (such as OpenAI Codex, Google Antigravity, Claude Code, Grok Build, and GitHub Copilot).
* **Provider Authentication:** AIQuotaBar **never** reads, copies, or persists your passwords, API keys, session tokens, or provider credential files. All authentication is managed exclusively by the respective provider tools.
* **Network Egress:** AIQuotaBar itself makes no outbound network connections on its own behalf. Note that provider-owned CLI tools and processes communicate directly with their own service backends in accordance with their respective privacy policies.
* **No Data History Database:** AIQuotaBar does not maintain a database of historical prompts, model interactions, or usage history.

---

## 4. Local Settings & Storage

* **Local Preferences:** Your widget preferences (such as window position, width, compact mode, dock mode, always-on-top, and per-provider visibility preferences) are stored strictly on your local computer in:
  ```
  %LOCALAPPDATA%\AIQuotaBar\settings.json
  ```
* **Data Removal:** Deleting the `%LOCALAPPDATA%\AIQuotaBar\` directory or uninstalling the application completely removes all local settings stored by AIQuotaBar.

---

## 5. Third-Party Services

AIQuotaBar interacts only with provider software that you have independently installed and authenticated. Your use of third-party AI provider services remains governed by their respective terms of service and privacy policies:

* OpenAI (Codex / ChatGPT)
* Google (Antigravity / Gemini)
* Anthropic (Claude Code)
* xAI (Grok Build)
* GitHub / Microsoft (GitHub Copilot)

---

## 6. Contact

If you have any questions or feedback regarding this Privacy Policy, please contact:

**AGIFutures**  
Email: [marc@agifutures.io](mailto:marc@agifutures.io)  
Website: [https://github.com/MDoots/AIQuotaBar](https://github.com/MDoots/AIQuotaBar)
