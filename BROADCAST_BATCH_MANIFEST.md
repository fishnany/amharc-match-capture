# AMHARC Broadcast Lite Batch Manifest

Engineering batch: **1.1 + 1.2 + 1.3**

Key implementation areas:

- `agent-windows/src/AmharcAgent.Core/Broadcast/`
- `agent-windows/src/AmharcAgent.Core/Interfaces/I*Renderer.cs`
- `agent-windows/src/AmharcAgent.Infrastructure/Broadcast/`
- `agent-windows/src/AmharcAgent.Api/Controllers/BroadcastController.cs`
- `agent-windows/src/AmharcAgent.Infrastructure/Events/EventTaggingService.cs`
- `agent-windows/tests/AmharcAgent.Tests/*RendererTests.cs`
- `docs/broadcast/`
- canonical branding assets under API `wwwroot` and Operator UI `public/branding`

The batch deliberately keeps video composition/export separate from the rendering/control plane. 1.3 is an application-native ten-second animated SVG primitive; encoding to MOV/MP4 is a later compositor/export concern.
