# User Guide

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

The Device Management (Computer Inventory) dashboard lets your IT team track
every desktop, laptop, and loaner PC in one place. This guide covers the
everyday tasks you'll do in the web app. For fixes to common problems see
[Troubleshooting](troubleshooting.md); for quick answers see the [FAQ](faq.md).

## Introduction

Device Management is a web-based system for tracking your organization's
computers — their hardware, location, assigned user, status, and maintenance
history. Windows machines can run a lightweight **agent** that auto-reports
their details and keeps their online status current.

**Who it's for:** IT administrators, technicians, and staff who need visibility
into company devices. What you can do depends on your role:

| Role | What you can do |
|------|-----------------|
| **Viewer** | Browse and search inventory, view details and reports (read-only) |
| **Technician** | Everything a Viewer can, plus add/edit computers, assign users, and log maintenance |
| **Admin** | Full access, including users, invite codes, departments, locations, packages, and audit logs |

## Getting Started

### Signing In

1. Go to your organization's Device Management URL (ask your administrator).
2. Enter your **username** and **password**.
3. Select **Sign In**.

You'll land on the **Dashboard**. If the app was just installed, you'll instead
see an **Initial Setup** screen — the first person to use it creates the
administrator account there.

### Getting an Account

There's no public sign-up. You get access one of two ways:

- An **Admin creates your account** and gives you a username and temporary
  password, or
- An **Admin sends you an invite code** that you use to register your own
  account. If email verification is enabled, click the link in the verification
  email to activate your account before signing in.

> 💡 **Tip:** Forgot your password? There's no self-service reset — ask an
> administrator to reset it for you.

### The Main Interface

After signing in you'll see a navigation sidebar on the left and your content on
the right.

- **Dashboard** — at-a-glance counts (total, unassigned, assigned, in
  maintenance) plus quick actions.
- **Computers** — the full inventory list with search, filters, and add/edit.
- **Map** — device locations plotted on a map.
- **Maintenance** — scheduled and past maintenance records.
- **Reports** — detailed hardware/system reports.
- **Download Agent** — get the installer for the Windows agent.
- **Version History** — agent versions reported across your fleet.

Admins also see: **Admin Dashboard, Users, Invite Codes, Departments,
Locations, Audit Log,** and **Packages**. Your name in the top corner opens
**My Profile** and **Sign Out**.

## Core Tasks

### Find a computer

1. Open **Computers**.
2. Type in the **Search** box (hostname, service tag, IP, or model), or use the
   **Status, Type, Location, Department,** and **Connection** filters.
3. Select a row to open its full details.

> 💡 **Tip:** The **Connection** filter (Online / Offline / No Agent) quickly
> shows which machines are reporting in.

### Add or edit a computer

1. On **Computers**, select **Add Computer** (or open an existing one and choose
   **Edit**).
2. Fill in the details — service tag, hostname, model, type, status, location,
   and department.
3. Select **Save**.

> Machines with the agent installed register themselves, so you rarely need to
> add those by hand.

### Assign a computer to a user

1. Open the computer's details.
2. Choose **Assign** and pick the user.
3. Save. The Dashboard's **Assigned/Unassigned** counts update automatically.

### Change a computer's status

Open the computer and set its **Status** (for example: In Use, Available, In
Maintenance, Retired). Machines marked **In Maintenance** appear on the
Dashboard's maintenance count.

### Log maintenance

1. Open **Maintenance** (or a computer's details).
2. Select **Record maintenance**, enter what was done and the date.
3. Save to keep a running history for that device.

### Deploy the agent to a computer

1. Open **Download Agent**.
2. Select **Generate Install Link** to create a one-time command (it expires in
   1 hour and works once).
3. On the target Windows PC, open **PowerShell as Administrator**, paste the
   command, and run it.

Within about a minute the computer registers itself and appears in the
inventory as **Online**.

### View devices on the map

Open **Map** to see where devices are. Locations are estimated from each
device's IP address, so they're approximate (city/region), not exact.

### Export or import inventory

On **Computers**, use **Export CSV** to download the current list, or **Import
CSV** to bulk-add machines from a spreadsheet.

## Settings & Preferences

Open **My Profile** (top-right menu) to:

- View your username, name, email, and role.
- Edit your name and email.
- Change your password.

Organization-wide settings — departments, locations, users, invite codes, and
agent packages — are managed by Admins from the matching sidebar items.

## Managing Your Account

This is an internal tool, so there's no billing or subscription.

- **Update your details or password:** **My Profile**.
- **Deactivate or remove an account:** ask an Admin (Admins manage this from the
  **Users** page).
- Your role is assigned by an Admin — request a change if you need more access.

## Privacy & Your Data

The system stores device information (hardware, IP address, approximate
location, status) and basic account details (name, email, role). Device
locations are derived from IP address, not GPS. Data is retained on a rolling
schedule and old records are cleaned up automatically. For how your organization
handles this data and your rights, see [Compliance](../security/compliance.md)
and the [Security Policy](../../SECURITY.md), or contact your administrator.

## Getting Help

- [FAQ](faq.md) — quick answers.
- [Troubleshooting](troubleshooting.md) — fixes for common issues.
- Contact your IT administrator / Engineering team for anything else.

## Glossary

| Term | Meaning |
|------|---------|
| Agent | Small program installed on a Windows PC that auto-reports its details and online status. |
| Heartbeat | Periodic check-in the agent sends so the server knows a device is online. |
| Service tag | Manufacturer's serial number that uniquely identifies a computer. |
| Assigned / Unassigned | Whether a computer is currently allocated to a user. |
| Invite code | One-time code from an Admin that lets a new user register. |
| Online / Offline / No Agent | Whether a device recently sent a heartbeat; "No Agent" means the agent isn't installed. |
| Role | Your permission level: Viewer, Technician, or Admin. |
