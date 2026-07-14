# Contributing to Computer Inventory Management System

Thank you for your interest in contributing! This document explains how to get
set up and the workflow we follow.

## Code of Conduct

This project and everyone participating in it is governed by our
[Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to
uphold it.

## Getting Started

1. Fork the repository and clone your fork.
2. Follow the [Quick Start](README.md#quick-start) to set up your environment
   (.NET 8 SDK, Docker Desktop, and the development database).
3. Create a branch for your work (see
   [Branch & Commit Conventions](#branch--commit-conventions)).

## How to Contribute

### Reporting Bugs

- Search existing issues first to avoid duplicates.
- Open a [GitHub issue](https://github.com/MKSWORKS/inventory/issues) with a
  clear, descriptive title.
- Include steps to reproduce, expected vs. actual behavior, and environment
  details (OS, .NET version, browser).

### Suggesting Enhancements

- Open an issue describing the problem and your proposed solution.
- Explain the use case and who benefits.

### Pull Requests

1. Follow the existing code style and conventions in the codebase.
2. Add or update tests for your change.
3. Update relevant documentation.
4. Ensure the build and all tests pass locally (`dotnet build` and
   `dotnet test`).
5. Open a PR with a clear title and description; link related issues.

## Branch & Commit Conventions

- **Branches:** `feature/<short-name>`, `fix/<short-name>`, `docs/<short-name>`
- **Commits:** Follow [Conventional Commits](https://www.conventionalcommits.org/):
  `type(scope): summary` — e.g. `feat(auth): add password reset flow`.

## Review Process

- At least one approving review is required before merge.
- Address review comments or explain why a suggestion doesn't apply.
- Keep PRs focused and reasonably small for faster review.

## Questions?

Open a [GitHub issue or discussion](https://github.com/MKSWORKS/inventory/issues).
