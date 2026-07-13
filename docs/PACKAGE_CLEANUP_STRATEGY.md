# Update Package Cleanup Strategy

> Automated cleanup of old update packages, backups, and temp files across server and client.

---

## Overview

The system manages update packages at three levels. Each level has its own cleanup
mechanism to prevent disk bloat.

| Layer | What accumulates | Cleanup mechanism |
|-------|-----------------|-------------------|
| **API Server** | Stale files in `agent/update/` | `DatabaseMaintenanceService` periodic sweep |
| **Updater (client)** | `{installDir}_backup_*` directories | `CleanupOldBackups()` after successful update |
| **Updater (client)** | `%TEMP%\ComputerInventoryUpdate` | `CleanupUpdateTempDirectory()` after successful update |

---

## 1. Server-Side Cleanup

**Service**: `DatabaseMaintenanceService.CleanupOldUpdatePackages()`
**Runs**: Every cleanup cycle (default 24 hours, configured via `DatabaseMaintenance:CleanupIntervalHours`)

The server only ever holds one active update package (`agent-update.zip` + `update.json`).
The cleanup removes anything else that may have been left behind:

- Unexpected files (old archives, temp files, partial uploads)
- Subdirectories (leftover extraction artifacts)

Only `agent-update.zip` and `update.json` are preserved.

### Server directory layout

```
{API_ROOT}/agent/update/
├── agent-update.zip     ← active package (~60 MB)
└── update.json          ← version metadata
```

---

## 2. Client-Side Backup Cleanup

**Function**: `CleanupOldBackups()` in `AgentUpdater.exe`
**Runs**: Immediately after a successful update, before the process exits

On every update the updater creates a full backup of the installation directory:

```
C:\Program Files\ComputerInventoryAgent_backup_20260310143022\
```

Without cleanup, each backup is ~60 MB and accumulates indefinitely. The cleanup:

1. Scans the parent directory for directories matching `{installDirName}_backup_*`
2. Sorts by timestamp suffix (descending)
3. Deletes all but the N most recent backups

### Configuration

In the thin client's `appsettings.json`:

```json
{
  "AgentSettings": {
    "MaxBackupsToKeep": 1
  }
}
```

The value is passed to the updater via `updater-config.json`. Default is **1** (keep only
the latest backup for rollback).

---

## 3. Client-Side Temp Cleanup

**Function**: `CleanupUpdateTempDirectory()` in `AgentUpdater.exe`
**Runs**: Immediately after a successful update

The update temp directory (`%TEMP%\ComputerInventoryUpdate`) holds:

| File/Dir | Purpose | Cleaned up? |
|----------|---------|-------------|
| `update.zip` | Downloaded package (kept for resume) | Yes, after success |
| `extracted/` | Extracted binaries | Yes, after success |
| `updater-config.json` | Server URL + API key for progress reporting | Yes, after success |
| `AgentUpdater.exe` | Updater binary copy | Left in place (self) |

After a successful update, `update.zip`, `extracted/`, and `updater-config.json` are
deleted. This also removes the API key from the temp directory for security.

**On failure**: Temp files are preserved to allow download resume on the next attempt.

---

## 4. Existing Pre-Download Cleanup (UpdateService)

Before downloading a new update, the thin client's `UpdateService.DownloadAndApplyUpdateAsync()`
cleans the temp directory:

- Deletes all subdirectories (`extracted/`, etc.)
- Deletes all files **except** `update.zip` (preserved for HTTP range-based resume)

This ensures each update attempt starts from a clean state while still benefiting from
partial downloads.

---

## Disk Space Summary

| Item | Size | Retention |
|------|------|-----------|
| Server update package | ~60 MB | 1 (current only) |
| Client backup | ~60 MB each | Configurable, default 1 |
| Client temp download | ~60 MB | Deleted after success |

Worst-case per-client disk usage: **~120 MB** (one backup + active installation).
