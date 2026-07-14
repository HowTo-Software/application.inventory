# Architecture Decision Records (ADRs)

> An **Architecture Decision Record** captures a single significant architectural
> decision, its context, and its consequences.

## Why ADRs?

- Preserve the **reasoning** behind decisions, not just the outcome.
- Help future contributors understand "why is it like this?"
- Make trade-offs explicit and reviewable.

## How to Add a New ADR

1. Copy [`template.md`](template.md) to `NNNN-short-title.md`
   (e.g. `0002-use-postgresql.md`). Use the next sequential number.
2. Fill it in. Set status to **Proposed**.
3. Open a PR and discuss. Once agreed, change status to **Accepted**.
4. If a later ADR overturns it, mark this one **Superseded by [ADR-XXXX]**.

## Status Values

| Status | Meaning |
|--------|---------|
| Proposed | Under discussion |
| Accepted | Decided and in effect |
| Deprecated | No longer recommended |
| Superseded | Replaced by a newer ADR |

## Index

| # | Title | Status | Date |
|---|-------|--------|------|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted | 2026-07-14 |
