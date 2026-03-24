# Settings Reference

All settings are available in the GUI's **Settings** panel and in the API request body under `"settings"`. They can be saved and loaded as named **presets**.

---

## Max frames

**API field:** `maxFrames` (integer, optional)
**Default:** none (process entire video)

Limits how many video frames are passed to MediaPipe. Useful for quick testing on long videos. Leave blank or omit to analyse the whole file.

---

## Step threshold

**API field:** `stepThreshold` (number)
**Default:** `0`

The zero-crossing threshold used by the step counter. A step is counted each time the vertical hip oscillation signal crosses this value. Raising it above zero filters out small oscillations that would otherwise be counted as steps. Leave at `0` for most videos.

---

## Stance tolerance %

**API field:** `stanceTolerance` (number, stored as %)
**Default:** `5`

Controls how strictly a foot must be near the ground to be considered in the **stance phase** (the part of the stride where the foot is in contact with the treadmill belt). Specifically, it defines the tolerance band above the lowest recorded heel position for each foot:

- A heel is counted as "on the belt" if its height is within `stanceTolerance %` of its own range of motion above its minimum.
- Lower values → stricter (only the very lowest heel positions count as stance).
- Higher values → looser (a larger portion of the stride counts as stance).

Belt speed is estimated from foot velocity during stance, so this setting has a direct effect on speed accuracy.

---

## Debug steps

**API field:** not available (UI only)
**Default:** off

When checked, prints per-frame step detection output to the console. Useful for diagnosing unexpected step counts. Has no effect on the analysis result.

---

## Yaw correction

**API field:** `yawCorrectionMethod` (`"Median"` | `"PerFrame"` | `"NoYaw"`)
**Default:** `Median`

Corrects for the camera not being perfectly side-on to the treadmill. A camera placed at an angle compresses apparent horizontal motion by cos(θ), causing speed to be underestimated.

| Option | Behaviour |
|---|---|
| **Median** | Estimates a single yaw angle from the median of all frames and applies it globally. Best for fixed cameras. |
| **Per frame** | Estimates and applies a separate yaw angle each frame. Can handle gradual camera drift but is noisier. |
| **No yaw** | Skips yaw correction entirely. Use only if the camera is accurately side-on or if you want to disable this stage for debugging. |

---

## Perspective correction

**API field:** `perspectiveCorrection` (boolean)
**Default:** `true`

Recovers true hip-relative world coordinates from MediaPipe's normalised screen coordinates. MediaPipe outputs X and Y in the range [0, 1] regardless of how far the subject is from the camera — without this correction, apparent distances depend on camera distance and change as the subject moves closer or further away.

The corrector calibrates focal length and hip depth automatically by finding the values that minimise the variation in anatomically constant distances (hip width, shoulder width) across all frames.

Disabling this is not recommended for speed estimation; it is mainly useful for debugging or comparing outputs.

---

## Temporal smoothing

**API field:** `temporalSmoothing` (boolean)
**Default:** `true`

Applies a Gaussian-weighted moving average over time to each landmark's position. Reduces frame-to-frame jitter in the pose estimates without shifting the signal in time (unlike a simple rolling average). Uses a 5-frame window (2 frames either side) with σ = 1.0.

Disable if you need to inspect raw MediaPipe output or if the video already has very stable pose estimates.

---

## Visibility interpolation

**API field:** `visibilityInterpolation` (boolean)
**Default:** `true`

MediaPipe assigns each landmark a visibility score. When a landmark's visibility drops below 0.5 (e.g. a foot temporarily obscured), its position becomes unreliable. This setting linearly interpolates the landmark's X, Y, Z across such gaps, using the last visible frame before and the first visible frame after as endpoints.

Gaps at the start or end of the video (no valid endpoint on one side) are left unchanged. Runs before temporal smoothing so the smoother operates on complete trajectories.

Disable to pass raw MediaPipe positions through without filling low-visibility gaps.

---

## Presets

Any combination of the above settings can be saved as a named preset using the **Save** button in the Settings panel. Presets are stored locally in `%APPDATA%\runRobot\presets.json` and persist between sessions. Select a preset name from the dropdown to restore its values; delete it with **Delete**.
