# runRobot API Usage Guide

The `runRobot.Api` project exposes an HTTP API for submitting video analysis jobs and polling their results. Analysis runs asynchronously — you submit a job, receive a `jobId`, and poll until the status is `done`.

## Starting the server

```
dotnet run --project runRobot.Api
```

By default the server listens on `http://localhost:5000` (or the port shown in the console). In development mode, the interactive API reference (Scalar) is available at `http://localhost:5000/scalar/v1`.

---

## Endpoints

### POST /analyze

Submit a video for analysis. Returns immediately with a `jobId`.

**Request body** (JSON):

| Field | Type | Required | Description |
|---|---|---|---|
| `videoPath` | string | Yes | Absolute path to the video file **on the server machine** |
| `aspectRatio` | number | No | Video width ÷ height (e.g. `1.7778` for 16:9). Defaults to `1.0`. Pass the correct value to avoid underestimating horizontal speed. |
| `settings` | object | No | Pipeline settings (see below). Defaults are used when omitted. |
| `profile` | object | No | Runner body metrics for speed scaling and calorie estimation. Calories are only estimated when `weight` is provided. |

**`settings` object:**

| Field | Type | Default | Description |
|---|---|---|---|
| `maxFrames` | integer or null | null | Cap the number of frames processed. Null = process all. |
| `stepThreshold` | number | `0` | Zero-crossing threshold for step detection. |
| `stanceTolerance` | number | `5` | Stance phase tolerance as a percentage (e.g. `5` = 5%). |
| `yawCorrectionMethod` | string | `"Median"` | One of `"Median"`, `"PerFrame"`, `"NoYaw"`. |
| `perspectiveCorrection` | boolean | `true` | Apply perspective distortion correction. |
| `temporalSmoothing` | boolean | `true` | Apply Gaussian temporal smoothing. |
| `visibilityInterpolation` | boolean | `true` | Interpolate across low-visibility landmark gaps. |

**`profile` object:**

| Field | Type | Default | Description |
|---|---|---|---|
| `hipHeight` | number or null | null | Hip height value (used for speed scale calibration). |
| `hipHeightUnit` | string | `"cm"` | `"cm"` or `"in"` |
| `weight` | number or null | null | Body weight (required for calorie estimation). |
| `weightUnit` | string | `"kg"` | `"kg"` or `"lbs"` |

**Response** — `202 Accepted`:

```json
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

### GET /analyze/{jobId}

Poll the status of a previously submitted job.

**Response** — `200 OK`:

```json
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "done",
  "progress": "Done — 1200 frames",
  "result": {
    "estimatedSteps": 142,
    "speed": 3.21,
    "scaleFactor": 0.48,
    "calories": 87.4,
    "averageMet": 8.3,
    "durationMinutes": 4.1,
    "averageSpeedMph": 6.8,
    "runningFraction": 0.94,
    "speedPerFrame": [null, null, 3.1, 3.2, ...]
  },
  "error": null
}
```

**`status` values:** `queued` → `running` → `done` | `failed`

**`result` fields:**

| Field | Description |
|---|---|
| `estimatedSteps` | Total step count for the video |
| `speed` | Median belt speed in world units/s (multiply by `scaleFactor` × 2.23694 for mph) |
| `scaleFactor` | Metres per world unit — multiply by `speed` to get m/s |
| `calories` | Total kilocalories burned. Null when no weight was provided. |
| `averageMet` | Average MET value over the session. Null when no weight was provided. |
| `durationMinutes` | Effective session duration in minutes. Null when no weight was provided. |
| `averageSpeedMph` | Average speed in mph. Null when no weight was provided. |
| `runningFraction` | Fraction of frames classified as running vs. walking (0–1). Null when no weight was provided. |
| `speedPerFrame` | Per-frame belt speed in world units/s. Null for frames outside the stance phase. |

**`result`** is `null` until `status` is `"done"`. **`error`** is `null` unless `status` is `"failed"`.

Returns `404 Not Found` if the `jobId` does not exist (jobs are held in memory and cleared on server restart).

---

## Examples

### curl

**Submit a job:**

```bash
curl -X POST http://localhost:5000/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "videoPath": "C:\\Videos\\treadmill_run.mp4",
    "aspectRatio": 1.7778,
    "settings": {
      "yawCorrectionMethod": "Median",
      "stanceTolerance": 5
    },
    "profile": {
      "hipHeight": 92,
      "hipHeightUnit": "cm",
      "weight": 75,
      "weightUnit": "kg"
    }
  }'
