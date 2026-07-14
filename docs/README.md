# Documentation

Welcome to the {{PROJECT_NAME}} documentation. Docs are organized to mirror the
**software development lifecycle (SDLC)** — each phase has its own folder so you
can find information based on where you are in the project.

## How This Is Organized

```
docs/
├── phase-1-inception/     → Why we're building it & what it must do
├── phase-2-design/        → How it's architected & designed
├── phase-3-development/   → How to build & contribute code
├── phase-4-testing/       → How quality is verified
├── phase-5-deployment/    → How it ships to users
├── phase-6-operations/    → How it runs in production
├── security/              → Cross-cutting security & compliance
└── user/                  → End-user facing guides
```

## Phase Map

| Phase | Name | Key Questions Answered | Documents |
|:-----:|------|------------------------|-----------|
| **1** | [Inception & Requirements](phase-1-inception/) | Why? For whom? What exactly? | Vision & Scope, PRD, SRS, User Stories, Roadmap |
| **2** | [Design & Architecture](phase-2-design/) | How is it structured? | Architecture, Technical Design, Data Model, API Spec, Diagrams, ADRs |
| **3** | [Development](phase-3-development/) | How do I build it? | Onboarding, Coding Standards, Git Workflow |
| **4** | [Testing & QA](phase-4-testing/) | Does it work correctly? | Test Plan, Test Cases, Bug Reports |
| **5** | [Deployment & Release](phase-5-deployment/) | How does it ship? | Deployment Guide, CI/CD, Release Notes |
| **6** | [Operations & Maintenance](phase-6-operations/) | How do we keep it running? | Runbook, Monitoring, Disaster Recovery |

## Cross-Cutting Documentation

These apply across all phases:

- **[Security](security/)** — Threat model and compliance requirements.
- **[User Guides](user/)** — Documentation for end users: user guide, FAQ, troubleshooting.

## Conventions

- Every document starts with a **status**, **owner**, and **last-updated** header.
- Replace all `{{PLACEHOLDER}}` tokens with real content.
- Prefer linking between documents over duplicating information.
- Keep diagrams as text (Mermaid) where possible so they're version-controllable.

## Document Status Legend

| Status | Meaning |
|--------|---------|
| 🟢 Approved | Reviewed and current |
| 🟡 Draft | In progress, not yet reviewed |
| 🔴 Outdated | Needs revision |
| ⚪ Placeholder | Template not yet filled in |
