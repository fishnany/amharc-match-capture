# ADR-005: SQLite for Local Match Data

**Date:** July 2026  
**Status:** Accepted

---

## Context

The local agent must persist match metadata, events, recording segments, clock state, and health events during a match. This persistence must work without any network connection and must not require a separately installed or managed database server.

## Decision

SQLite (accessed via Entity Framework Core) will be used for all local data persistence in the Windows Capture Agent.

Cloud or centralised deployments may use PostgreSQL for multi-operator reporting and metadata synchronisation. This remains optional and must not be required for local match operation.

## Consequences

- No database server installation is required on the match laptop.
- The SQLite file is a single portable file that can be backed up by copying it.
- Concurrent write performance is limited, but a single-operator system does not require concurrent writes.
- The SQLite file must be protected by Windows NTFS ACLs to prevent unauthorised access.
- EF Core migrations will be used to manage schema evolution.
