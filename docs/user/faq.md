# Frequently Asked Questions (FAQ)

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

## General

### What is Device Management (Computer Inventory)?

A self-hosted web app for tracking your organization's computers — hardware,
location, assigned user, status, and maintenance history — in one place. Windows
machines can run a lightweight **agent** that auto-reports their details and
keeps their online status current.

### Who is it for?

Internal IT teams. **Admins** manage everything, **Technicians** add/edit
devices and log maintenance, and **Viewers** browse read-only. See the
[User Guide](user-guide.md#introduction) for what each role can do.

### How much does it cost?

Nothing to you — it's an internal tool your organization hosts on its own
server. There's no per-user charge and no subscription.

## Account

### How do I create an account?

There's no public sign-up. An administrator either creates your account and
gives you a username and temporary password, or sends you an **invite code** to
register yourself. See [Getting an Account](user-guide.md#getting-an-account).

### I forgot my password. What do I do?

There's no self-service reset — ask an administrator to reset it. Repeated failed
sign-ins temporarily lock the account (~15 minutes); wait, then try again. See
[I can't sign in](troubleshooting.md#i-cant-sign-in).

### How do I delete my account?

Contact an administrator. Accounts are deactivated rather than erased so audit
history stays intact; see data handling in [Compliance](../security/compliance.md).

## Features

### How do I add or find a computer?

On the **Computers** page, search by hostname, service tag, IP, or model, and
select **Add Computer** to create one. See [Core Tasks](user-guide.md#core-tasks).

### How do I install the agent on a PC?

On the **Download Agent** page, generate a one-time install link (expires in
1 hour) and run it in an **elevated PowerShell** window on the target Windows PC.
See [Deploy the agent](user-guide.md#deploy-the-agent-to-a-computer).

### Does it support macOS or Linux?

The auto-reporting agent is **Windows-only** (it runs as a Windows service). You
can still track macOS, Linux, and other devices by adding and editing them
manually.

### Can I import or export inventory?

Yes — the Computers page supports CSV **export** and **import**. See
[Export or import inventory](user-guide.md#export-or-import-inventory).

## Data & Privacy

### Where is my data stored?

In a SQL Server database hosted by your own organization — nothing is sent to a
third-party cloud. The optional map view and IP-based location use external
services (OpenStreetMap tiles and an IP-geolocation lookup); ask your admin if
those are enabled.

### Is my data secure?

Access requires a sign-in and is restricted by role. Passwords are hashed,
sessions use signed tokens over HTTPS, and administrative actions are
audit-logged. See the [Security Policy](../../SECURITY.md).

## The Agent

### What does the agent do, and is it safe?

It's a lightweight Windows service that periodically reports the PC's hardware
and system details and sends a heartbeat so the dashboard shows it as online. It
does not capture personal files or keystrokes.

### Will it slow down my computer?

No — it runs quietly in the background and reports on a schedule, using minimal
resources.

### A device shows "Offline" — what does that mean?

The agent hasn't sent a heartbeat recently: the PC is off, asleep, or
disconnected, or the service stopped. See
[A computer shows "Offline"](troubleshooting.md#a-computer-shows-offline-or-hasnt-reported-recently).

## Still need help?

See [Troubleshooting](troubleshooting.md) or contact your internal IT/Engineering
support team.
