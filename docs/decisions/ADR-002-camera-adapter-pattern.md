# ADR-002: Camera Adapter Pattern

**Date:** July 2026  
**Status:** Accepted

---

## Context

The initial camera is the AXIS Q6128-E, which uses VAPIX for PTZ control and RTSP for video. Future cameras from Canon, Panasonic, Sony, PTZOptics, BirdDog, and generic RTSP sources must also be supported. Camera-specific behaviour must not bleed into the recording, overlay, or streaming components.

## Decision

All camera and PTZ functionality will be accessed through the `ICameraAdapter` and `IPtzController` interfaces. Each manufacturer or protocol will be implemented as a separate adapter class. The `AxisCameraAdapter` is the first concrete implementation. An ONVIF adapter will cover most other manufacturers.

## Consequences

- Adding support for a new camera requires implementing two interfaces only; no changes to the recording, overlay, or streaming components.
- Mock adapters can be used in Replit and unit tests without physical hardware.
- The initial implementation is tightly coupled to Axis VAPIX; this must not become a permanent pattern. All Axis-specific code must remain within `AxisCameraAdapter`.
