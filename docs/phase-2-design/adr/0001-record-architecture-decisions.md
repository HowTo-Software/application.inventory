# ADR-0001: Record architecture decisions

> **Status:** Accepted · **Date:** 2026-07-14 · **Deciders:** MKSWORKS

## Context

We need to record the architectural decisions made on this project so that new
and existing team members can understand the reasoning behind them. Without a
lightweight, consistent format, context is lost over time and decisions get
re-litigated.

## Decision

We will use **Architecture Decision Records**, as described by Michael Nygard,
stored as Markdown files in `docs/phase-2-design/adr/`. Each record is numbered
sequentially and never deleted — superseded records are kept and marked as such.

## Consequences

### Positive
- Decisions and their rationale are preserved and discoverable.
- Onboarding is faster; "why" questions have a written answer.
- Changes to decisions are auditable over time.

### Negative / Trade-offs
- Requires discipline to write ADRs when decisions are made.

### Neutral
- Adds a small, well-defined step to the design process.

## Alternatives Considered

| Alternative | Why not chosen |
|-------------|----------------|
| Wiki pages | Harder to review, drifts from code, no PR workflow |
| No formal record | Context is lost; decisions get re-debated |

## References

- Michael Nygard, ["Documenting Architecture Decisions"](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
- [adr.github.io](https://adr.github.io/)
