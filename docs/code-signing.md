# Windows Code Signing & SmartScreen Investigation

**Document Version:** 1.0  
**Target Release:** AIQuotaBar v0.2.0 Preview  
**Audience:** Repository Maintainers & Release Engineers  

---

## Executive Summary

When distributing Windows desktop applications directly via GitHub Releases, binary signing and reputation directly impact user friction. This document outlines the technical realities of Windows Defender SmartScreen, evaluates current code-signing options (including Microsoft Trusted Signing and traditional Authenticode), and recommends a pragmatic phased approach for AIQuotaBar.

---

## 1. What Happens with Unsigned Binaries

When a user downloads a `.zip` archive or `.exe` binary from a browser:

1. **Mark of the Web (MotW):** Windows attaches an NTFS Alternate Data Stream (`Zone.Identifier` with `ZoneId=3` for Internet) to the downloaded file and extracted contents.
2. **SmartScreen Evaluation:** When `AIQuotaBar.exe` is launched, Windows Defender SmartScreen queries Microsoft reputation servers using the file's SHA-256 hash and digital signature (if present).
3. **SmartScreen Warning:** If the executable is unsigned and lacks accumulated reputation in Microsoft's telemetry graph, Windows displays a full-screen prompt:
   > *"Windows protected your PC — Microsoft Defender SmartScreen prevented an unrecognized app from starting."*
4. **User Override:** The user must explicitly click **"More info"** and then **"Run anyway"** to execute the application.

---

## 2. Understanding SmartScreen Reputation

* **Reputation is Algorithmic:** SmartScreen calculates reputation dynamically based on telemetry from Windows users, download volume, clean execution history, and publisher certificate trust.
* **Unsigned Executables:** Every new compile or version change changes the binary hash. An unsigned binary must build reputation from scratch on each release, requiring dozens to hundreds of user overrides before warnings abate.
* **Signed Executables:** Code signing attaches reputation to the **publisher certificate identity** rather than solely to the individual binary hash. New versions signed with the same certificate inherit the publisher's established reputation.

---

## 3. Current Code-Signing Options Evaluated

### Option A: Microsoft Trusted Signing (Recommended Managed Path)

* **Overview:** Formerly known as *Azure Trusted Signing* (and *Azure Code Signing*), Microsoft Trusted Signing is Microsoft's fully managed, cloud-native code-signing service.
* **How It Works:** Certificates are generated on-demand backed by Microsoft-managed HSMs. Release workflows sign binaries using the official GitHub Action (`azure/trusted-signing-action`) or Azure CLI.
* **Identity Verification:** Requires an active Microsoft Azure subscription and identity proofing through Microsoft's validation partner (e.g., GlobalSign). Supports both registered organizations and individual developers.
* **Cost & Friction:**
  * Basic Tier: ~$9.99/month (covers up to 100 signatures per month).
  * No physical hardware tokens (USB dongles) required.
  * Friction: Requires identity verification review (business registration or government photo ID).
* **SmartScreen Impact:** Microsoft Trusted Signing certificates establish SmartScreen reputation significantly faster than unsigned binaries and provide full Authenticode trust.

### Option B: Traditional CA Authenticode Certificates (DigiCert, Sectigo, GlobalSign)

* **Baseline Requirements:** Per CA/Browser Forum rules effective June 1, 2023, all code-signing private keys (Standard and EV) must reside on certified hardware (FIPS 140-2 Level 2 or Common Criteria EAL 4+).
* **Cost:** ~$200–$600+ per year plus hardware token shipping.
* **Operational Friction:** Physical USB tokens cannot easily be used in cloud CI/CD pipelines (such as GitHub-hosted runners) without specialized cloud HSM hosting or self-hosted runners with remote key access.
* **SmartScreen Impact:** Standard certificates still require building initial reputation; EV certificates historically bypassed SmartScreen immediately, but Microsoft's current policy treats EV reputation as fast-accruing rather than an absolute Day 1 guarantee.

### Option C: Microsoft Store Distribution (MSIX)

* **Overview:** Apps distributed via the Microsoft Store are signed by Microsoft's Store infrastructure and do not trigger SmartScreen warnings.
* **Cost:** One-time $19 USD developer registration fee for individuals.
* **Friction:** Requires packaging as MSIX or Windows App SDK package and navigating Microsoft Store certification rules. May restrict certain standalone portable executable use cases.

---

## 4. Phased Recommendation

### Phase 1: First Public Preview (v0.2.0) — *Required Baseline*

For the initial open-source release candidate:

* **Unsigned Portable Distribution:** Distribute `AIQuotaBar.exe` inside a clean ZIP archive (`AIQuotaBar-v0.2.0-win-x64.zip`) accompanied by an authentic SHA-256 checksum file (`AIQuotaBar-v0.2.0-win-x64.zip.sha256`).
* **Clear Documentation:** Document the SmartScreen prompt clearly in `README.md` and release notes, explaining that users can click *"More info -> Run anyway"*.
* **Integrity Guarantee:** Provide SHA-256 checksums so users can verify the binary against GitHub Releases.
* **Zero Cost / Zero Delay:** Allows public release review without waiting for certificate identity proofing.

### Phase 2: Broader Distribution & Official Releases — *Recommended Next Step*

Before wider community distribution:

1. Enroll in **Microsoft Trusted Signing** via Azure.
2. Complete identity validation.
3. Configure GitHub repository secrets (`AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, etc.).
4. Uncomment the signing step in `.github/workflows/release.yml` using `azure/trusted-signing-action`.
5. Future releases will be automatically signed prior to ZIP archive creation and SHA-256 hashing.
