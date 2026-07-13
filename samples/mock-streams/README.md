# Mock RTSP Streams

For development and testing without a physical camera, use the following approaches to provide a mock RTSP stream.

## Option 1 — FFmpeg Test Source (Recommended)

Generate a test RTSP stream using FFmpeg's built-in `testsrc` video source.

### Windows

```bat
ffmpeg -re -f lavfi -i testsrc=size=1920x1080:rate=25 -f lavfi -i sine=frequency=440 ^
  -c:v libx264 -preset ultrafast -tune zerolatency -b:v 4M ^
  -c:a aac -b:a 128k ^
  -f rtsp rtsp://localhost:8554/test
```

### Linux / macOS (development only)

```bash
ffmpeg -re -f lavfi -i testsrc=size=1920x1080:rate=25 -f lavfi -i sine=frequency=440 \
  -c:v libx264 -preset ultrafast -tune zerolatency -b:v 4M \
  -c:a aac -b:a 128k \
  -f rtsp rtsp://localhost:8554/test
```

### Configuration

Once the test source is running, configure the mock camera in AMHARC Match Capture:

```json
{
  "name": "Test Source",
  "manufacturer": "generic-rtsp",
  "adapter": "GenericRtspAdapter",
  "ipAddress": "127.0.0.1",
  "rtspUrl": "rtsp://localhost:8554/test",
  "resolution": "1920x1080",
  "frameRate": 25,
  "codec": "H.264"
}
```

## Option 2 — MediaMTX (RTSP Server)

[MediaMTX](https://github.com/bluenviron/mediamtx) is a lightweight RTSP server suitable for development.

```bash
# Download and run MediaMTX, then publish a test stream to it
```

## Option 3 — VLC

VLC can stream a test pattern or a local video file as an RTSP source.

```
vlc -vvv sample.mp4 --sout '#rtp{sdp=rtsp://localhost:8554/stream}' --sout-all --loop
```
