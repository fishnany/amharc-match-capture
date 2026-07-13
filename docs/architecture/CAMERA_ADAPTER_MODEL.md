# AMHARC Match Capture — Camera Adapter Model

**Version:** 0.1.0  
**Date:** July 2026

---

## 1. Design Principles

The camera adapter model enforces strict vendor neutrality. All camera-specific behaviour is isolated behind the `ICameraAdapter` and `IPtzController` interfaces. The rest of the system interacts only with these interfaces and has no knowledge of the underlying manufacturer or protocol.

**Benefits:**
- Add support for a new camera by implementing two interfaces, not changing existing code
- Test the system with a mock camera adapter without physical hardware
- Maintain a single configuration model across all manufacturers

---

## 2. ICameraAdapter Interface

```csharp
namespace Amharc.MatchCapture.CameraAbstractions;

public interface ICameraAdapter
{
    string CameraId { get; }
    string Manufacturer { get; }
    string? Model { get; }
    CameraConnectionState ConnectionState { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<string> GetStreamUrlAsync(string? profileName = null, CancellationToken ct = default);
    Task<CameraInfo> GetCameraInfoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StreamProfile>> GetStreamProfilesAsync(CancellationToken ct = default);
    Task ReconnectAsync(CancellationToken ct = default);
    
    event EventHandler<CameraConnectionStateChangedEventArgs> ConnectionStateChanged;
    event EventHandler<CameraHealthEventArgs> HealthChanged;
}

public enum CameraConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

public record CameraInfo(
    string? Model,
    string? SerialNumber,
    string? FirmwareVersion,
    string? MacAddress
);

public record StreamProfile(
    string Name,
    string? Resolution,
    int? FrameRate,
    string? Codec,
    int? BitRate
);
```

---

## 3. IPtzController Interface

```csharp
namespace Amharc.MatchCapture.CameraAbstractions;

public interface IPtzController
{
    Task PanAsync(PtzDirection direction, float speed, CancellationToken ct = default);
    Task TiltAsync(PtzDirection direction, float speed, CancellationToken ct = default);
    Task ZoomAsync(ZoomDirection direction, float speed, CancellationToken ct = default);
    Task MoveAbsoluteAsync(float pan, float tilt, float zoom, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task GoHomeAsync(CancellationToken ct = default);
    Task RecallPresetAsync(string presetId, CancellationToken ct = default);
    Task SavePresetAsync(string presetId, string presetName, CancellationToken ct = default);
    Task EmergencyWideAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PtzPreset>> GetPresetsAsync(CancellationToken ct = default);
}

public enum PtzDirection { Left, Right, Up, Down }
public enum ZoomDirection { In, Out }

public record PtzPreset(string PresetId, string Name, bool IsHome);
```

---

## 4. AxisCameraAdapter

The AXIS Q6128-E adapter is the first concrete implementation.

**Protocol:** AXIS VAPIX API (HTTP/HTTPS)  
**Authentication:** Digest authentication  
**PTZ:** VAPIX CGI endpoints (`/axis-cgi/com/ptz.cgi`)  
**Stream URL:** RTSP (`rtsp://{ip}/axis-media/media.amp`)  
**Stream profiles:** Retrieved via VAPIX `streamprofile.cgi`  
**Camera info:** Retrieved via VAPIX `basicdeviceinfo.cgi`  

**VAPIX PTZ command example:**
```
GET /axis-cgi/com/ptz.cgi?continuouspantiltmove=10,-5 HTTP/1.1
```

**Axis-specific error handling:**
- HTTP 401 → authentication failure → log and surface credential error
- HTTP 500 → camera internal error → log, mark as `Error`, schedule reconnect
- Connection timeout → mark as `Reconnecting`, begin reconnect loop

---

## 5. OnvifAdapter (Planned)

The ONVIF adapter will implement `ICameraAdapter` and `IPtzController` using the ONVIF Device, Media, and PTZ services.

**Protocol:** ONVIF (WS-Discovery, SOAP)  
**PTZ:** ONVIF PTZ service (ContinuousMove, GotoPreset, SetPreset)  
**Authentication:** WS-Security UsernameToken  

This adapter will support any ONVIF-compliant camera, covering manufacturers such as Canon, Panasonic, Sony, PTZOptics, BirdDog, AVer, and Bolin.

**Status:** Planned for Phase 2+

---

## 6. GenericRtspAdapter (Planned)

A read-only adapter for cameras that support RTSP but not ONVIF or any manufacturer-specific PTZ API.

**Capabilities:** Stream connection only. No PTZ.  
**Use case:** Fixed cameras, wide-angle secondary cameras.

**Status:** Planned for Phase 7 (multi-camera)

---

## 7. MockCameraAdapter (Development)

Used in Replit and development environments where no physical camera is available.

**Behaviour:**
- Simulates connection, disconnection, and reconnection
- Returns a configurable test RTSP URL (e.g. a local test stream)
- Simulates PTZ commands without hardware
- Returns pre-configured camera info (model, serial number)
- Can simulate failure scenarios: stream interruption, authentication failure

**Location:** `adapters/mock/MockCameraAdapter.cs`

---

## 8. Future Adapter Candidates

| Manufacturer | Likely Protocol | PTZ | Notes |
|-------------|----------------|-----|-------|
| Axis | VAPIX | Yes | Initial implementation |
| Canon | ONVIF | Yes | Via OnvifAdapter |
| Panasonic | ONVIF | Yes | Via OnvifAdapter |
| Sony | ONVIF | Yes | Via OnvifAdapter |
| PTZOptics | ONVIF / VISCA | Yes | VISCA-over-IP extension may be required |
| BirdDog | NDI / ONVIF | Yes | NDI adapter required for NDI output |
| AVer | ONVIF | Yes | Via OnvifAdapter |
| Bolin | ONVIF | Yes | Via OnvifAdapter |
| Generic RTSP | RTSP only | No | GenericRtspAdapter |

---

*AMHARC Match Capture — Camera Adapter Model v0.1.0*
