# Windows Code Signing & SmartScreen Investigation

**Document Version:** 1.1  
**Target Release:** Candidate-specific; verify the version and artifact record before release
**Audience:** Repository Maintainers & Release Engineers  

---

## Executive Summary

When distributing Windows desktop binaries directly via GitHub Releases, digital signing and Windows Defender SmartScreen directly impact the first-run user experience. This document outlines the technical distinctions between Authenticode signature verification and SmartScreen reputation, and evaluates current code-signing mechanisms including Microsoft Artifact Signing and traditional Authenticode.

---

## 1. Authenticode vs. Microsoft Defender SmartScreen

It is critical to distinguish between signature validity and reputation:

* **Authenticode Code Signing:** Validates the digital integrity of the binary (ensuring it has not been altered or corrupted in transit) and cryptographically links the binary to an identity-verified publisher.
* **Microsoft Defender SmartScreen:** A separate, cloud-telemetry reputation system. SmartScreen evaluates file hashes, download frequency, telemetry history, and publisher certificate trust.
* **SmartScreen Warning on Signed Apps:** A valid Authenticode signature—including one from an Extended Validation (EV) certificate or Microsoft Artifact Signing—**does not guarantee immediate SmartScreen trust on Day 1**. A newly compiled binary or a new publisher identity may still produce an *"unrecognized app"* warning until sufficient clean download and execution reputation has accumulated across Windows systems.

---

## 2. What Happens with Unsigned Binaries

When users download a `.zip` archive or `.exe` binary from a browser:

1. **Mark of the Web (MotW):** Windows applies an NTFS Alternate Data Stream (`Zone.Identifier` with `ZoneId=3` for Internet).
2. **SmartScreen Check:** When `AIQuotaBar.exe` is launched, SmartScreen evaluates the SHA-256 hash and digital signature against Microsoft's reputation service.
3. **SmartScreen Warning:** If the file is unsigned and lacks established reputation, Windows presents a warning dialog:
   > *"Windows protected your PC — Microsoft Defender SmartScreen prevented an unrecognized app from starting."*
4. **User Override:** Users can click **"More info"** followed by **"Run anyway"** to execute the binary.

> [!IMPORTANT]
> Users should **never** be instructed to disable Windows Defender SmartScreen. For open-source tools, users should verify that the executable originated from the canonical GitHub repository and matches the official published SHA-256 checksum.

---

## 3. Evaluation of Code-Signing Options

### Option A: Microsoft Artifact Signing (formerly Trusted Signing)

* **Overview:** Currently named **Artifact Signing** (formerly *Trusted Signing*, *Azure Trusted Signing*, and *Azure Code Signing*), this is Microsoft's fully managed, cloud-native signing service.
* **Architecture:** Cryptographic keys remain secured within Microsoft-managed FIPS 140-2 Level 3 HSMs. Binaries are signed dynamically during CI/CD pipelines via official actions (`azure/trusted-signing-action`) or the Azure CLI.
* **Identity Verification:** Requires an active Microsoft Azure subscription and identity proofing through Microsoft's validation partner (e.g., GlobalSign). Supports both registered organizations and individual developers.
* **Cost & Operational Profile:**
  * Basic Tier: ~$9.99/month (covers up to 100 signatures per month).
  * No physical USB HSM dongles or local key management required.
* **Reputation Reality:** Provides trusted publisher identity and Authenticode compliance, but like all certificates, SmartScreen reputation still accrues organically over time.

### Option B: Traditional CA Authenticode Certificates (DigiCert, Sectigo, GlobalSign)

* **Hardware Token Mandate:** Per CA/Browser Forum rules effective June 1, 2023, private keys for both Standard and EV code-signing certificates must be stored on physical FIPS 140-2 Level 2+ hardware crypto-tokens (USB dongles or cloud HSMs).
* **Cost:** ~$200–$600+ annually plus token shipping and maintenance fees.
* **Operational Friction:** Physical USB tokens cannot be integrated directly into cloud-hosted GitHub Actions runners without dedicated hardware hosting.
* **EV Reality:** Current Microsoft guidance clarifies that **EV certificates no longer automatically bypass SmartScreen reputation checks**. Purchasing an expensive EV certificate solely in hopes of bypassing SmartScreen is not recommended.

### Option C: Microsoft Store Distribution (MSIX)

* **Overview:** Applications packaged as MSIX and distributed via the Microsoft Store are signed by Microsoft and do not trigger SmartScreen warnings.
* **Cost:** $19 USD one-time registration fee for individuals.
* **Friction:** Requires MSIX packaging, sandboxing compliance, and submission to the Microsoft Partner Center review process.

---

## 4. Candidate Signing Status

Do not infer signing status or a release version from this document. For each candidate, record whether the exact portable archive, executable, and package are signed, by whom, and through which certificate or Store route. If an unsigned portable candidate is approved for local preview, document that fact in its release record:

1. **Distribution Integrity:**
   * Distributed exclusively via the canonical GitHub repository (`https://github.com/MDoots/AIQuotaBar`).
   * Packaged in a clean ZIP archive whose candidate version and exact contents are recorded.
   * Accompanied by an authoritative SHA-256 checksum for the frozen archive.
2. **Transparent Documentation:** `README.md` and release notes transparently document the candidate's signing status, explaining how users can verify archive integrity and run the application.
3. **Future Improvement:** Artifact Signing or Microsoft Store signing may be evaluated for a later candidate after identity, package, and certification evidence are confirmed.
