# runRobot

Analyses treadmill running and walking videos to estimate **step count**, **belt speed**, and **calories burned** using [MediaPipe Pose](https://ai.google.dev/edge/mediapipe/solutions/vision/pose_landmarker). A Python subprocess extracts 33-landmark pose data per frame; all analysis logic is in C#.

Two ways to use it:

| Mode | Project | Description |
|---|---|---|
| GUI | `runRobot` | Windows Forms desktop app |
| API | `runRobot.Api` | HTTP microservice for remote/programmatic access |

---

## Prerequisites

### .NET
[.NET 10 SDK](https://dotnet.microsoft.com/download)

### Python
Python 3.8+ with the following packages:
```
pip install mediapipe opencv-python numpy
```

### MediaPipe model weights
The pose landmark model is **not included** in the repository (large binary). Download it manually:

1. Go to the [MediaPipe Pose Landmarker page](https://ai.google.dev/edge/mediapipe/solutions/vision/pose_landmarker)
2. Download **`pose_landmarker_heavy.task`**
3. Place it at:
   ```
   runRobot/assets/MLModels/pose_landmarker_heavy.task
   ```

---

## Building

```
dotnet build
```

---

## GUI (Windows Forms)

```
dotnet run --project runRobot
```

1. Click **Browse** and select a video file
2. Optionally fill in a **User Profile** (hip height + weight) for accurate speed scaling and calorie estimates
3. Adjust **Settings** if needed (yaw correction, stance tolerance, etc.)
4. Click **Run**

Results are displayed as an annotated frame-by-frame playback with step count, speed, and calories shown in the control bar.

---

## API (HTTP microservice)

### Setup

Copy the example secrets file and fill in your details:

```bash
cp runRobot.Api/appsettings.Development.json.example runRobot.Api/appsettings.Development.json
```

Edit `appsettings.Development.json`:

```json
{
  "ApiKeys": [
    { "Key": "your-secret-key", "Machine": "YOUR-HOSTNAME" }
  ]
}
```

- **Key** — any secret string you choose; callers send it in the `X-Api-Key` request header
- **Machine** — the hostname of the machine running the server (`hostname` in a terminal); a key only works on its paired machine

Add more entries to grant access to additional callers.

### Running

```
dotnet run --project runRobot.Api
```

The server listens on `http://localhost:5000`. Interactive API docs (Scalar) are at `http://localhost:5000/scalar/v1`.

### Submitting a job

```bash
curl -X POST http://localhost:5000/analyze \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-secret-key" \
  -d '{
    "videoPath": "C:\\Videos\\run.mp4",
    "aspectRatio": 1.7778,
    "profile": { "hipHeight": 92, "hipHeightUnit": "cm", "weight": 75, "weightUnit": "kg" }
  }'
```

Response (`202 Accepted`):
```json
{ "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6" }
```

### Polling for results

```bash
curl http://localhost:5000/analyze/3fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "X-Api-Key: your-secret-key"
```

Poll until `"status"` is `"done"`, then read the `"result"` object. See [`docs/api-usage.md`](docs/api-usage.md) for the full request/response schema and Python/C# client examples.

---

## Running tests

```
dotnet test runRobot.Api.Tests
```

---

## Project structure

```
runRobot/
├── runRobot.Core/        # Shared analysis library (no UI dependency)
├── runRobot/             # WinForms GUI
├── runRobot.Api/         # ASP.NET Core HTTP microservice
├── runRobot.Api.Tests/   # Integration tests (xUnit + WebApplicationFactory)
└── docs/
    ├── api-usage.md      # Full API reference with Python and C# examples
    └── settings.md       # Description of every pipeline setting
```
