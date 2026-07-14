# Git Workflow

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

## Branching Model

We use **GitHub Flow** with a protected `main`. `main` is always releasable;
CI runs on every push to `main` and on every pull request targeting it.
Contributors branch off `main` (or a fork), open a PR, get one review, and merge.

### Branch Naming

```
feature/<short-name>     e.g. feature/agent-auto-update
fix/<short-name>         e.g. fix/null-serial-crash
docs/<short-name>        e.g. docs/git-workflow
chore/<short-name>       e.g. chore/bump-efcore
```

## Commit Messages — Conventional Commits

```
<type>(<scope>): <short summary>

<optional body explaining what & why>

<optional footer: BREAKING CHANGE, Closes #123>
```

| Type | Use for |
|------|---------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `refactor` | Code change that isn't a fix or feature |
| `test` | Adding/updating tests |
| `chore` | Tooling, deps, build |
| `perf` | Performance improvement |

Example: `feat(auth): add password reset flow`

## Typical Cycle

```bash
git switch main
git pull --ff-only
git switch -c feature/agent-auto-update

# ...make changes, commit in small steps...
git add -p
git commit -m "feat(agent): add update check endpoint"

git push -u origin feature/agent-auto-update
# Open a Pull Request against main
```

## Pull Requests

- Fill in the [PR template](../../.github/pull_request_template.md); link the issue (`Closes #123`).
- Keep PRs small and focused.
- All CI checks must pass — build, unit tests (Windows), and integration tests
  (SQL Server on Ubuntu). See [CI/CD](../phase-5-deployment/ci-cd.md).
- Requires at least **1** approving review.
- **Squash merge** to keep a linear history; the squash summary follows
  Conventional Commits.

## Keeping Branches Up to Date

```bash
git switch feature/...
git fetch origin
git rebase origin/main    # preferred for a linear history
# resolve conflicts, then:
git push --force-with-lease
```

> Use `--force-with-lease`, never a plain `--force`, to avoid clobbering others' work.

## Releases & Tags

- Tag releases with semantic versions: `v1.4.0` (see [SemVer](https://semver.org)).
- Update the [CHANGELOG](../../CHANGELOG.md) and
  [Release Notes](../phase-5-deployment/release-notes-template.md).
