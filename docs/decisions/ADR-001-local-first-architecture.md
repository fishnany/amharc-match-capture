# ADR-001: Local-First Architecture

**Date:** July 2026  
**Status:** Accepted

---

## Context

AMHARC Match Capture operates at sports grounds where Internet connectivity is unreliable or absent. The system must be capable of recording full matches, tagging events, and maintaining the match clock and scoreboard without any network access beyond the local Ethernet connection to the camera.

## Decision

The system will be designed as local-first. All core match operations (recording, event tagging, PTZ control, match clock, scoreboard, broadcast overlays, and local persistence) will be implemented in the Windows Capture Agent and must function without Internet connectivity.

Cloud services (RTMP streaming, metadata sync, remote administration) are strictly optional and will be implemented as add-ons that the system remains functional without.

## Consequences

- The Replit-hosted environment cannot be used as the production backend. Replit is used only for development, prototyping the operator interface, and mock services.
- A local Windows application (C# / ASP.NET Core) is required for production use.
- All data is persisted locally in SQLite; PostgreSQL is only used for optional cloud services.
- The system must handle graceful degradation of optional services without interrupting core recording.
