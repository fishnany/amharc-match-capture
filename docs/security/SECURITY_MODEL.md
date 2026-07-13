# AMHARC Match Capture — Security Model

**Version:** 0.1.0  
**Date:** July 2026

---

## 1. Credential Storage

### Camera Credentials

Camera usernames and passwords must never be stored in plain text.

On Windows, credentials are stored using the Windows Credential Manager (DPAPI encryption):
- Credential target: `Amharc.MatchCapture.Camera.{cameraId}`
- The SQLite database stores only the credential reference, not the credentials themselves.

### Streaming Credentials (Stream Keys)

Stream keys are encrypted at rest using AES-256-CBC:
- The encryption key is derived from the Windows machine SID using DPAPI.
- Encrypted values are stored in SQLite under the `StreamingDestination` table.
- Stream keys must never appear in application logs.

### Log Redaction

The structured logger must redact the following fields:
- `Authorization` header
- `Cookie` header
- Any field named `password`, `streamKey`, `secret`, or `credential`

---

## 2. Local API Access

The local API (`http://localhost:5000/api`) listens only on `127.0.0.1` by default.

- Connections from external IP addresses are rejected at the network layer.
- The WebSocket endpoint (`ws://localhost:5000/ws`) follows the same binding.

If remote administration is enabled:
- Connections must use HTTPS with a valid TLS certificate.
- All requests require a bearer token (JWT, RS256 signed).
- Tokens are issued by the local agent and expire after 24 hours.

---

## 3. Network Exposure

The following ports are used locally and must not be exposed externally:

| Port | Protocol | Service | Binding |
|------|----------|---------|---------|
| 5000 | HTTP | Local Agent API | 127.0.0.1 |
| 5001 | HTTP | Overlay Renderer | 127.0.0.1 |
| 5002 | WS | WebSocket | 127.0.0.1 |

The camera must be connected on a private Ethernet segment (isolated from the public Internet) to prevent camera credentials from being exposed.

---

## 4. Input Validation

All API inputs must be validated using Zod schemas (TypeScript frontend) and FluentValidation (C# local agent).

- Path parameters: type-checked, bounds-checked
- Request bodies: validated against OpenAPI-derived schemas
- File paths (recording directories): validated to prevent path traversal (`../` sequences are rejected)
- FFmpeg arguments: constructed only from pre-approved templates; no free-form arguments from user input

---

## 5. FFmpeg Security

FFmpeg is invoked programmatically with:
- Fixed argument templates defined in code
- No shell interpolation (arguments passed as `string[]`, never as a shell command string)
- Configurable parameters limited to: output path (validated), bitrate (integer), segment duration (integer), codec (from allowlist)
- Input stream URL validated against a strict format (must begin with `rtsp://` or `file://`)

Direct construction of FFmpeg arguments from untrusted user input is prohibited.

---

## 6. Audit Log

All security-relevant actions are written to the structured log and to the `TechnicalLogEntry` table:

- Clock corrections (who, when, before value, after value, reason)
- Score corrections (who, when, before value, after value)
- Camera credential changes
- Stream key changes
- Recording start/stop
- Application start/shutdown
- Authentication failures (remote access)

---

## 7. Encryption

| Asset | Algorithm | Key Source |
|-------|-----------|-----------|
| Camera credentials | Windows DPAPI | Windows machine SID |
| Stream keys | AES-256-CBC | DPAPI-protected key |
| Remote API TLS | TLS 1.2+ | Self-signed or CA-signed cert |
| SQLite database | None (file-system protected) | Windows ACL |

The SQLite file is protected by Windows NTFS ACLs and is accessible only to the local user account running the agent.

---

## 8. Prohibited Patterns

- Hard-coded camera addresses, credentials, or stream keys in source code
- Storing passwords in environment variables in production
- Logging any field named `password`, `streamKey`, or `credential`
- Constructing shell commands from user input
- Exposing the local API on `0.0.0.0` without explicit administrative configuration
- Exposing the camera directly to the public Internet

---

*AMHARC Match Capture — Security Model v0.1.0*
