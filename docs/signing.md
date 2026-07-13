# Release signing

PodRelay supports Authenticode signing, but the repository intentionally contains no certificate or private key.

## What users can verify today

- Every GitHub Release asset exposes a SHA-256 digest.
- The Windows CI workflow builds and tests the public source, uploads the generated packages, and requests a GitHub build-provenance attestation on `main` pushes.
- Release notes publish the expected SHA-256 values.

These checks prove file integrity and source provenance, but they do not replace a publicly trusted Windows Authenticode identity and do not suppress SmartScreen by themselves.

The current 0.1.0 executables are Authenticode-unsigned because the original author's Windows certificate store does not contain a trusted code-signing certificate with an accessible private key.

## Authenticode certificate setup

Obtain a Windows code-signing certificate from a trusted certificate authority or managed signing service. Import it into the current user's `Personal` (`Cert:\CurrentUser\My`) certificate store without exporting its private key to the repository.

Set the certificate thumbprint only for the publishing process:

```powershell
$env:PODRELAY_SIGNING_THUMBPRINT = 'YOUR_CERTIFICATE_SHA1_THUMBPRINT'
./publish.cmd
```

`scripts/sign.ps1` locates `signtool.exe`, signs the application and diagnostics executables with SHA-256 plus an RFC 3161 timestamp, then verifies every signature before packaging. Publishing fails if signing or verification fails.

For GitHub Actions, import a PFX from an encrypted repository secret into the runner's temporary certificate store, expose only its thumbprint as `PODRELAY_SIGNING_THUMBPRINT`, run the publish script, and remove the certificate after packaging. Never commit a PFX, password, private key, or base64-encoded certificate bundle.

Self-signed certificates are deliberately not generated for public releases: other Windows computers do not trust them, and asking users to install an untrusted root certificate weakens security.