```

Response:
```json
{"jobId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}
```

**Poll until done:**

```bash
curl http://localhost:5000/analyze/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

---

### Python

```python
import requests
import time

BASE = "http://localhost:5000"

# Submit
resp = requests.post(f"{BASE}/analyze", json={
    "videoPath": r"C:\Videos\treadmill_run.mp4",
    "aspectRatio": 16 / 9,
    "profile": {
        "hipHeight": 92,
        "hipHeightUnit": "cm",
        "weight": 75,
        "weightUnit": "kg",
    },
})
resp.raise_for_status()
job_id = resp.json()["jobId"]
print(f"Job submitted: {job_id}")

# Poll
while True:
    poll = requests.get(f"{BASE}/analyze/{job_id}").json()
    status = poll["status"]
    print(f"  {status}: {poll['progress']}")
    if status == "done":
        r = poll["result"]
        print(f"Steps: {r['estimatedSteps']}")
        speed_ms = r["speed"] * r["scaleFactor"]
        print(f"Speed: {speed_ms * 2.23694:.2f} mph")
        if r["calories"] is not None:
            print(f"Calories: {r['calories']:.1f} kcal")
        break
    if status == "failed":
        print(f"Error: {poll['error']}")
        break
    time.sleep(2)
```

---

### C# (HttpClient)

```csharp
using System.Net.Http.Json;

var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

// Submit
var jobResp = await client.PostAsJsonAsync("/analyze", new
{
    videoPath   = @"C:\Videos\treadmill_run.mp4",
    aspectRatio = 16.0 / 9.0,
    profile     = new { hipHeight = 92, hipHeightUnit = "cm", weight = 75.0, weightUnit = "kg" },
});
jobResp.EnsureSuccessStatusCode();

var jobId = (await jobResp.Content.ReadFromJsonAsync<JobSubmitResponse>())!.JobId;
Console.WriteLine($"Job submitted: {jobId}");

// Poll
while (true)
{
    var poll = await client.GetFromJsonAsync<JobPollResponse>($"/analyze/{jobId}");
    Console.WriteLine($"  {poll!.Status}: {poll.Progress}");

    if (poll.Status == "done")
    {
        var r = poll.Result!;
        var speedMph = r.Speed * r.ScaleFactor * 2.23694;
        Console.WriteLine($"Steps:    {r.EstimatedSteps}");
        Console.WriteLine($"Speed:    {speedMph:F2} mph");
        if (r.Calories.HasValue)
            Console.WriteLine($"Calories: {r.Calories:F1} kcal");
        break;
    }
    if (poll.Status == "failed")
    {
        Console.WriteLine($"Error: {poll.Error}");
        break;
    }
    await Task.Delay(2000);
}

// Minimal DTOs for deserialization
record JobSubmitResponse(Guid JobId);
record JobPollResponse(Guid JobId, string Status, string Progress, ResultDto? Result, string? Error);
record ResultDto(int EstimatedSteps, double Speed, double ScaleFactor,
                 double? Calories, double? AverageMet, double? AverageSpeedMph,
                 double? RunningFraction, double? DurationMinutes);
```

---

## Speed conversion

The `speed` field is in **world units/s**. To convert to real-world speeds:

```
speed_m_s = speed × scaleFactor
speed_mph = speed_m_s × 2.23694
speed_kph = speed_m_s × 3.6
```

`scaleFactor` is derived from the runner's limb proportions (or from `hipHeight` when provided). It is `null` when the pipeline could not estimate it (e.g. very short clip with no visible heels).

## Notes

- **Video path is resolved on the server.** The path must be accessible from the machine running `runRobot.Api`, not the caller's machine.
- **Jobs are in-memory only.** Restarting the server clears all jobs and results.
- **Calorie fields are null when no weight is supplied.** Pass a `profile` with `weight` to get calorie estimates.
- **`speedPerFrame` can be large.** For a 30 fps, 5-minute video this is ~9 000 entries. Omit it from display if not needed.
