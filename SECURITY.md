# Security Policy

## Supported Versions

Use this table to communicate which versions of the project are currently
receiving security updates.

| Version | Supported          |
| ------- | ------------------ |
| 0.1.x   | :white_check_mark: |
| < 0.1   | :x:                |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public issues.**

Instead, report them privately using GitHub's [private vulnerability
reporting](https://github.com/MKSWORKS/inventory/security/advisories/new) or
by email to **security@mksworks.com**.

Please include:

- A description of the vulnerability and its impact
- Steps to reproduce or a proof of concept
- Affected version(s) and configuration
- Any suggested mitigation

## What to Expect

- **Acknowledgement:** within 3 business days.
- **Assessment & triage:** we will confirm the issue and determine severity.
- **Fix & disclosure:** we will work on a fix and coordinate a disclosure
  timeline with you. We aim to resolve critical issues within 30 days.

## Scope

**In scope:** the API server (ASP.NET Core), Blazor WebAssembly web app, and
Thin Client agent in this repository, including authentication/authorization,
agent registration, heartbeat/check-in, and data handling.

**Out of scope:** third-party dependencies (please report upstream),
deployment or configuration issues specific to a self-hosted install, and
denial-of-service via excessive load.

Thank you for helping keep the Computer Inventory Management System and its
users safe.
