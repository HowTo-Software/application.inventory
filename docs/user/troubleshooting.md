# Troubleshooting

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

Solutions to common problems with the Computer Inventory dashboard and the
thin-client agent. If you don't find your issue here, check the
[FAQ](faq.md) or contact your IT administrator.

## How to Use This Guide

1. Find the symptom that matches your problem.
2. Follow the steps in order.
3. If it's still broken, gather the info in [Reporting a Problem](#reporting-a-problem)
   and contact support.

---

## I can't sign in

**Possible causes & fixes:**

1. **Wrong username or password** — Check Caps Lock and re-enter your
   credentials. First-time admin login is `admin` / `Admin123!`; change it
   immediately after signing in.
2. **Account temporarily locked** — After several failed attempts the system
   blocks sign-in for a few minutes to stop brute-force attacks. Wait ~15
   minutes, then try again.
3. **"Session expired" or you keep getting bounced back to the login page** —
   Your access token has lapsed. Sign in again. If it persists, clear this
   site's data/cookies in your browser (the app stores your login there) and
   reload.
4. Still stuck? Ask an **Admin** to confirm your account is active and has a
   role assigned, or contact your IT administrator to reset your password.

## The dashboard won't load or is blank

1. **Hard refresh** with `Ctrl`+`F5` to clear cached app files — needed after a
   new version is deployed.
2. **Check the server is up** — open `/health/ready` on the API address; it
   should return healthy. If not, the server or database may be down.
3. **Check your network/VPN** — the dashboard needs to reach the API server over
   HTTPS.
4. **Try a different browser or device** to rule out a local browser issue.

## A computer shows "Offline" or hasn't reported recently

The agent sends a periodic heartbeat; "Offline" means none arrived recently.

1. **The PC is powered off or off the network** — a machine that's asleep or
   disconnected can't check in. This is usually normal.
2. **The agent service isn't running** — on that PC open `services.msc`, find
   the **Computer Inventory ThinClient** service, and set it to **Running** /
   **Automatic**.
3. **The agent can't reach the server** — a firewall or wrong server address
   blocks the heartbeat. Verify the agent's configured server URL and that the
   port is open.
4. Still offline after a restart? Reinstall the agent (see below).

## A new loaner PC isn't appearing in inventory

1. **The agent isn't installed** — run the thin-client installer on that PC
   **as Administrator**.
2. **Registration was blocked** — the agent needs network access and a valid
   API key to register. Check the agent log (`agent-*.log`) on the PC for
   errors.
3. **Give it a moment** — wait for the next heartbeat, then refresh the
   dashboard.

## My changes aren't saving

1. **You may not have permission** — **Viewers** are read-only. Editing
   requires a **Technician** or **Admin** role. Ask an Admin to upgrade your
   access.
2. **Someone else edited the same record** — the system rejects conflicting
   simultaneous edits. Reload the page to get the latest values, then re-apply
   your change.
3. **Your session expired** — if you were idle, sign in again and retry.

## The map isn't loading or shows the wrong location

1. **Map tiles need internet** — the map loads from OpenStreetMap. Check your
   connection and any ad/content blockers.
2. **Locations are approximate** — position is estimated from the device's IP
   address, not GPS, so it may be off by city/region.
3. **No location yet** — a newly registered device won't have a location until
   it reports its IP on the next heartbeat.

## An agent update didn't finish (stuck or "Failed")

1. **Transient failure** — the agent retries automatically and rolls back to the
   last working version, so the machine stays functional.
2. **Repeatedly failing** — the agent likely can't reach the server or download
   the package. Confirm the PC is online; an Admin can re-publish or re-target
   the update.
3. Still failing? Report it to IT with the computer's **serial number**.

## I'm getting an error code

| Error | Meaning | What to do |
|-------|---------|-----------|
| 401 Unauthorized | You're not signed in or your session expired | Sign in again |
| 403 Forbidden | Your role doesn't allow this action | Request access from an Admin |
| 404 Not Found | The item was removed or the link is wrong | Go back to the dashboard |
| 429 Too Many Requests | Rate limited (e.g. too many login attempts) | Wait a few minutes and retry |
| 500 Server Error | A problem on the server | Retry; if it persists, contact support |
| 503 Service Unavailable | Server is starting up or the database is unreachable | Wait a minute and retry |

---

## Reporting a Problem

If none of the above works, please contact support with:

- What you were trying to do
- What happened instead (exact error message)
- Steps to reproduce
- Your environment (browser/app version, OS; for agent issues, the PC's serial number)
- Screenshots if possible

**Support:** your IT administrator / Engineering team ·
**Status:** API health at `/health/ready`

> For developers filing detailed bugs, use the
> [Bug Report template](../phase-4-testing/bug-report-template.md).
