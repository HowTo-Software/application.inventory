# Security Policy

This is proprietary software (see [`LICENSE`](../LICENSE)). Please handle security
issues responsibly and privately.

## Reporting a Vulnerability

- **Do not** open a public issue for security problems.
- Use GitHub's [private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
  (repository **Security** tab → **Report a vulnerability**), or contact the maintainers directly.
- Include steps to reproduce, the affected component, and the potential impact.

Please allow a reasonable time for a fix before any disclosure.

## Secrets & Credentials

- Never commit `.env`, connection strings, JWT signing keys, API keys, or `.pfx` / `.p12`
  private keys. These are ignored via [`.gitignore`](../.gitignore).
- Runtime secrets are supplied through environment variables — see [`.env.example`](../.env.example).
- If a credential is ever exposed in a commit, **rotate it immediately** and scrub it from
  history before it is shared further.
