# Stored Procedure Reference

> Last updated: March 27, 2026
>
> Source: `Docs/ComputerInventory_DDL.sql` — Migrations 029–046

---

## Table of Contents

1. [Database Maintenance](#1-database-maintenance)
2. [Reports](#2-reports)
3. [Search & Geo-Location](#3-search--geo-location)
4. [Restore Points](#4-restore-points)
5. [Computer Repository](#5-computer-repository)
6. [Lookups](#6-lookups)
7. [Reference Data (Find-or-Create)](#7-reference-data-find-or-create)
8. [Computer History](#8-computer-history)
9. [Maintenance Records](#9-maintenance-records)
10. [Users](#10-users)
11. [Version History & Health Check](#11-version-history--health-check)
12. [Geo-Location (Extended)](#12-geo-location-extended)
13. [Computer Ownership](#13-computer-ownership)
14. [Packages](#14-packages)
15. [System Details](#15-system-details)

---

## 1. Database Maintenance

Bulk cleanup and stale-data operations run by `DatabaseMaintenanceService`.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_mark_offline_computers` | `@offline_threshold_minutes INT` | Sets `is_online = 0` on agent statuses where `last_seen` is older than the threshold. Returns `affected_rows`. |
| `sp_cleanup_old_system_details` | `@retention_days INT` | Soft-deletes `system_details` rows older than the retention period. Returns `affected_rows`. |
| `sp_cleanup_old_restore_points` | `@retention_days INT` | Soft-deletes `restore_points` rows older than the retention period. Returns `affected_rows`. |
| `sp_cleanup_old_history_records` | `@retention_days INT` | Soft-deletes `computer_histories` rows older than the retention period. Returns `affected_rows`. |
| `sp_mark_stuck_updates_as_failed` | `@timeout_hours INT` | Marks active update requests as failed (status 99) when `update_started_at` exceeds the timeout. Returns `affected_rows`. |
| `sp_cleanup_excess_system_details` | `@max_records_per_computer INT` | Soft-deletes per-computer `system_details` rows beyond the N most recent (by `collected_at`). Returns `affected_rows`. |
| `sp_cleanup_excess_restore_points` | `@max_records_per_computer INT` | Soft-deletes per-computer `restore_points` rows beyond the N most recent (by `created_at`). Returns `affected_rows`. |
| `sp_cleanup_excess_records` | `@table_name NVARCHAR(128)`, `@max_records_per_computer INT` | Generic excess-record cleanup using dynamic SQL. Supported tables: `system_details`, `restore_points`. Returns `affected_rows`. |
| `sp_soft_delete_old_history` | `@cutoff DATETIME2` | Soft-deletes all `computer_histories` with `changed_at` before the cutoff. Returns `affected_rows`. |

---

## 2. Reports

Used by `SystemDetailsController` / `SystemDetailsRepository`.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_system_details_report` | `@page INT = 1`, `@page_size INT = 25` | Returns two result sets: (1) total count of distinct computers with system details, (2) paginated latest system detail per computer joined across all 16 sub-tables (BIOS, CPU, memory, storage, GPU, network, OS, security, battery, chassis, software, users/groups, peripherals, event logs, performance, motherboard). |

---

## 3. Search & Geo-Location

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_search_computers` | `@search_term NVARCHAR(200)` | Full-text-style LIKE search across hostname, serial number, IP, manufacturer, model, location, department, assigned-to, and current user. Returns matching computer IDs ordered by hostname. |
| `sp_get_computer_ids_needing_geo_lookup` | `@cutoff_hours INT` | Returns IDs of online, agent-equipped computers whose IP geo-location has never been looked up or was last updated before the cutoff. |
| `sp_get_all_computer_locations` | *(none)* | Returns all computers that have a public IP or geo-location record, with GPS and IP coordinates, city, region, country, and location-source preference. |
| `sp_upsert_geo_location` | `@computer_id INT`, `@public_ip_address NVARCHAR(45) = NULL`, `@gps_latitude FLOAT = NULL`, `@gps_longitude FLOAT = NULL`, `@gps_last_updated DATETIME2 = NULL`, `@gps_accuracy FLOAT = NULL`, `@ip_latitude FLOAT = NULL`, `@ip_longitude FLOAT = NULL`, `@ip_last_updated DATETIME2 = NULL`, `@latitude FLOAT = NULL`, `@longitude FLOAT = NULL`, `@city NVARCHAR(100) = NULL`, `@region NVARCHAR(100) = NULL`, `@country_id INT = NULL`, `@isp NVARCHAR(200) = NULL`, `@timezone NVARCHAR(50) = NULL`, `@lookup_successful BIT = 0`, `@lookup_error NVARCHAR(500) = NULL`, `@location_source NVARCHAR(MAX) = 'Auto'` | MERGE-based upsert into `computer_geo_locations`. On match, uses COALESCE to preserve existing values for NULL parameters. |
| `sp_get_or_create_country_id` | `@country_code NVARCHAR(10)`, `@country_name NVARCHAR(100)` | MERGE-based find-or-create on `countries` by country code. Returns `id`. |

---

## 4. Restore Points

Used by `RestorePointController`.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_restore_point_summary` | `@page INT = 1`, `@page_size INT = 25` | Two result sets: (1) count of distinct computers with restore points, (2) paginated per-computer summary with counts, date range, total size, and reboot count. |
| `sp_cleanup_old_restore_points_by_computer` | `@computer_id INT`, `@keep_count INT` | Hard-deletes all but the N most recent restore points for a given computer. Returns `deleted_rows`. |
| `sp_get_restore_point_by_guid` | `@restore_point_id UNIQUEIDENTIFIER` | Returns a single restore point by its GUID (non-deleted only). |
| `sp_get_restore_point_by_guid_with_details` | `@restore_point_id UNIQUEIDENTIFIER` | Three result sets: (1) restore point with computer/type info, (2) system info, (3) snapshot (compressed JSON columns). |
| `sp_get_restore_points_by_computer_id` | `@computer_id INT` | Two result sets: (1) all restore points for the computer ordered by `created_at` DESC, (2) all associated system info records. |
| `sp_get_latest_complete_restore_point` | `@computer_id INT` | Three result sets for the most recent complete (`is_complete = 1`) restore point: (1) restore point, (2) system info, (3) snapshot. Returns nothing if none found. |

---

## 5. Computer Repository

Core CRUD and query operations on `computers`.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_computer_by_serial_number` | `@serial_number NVARCHAR(100)` | Returns the computer with agent status and active update request, matched by serial number. |
| `sp_get_computer_by_serial_number_basic` | `@serial_number NVARCHAR(100)`, `@include_deleted BIT = 0` | Returns `c.*` for a computer by serial number. Optionally includes soft-deleted records. |
| `sp_get_total_computer_count` | *(none)* | Returns `total_count` of non-deleted computers. |
| `sp_get_status_counts` | *(none)* | Returns computer count grouped by `status_id`. |
| `sp_delete_computer_direct` | `@computer_id INT` | Soft-deletes a computer. Returns `affected_rows`. |
| `sp_get_all_computers` | `@search_ids NVARCHAR(MAX) = NULL`, `@status NVARCHAR(100) = NULL`, `@type NVARCHAR(100) = NULL`, `@location NVARCHAR(100) = NULL`, `@department NVARCHAR(100) = NULL`, `@connection NVARCHAR(20) = NULL`, `@cutoff_minutes INT = 10`, `@sort_by NVARCHAR(50) = NULL`, `@sort_direction NVARCHAR(4) = 'ASC'`, `@page INT = 1`, `@page_size INT = 50` | Server-side filtered, sorted, paginated computer list. Two result sets: (1) total count, (2) computers with agent status, update request, and nav-property names. Supports `@connection` filter values: `Online`, `Offline`, `NoAgent`. Sort keys: `hostname`, `serialnumber`, `ipaddress`, `status`, `type`, `location`, `department`, `manufacturer`, `model`, `assignedto`, `agentversion`, `updatedat`, `createdat`, `lastseen`, `isonline`. |
| `sp_get_computer_by_id` | `@id INT` | Three result sets: (1) computer with all nav properties (status, type, location, department, model, manufacturer, OS, agent status, update request), (2) maintenance records, (3) recent history (top 50). |
| `sp_get_computers_by_status` | `@status_id INT`, `@sort_by NVARCHAR(50) = NULL`, `@sort_direction NVARCHAR(4) = 'ASC'`, `@page INT = NULL`, `@page_size INT = NULL` | Two result sets: (1) total count, (2) paginated/sorted computers filtered by status. Same sort keys as `sp_get_all_computers`. |
| `sp_get_computers_by_location` | `@location_id INT`, `@sort_by NVARCHAR(50) = NULL`, `@sort_direction NVARCHAR(4) = 'ASC'`, `@page INT = NULL`, `@page_size INT = NULL` | Two result sets: (1) total count, (2) paginated/sorted computers filtered by location. |
| `sp_get_computers_by_department` | `@department_id INT`, `@sort_by NVARCHAR(50) = NULL`, `@sort_direction NVARCHAR(4) = 'ASC'`, `@page INT = NULL`, `@page_size INT = NULL` | Two result sets: (1) total count, (2) paginated/sorted computers filtered by department. |
| `sp_computer_exists` | `@id INT` | Returns BIT indicating whether a non-deleted computer exists with the given ID. |
| `sp_bulk_update_computer_status` | `@id_list NVARCHAR(MAX)`, `@status_id INT`, `@updated_by NVARCHAR(100) = NULL` | Updates `status_id` for a comma-separated list of computer IDs. Returns `affected_rows`. |
| `sp_get_dashboard_counts` | `@latest_agent_version NVARCHAR(50) = NULL` | Returns a single row with aggregate dashboard metrics: total, online, offline, agents installed, updating, failed, pending, on-latest-version, on-older-version, stale agents, expired warranties. |

---

## 6. Lookups

Simple read-all operations for dropdown/reference tables.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_computer_types` | *(none)* | Returns all rows from `computer_types` ordered by name. |
| `sp_get_computer_statuses` | *(none)* | Returns all rows from `computer_statuses` ordered by name. |
| `sp_get_departments` | *(none)* | Returns all rows from `departments` ordered by name. |
| `sp_get_locations` | *(none)* | Returns all rows from `locations` ordered by name. |
| `sp_get_manufacturers` | *(none)* | Returns all rows from `manufacturers` ordered by name. |
| `sp_get_computer_models` | `@manufacturer_id INT = NULL` | Returns computer models with manufacturer name. Optionally filtered by manufacturer. |
| `sp_get_operating_systems` | *(none)* | Returns all rows from `operating_systems` ordered by name. |
| `sp_get_location_types` | *(none)* | Returns all rows from `locations` ordered by name (alias for locations lookup). |
| `sp_get_department_types` | *(none)* | Returns all rows from `departments` ordered by name (alias for departments lookup). |
| `sp_get_manufacturer_types` | *(none)* | Returns all rows from `manufacturers` ordered by name (alias for manufacturers lookup). |
| `sp_get_maintenance_types` | *(none)* | Returns all rows from `maintenance_types` ordered by name. |
| `sp_get_maintenance_statuses` | *(none)* | Returns all rows from `maintenance_statuses` ordered by name. |
| `sp_get_user_roles` | *(none)* | Returns all rows from `user_roles` ordered by name. |
| `sp_get_version_types` | *(none)* | Returns all rows from `version_types` ordered by name. |
| `sp_department_name_exists` | `@name NVARCHAR(100)`, `@exclude_id INT = NULL` | Returns BIT indicating whether a department with the given name exists, optionally excluding an ID (for edit uniqueness checks). |
| `sp_get_department_usage_count` | `@id INT` | Returns combined count of computers + users assigned to the department. |
| `sp_location_name_exists` | `@name NVARCHAR(200)`, `@exclude_id INT = NULL` | Returns BIT indicating whether a location with the given name exists, optionally excluding an ID. |
| `sp_get_location_usage_count` | `@id INT` | Returns count of non-deleted computers assigned to the location. |

---

## 7. Reference Data (Find-or-Create)

MERGE-based idempotent lookups used by `ReferenceDataResolver` during agent heartbeat processing.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_find_or_create_manufacturer` | `@name NVARCHAR(100)` | Returns existing or newly-created `id` from `manufacturers`. Uses `MERGE` with `HOLDLOCK`. |
| `sp_find_or_create_computer_model` | `@manufacturer_id INT`, `@model_name NVARCHAR(100)` | Returns existing or newly-created `id` from `computer_models`. Matched on manufacturer + model name. |
| `sp_find_or_create_operating_system` | `@name NVARCHAR(100)`, `@version NVARCHAR(100) = NULL`, `@build_number NVARCHAR(100) = NULL` | Returns existing or newly-created `id` from `operating_systems`. Matched on name only. |
| `sp_find_or_create_department` | `@name NVARCHAR(100)` | Returns existing or newly-created `id` from `departments`. |
| `sp_find_or_create_location` | `@name NVARCHAR(200)` | Returns existing or newly-created `id` from `locations`. |

---

## 8. Computer History

Audit trail for computer changes.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_add_computer_history` | `@computer_id INT`, `@action NVARCHAR(100)`, `@changed_by NVARCHAR(100) = NULL`, `@old_value NVARCHAR(500) = NULL`, `@new_value NVARCHAR(500) = NULL`, `@notes NVARCHAR(500) = NULL` | Inserts a history record. Returns `SCOPE_IDENTITY()` as `id`. |
| `sp_set_location_source_preference` | `@computer_id INT`, `@preference NVARCHAR(MAX)` | Updates `preferred_location_source` on the computer's agent status. Returns `affected_rows`. |
| `sp_get_history_by_computer_id` | `@computer_id INT` | Returns all non-deleted history for a computer, ordered by `changed_at` DESC. |
| `sp_get_history_paged` | `@page INT = 1`, `@page_size INT = 25`, `@action NVARCHAR(100) = NULL`, `@search NVARCHAR(200) = NULL`, `@from_date DATETIME2 = NULL`, `@to_date DATETIME2 = NULL`, `@sort_by NVARCHAR(50) = NULL`, `@sort_direction NVARCHAR(4) = 'DESC'` | Two result sets: (1) total count, (2) paginated, filterable, sortable history with computer hostname/serial. Sort keys: `action`, `changedby`, `computer`, `changedat`. |
| `sp_get_distinct_history_actions` | *(none)* | Returns distinct `action` values from non-deleted history records, ordered alphabetically. |

---

## 9. Maintenance Records

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_all_maintenance_records` | `@sort_by NVARCHAR(50) = NULL`, `@sort_direction NVARCHAR(4) = 'DESC'`, `@page INT = 1`, `@page_size INT = 25` | Two result sets: (1) total count, (2) paginated/sorted maintenance records with computer, type, and status names. Sort keys: `type`, `status`, `computer`, `performedby`, `scheduleddate`, `completeddate`, `createdat`, `cost`. |
| `sp_get_maintenance_by_computer_id` | `@computer_id INT`, `@page INT = 1`, `@page_size INT = 25` | Two result sets: (1) total count, (2) paginated maintenance records for a specific computer. |
| `sp_get_maintenance_by_status` | `@status_id INT`, `@page INT = 1`, `@page_size INT = 25` | Two result sets: (1) total count, (2) paginated maintenance records filtered by status. |
| `sp_get_scheduled_maintenance` | `@start_date DATETIME2`, `@end_date DATETIME2`, `@page INT = 1`, `@page_size INT = 25` | Two result sets: (1) total count, (2) paginated maintenance records within a date range, ordered by `scheduled_date` ASC. |
| `sp_get_maintenance_by_id` | `@id INT` | Returns a single maintenance record with computer hostname/serial, type name, and status name. |
| `sp_get_maintenance_summary_counts` | *(none)* | Returns aggregate counts: `open_count` (scheduled + in-progress), `scheduled_count`, `completed_this_month`. |

---

## 10. Users

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_all_users` | `@search_term NVARCHAR(100) = NULL`, `@sort_by NVARCHAR(50) = NULL`, `@sort_direction NVARCHAR(4) = 'ASC'`, `@page INT = 1`, `@page_size INT = 25` | Two result sets: (1) total count, (2) paginated/sorted/searchable user list with department and role names. Search matches username, email, first/last name, department. Sort keys: `username`, `email`, `firstname`, `lastname`, `fullname`, `role`, `department`, `createdat`, `lastloginat`, `isactive`. |
| `sp_get_user_by_username` | `@username NVARCHAR(50)` | Returns a single user (including `password_hash`) with department and role names. |
| `sp_get_user_by_email` | `@email NVARCHAR(256)` | Returns a single user (including `password_hash`) by email. |
| `sp_get_user_by_id` | `@id INT` | Returns a single user (including `password_hash`) by ID. |
| `sp_get_users_by_role` | `@role_id INT` | Returns all users with the given role, ordered by last/first name. |
| `sp_check_user_exists` | `@username NVARCHAR(50) = NULL`, `@email NVARCHAR(256) = NULL`, `@username_exists BIT OUTPUT`, `@email_exists BIT OUTPUT` | Checks whether username and/or email already exist. Both inputs are nullable — only non-NULL values are checked. Results returned via OUTPUT parameters. |

---

## 11. Version History & Health Check

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_version_history` | `@page INT = 1`, `@page_size INT = 25` | Four result sets: (1) server version count, (2) paginated server versions (`type_id = 0`), (3) thin-client version count, (4) paginated thin-client versions (`type_id = 1`). |
| `sp_get_server_history` | `@page INT = 1`, `@page_size INT = 25` | Two result sets: (1) total count, (2) paginated server versions (`type_id = 0`). |
| `sp_get_thin_client_history` | `@page INT = 1`, `@page_size INT = 25` | Two result sets: (1) total count, (2) paginated thin-client versions (`type_id = 1`). |
| `sp_get_version_history_by_type_unpaged` | `@type_id INT` | Returns all version history rows for a given type, ordered by `released_at` DESC. No pagination. |
| `sp_version_exists` | `@type_id INT`, `@version NVARCHAR(50)` | Returns BIT indicating whether a version record exists for the given type and version string. |
| `sp_find_version_by_type_and_version` | `@type_id INT`, `@version NVARCHAR(50)` | Returns the full version history row for a given type + version combination. |
| `sp_health_check` | *(none)* | Returns two result sets: (1) `computer_count` of non-deleted computers, (2) `query_time_ms` elapsed. Used by health-check endpoint to verify database connectivity. |

---

## 12. Geo-Location (Extended)

Additional geo-location procedures beyond the core upsert (§3).

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_geo_location_by_computer_id` | `@computer_id INT` | Returns the geo-location record with country details for a single computer. |
| `sp_get_geo_location_simple` | `@computer_id INT` | Returns `SELECT *` from `computer_geo_locations` for a computer (no joins). |
| `sp_get_all_geo_locations_with_computers` | *(none)* | Returns all geo-location records joined with computer hostname/serial/IP and country info. Excludes deleted computers. |
| `sp_get_geo_locations_with_valid_coordinates` | *(none)* | Same as above, filtered to records where `latitude` and `longitude` are not NULL. |
| `sp_update_gps_location` | `@computer_id INT`, `@latitude FLOAT`, `@longitude FLOAT`, `@public_ip_address NVARCHAR(45) = NULL` | MERGE-based upsert that sets GPS coordinates and marks `location_source = 'GPS'`. Updates display lat/lon to GPS values. Returns `affected_rows`. |
| `sp_update_ip_location` | `@computer_id INT`, `@latitude FLOAT`, `@longitude FLOAT`, `@city NVARCHAR(100) = NULL`, `@region NVARCHAR(100) = NULL`, `@country NVARCHAR(100) = NULL`, `@country_code NVARCHAR(10) = NULL`, `@isp NVARCHAR(200) = NULL`, `@timezone NVARCHAR(50) = NULL`, `@public_ip_address NVARCHAR(45) = NULL` | MERGE-based upsert that sets IP-based coordinates. Resolves country to FK automatically. Only updates display lat/lon if no GPS data exists. Returns `affected_rows`. |
| `sp_delete_geo_location` | `@computer_id INT` | Hard-deletes the geo-location record for a computer. Returns `affected_rows`. |

---

## 13. Computer Ownership

Access-control records linking users to computers.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_ownership` | `@computer_id INT`, `@user_id INT` | Returns the ownership record for a specific computer/user pair. |
| `sp_get_ownerships_by_computer_id` | `@computer_id INT` | Returns all ownership records for a computer with user details (username, name, email). |
| `sp_get_ownerships_by_user_id` | `@user_id INT` | Returns all ownership records for a user with computer hostname/serial. Excludes deleted computers. |
| `sp_has_ownership_access` | `@computer_id INT`, `@user_id INT`, `@minimum_level INT = 0` | Returns BIT indicating whether the user has ownership access at or above the minimum level. |
| `sp_remove_ownership` | `@computer_id INT`, `@user_id INT` | Hard-deletes the ownership record for a specific computer/user pair. Returns `affected_rows`. |
| `sp_remove_all_ownerships_for_computer` | `@computer_id INT` | Hard-deletes all ownership records for a computer. Returns `affected_rows`. |

---

## 14. Packages

Package version management and distribution.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_all_packages` | *(none)* | Two result sets: (1) all non-deleted package versions ordered by `created_at` DESC, (2) all distribution rules for those packages. |
| `sp_get_package_by_id_with_rules` | `@id INT` | Two result sets: (1) the package version, (2) its distribution rules. |
| `sp_get_package_by_version` | `@version NVARCHAR(50)`, `@package_type NVARCHAR(50) = 'Agent'` | Two result sets: (1) the package matched by version + type, (2) its distribution rules. |
| `sp_get_active_package_version` | `@package_type NVARCHAR(50) = 'Agent'` | Two result sets: (1) the active package (status = 2) for the given type, (2) its distribution rules. |
| `sp_get_packages_by_status` | `@status INT`, `@package_type NVARCHAR(50) = 'Agent'` | Returns packages filtered by status and type, ordered by `created_at` DESC. |
| `sp_get_packages_by_channel` | `@channel NVARCHAR(20)`, `@package_type NVARCHAR(50) = 'Agent'` | Returns packages filtered by release channel and type, ordered by `created_at` DESC. |
| `sp_package_version_exists` | `@version NVARCHAR(50)`, `@package_type NVARCHAR(50) = 'Agent'` | Returns BIT indicating whether a package with the given version and type exists. |
| `sp_increment_download_count` | `@package_id INT` | Increments `download_count` by 1. Returns `affected_rows`. |
| `sp_get_distribution_rules` | `@package_id INT` | Returns all distribution rules for a package version. |

---

## 15. System Details

Hardware/software inventory snapshots collected by the agent.

| Procedure | Parameters | Purpose |
|-----------|-----------|---------|
| `sp_get_latest_system_details_by_computer_id` | `@computer_id INT` | 17 result sets: (1) system detail header + computer info, (2–17) one result set per sub-table (BIOS, motherboard, CPU, memory, storage, GPU, network, OS, security, battery, chassis, software, users/groups, peripherals, event logs, performance). Returns nothing if no records exist. |
| `sp_get_system_details_history` | `@computer_id INT`, `@limit INT = 10` | Four result sets for the N most recent system details: (1) header rows, (2) OS info, (3) performance data, (4) software data. |
| `sp_get_latest_system_details_all` | `@page INT = NULL`, `@page_size INT = NULL` | 18 result sets: (1) total count, (2) system detail headers + computer info, (3–18) all sub-tables for the latest detail per computer. Pagination is optional. |
