# ADR-003: MKV Segment Recording Strategy

**Date:** July 2026  
**Status:** Accepted

---

## Context

A full Gaelic football or hurling match lasts 70–90 minutes. Recording the entire match as a single file creates a risk: if the recording process crashes, the entire file may be unplayable. An interrupted MP4 file is typically unrecoverable.

## Decision

The recording manager will write the stream into recoverable MKV segments using FFmpeg segment muxer. Default segment duration is 5 minutes. MKV is chosen over MP4 because MKV files remain recoverable after incomplete closure; MP4 requires a complete `moov` atom.

After the match, all validated segments will be remuxed into a single MP4 file using stream copy (no re-encoding). The original MKV segments will be retained until the final MP4 has been validated by checksum.

## Consequences

- A crash or power loss invalidates only the current open segment; all closed segments are recoverable.
- The final MP4 remux adds 1–3 minutes of processing time after the match.
- Storage usage is approximately doubled until segments are deleted (after validation).
- Operators must not manually delete the `segments/` folder before post-match validation is complete.
