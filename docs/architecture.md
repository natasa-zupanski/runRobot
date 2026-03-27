# runRobot Architecture Diagrams

## Files

| File | Description |
|---|---|
| `factored_architecture.puml` | 6-page diagram — one page per subsystem. Start here. |
| `expanded_architecture.puml` | Single large diagram with every class and relationship in one view. |

---

## How to View

### Prerequisites
Install the [PlantUML extension for VS Code](https://marketplace.visualstudio.com/items?itemName=jebbs.plantuml) and a Java runtime (required by PlantUML).

### Opening the preview
1. Open `factored_architecture.puml` in VS Code
2. Press **Alt+D** to open the live preview panel
3. Navigate between pages with **PageDown / PageUp**, or use the arrow buttons at the top of the preview

### Exporting to images
- **Current page**: right-click in the editor → **Export Current Diagram**
- **All pages**: open the command palette (**Ctrl+Shift+P**) → **PlantUML: Export Current File Diagrams**

Images are exported to `docs/out/factored_architecture/` by default. Each page is saved as a separate PNG named after its diagram title.

---

## Pages in `factored_architecture.puml`

### Page 1 — System Overview
A component-level view of the four projects and their dependencies. Shows:
- **runRobot** (WinForms GUI) and **runRobot.Api** (ASP.NET Core) both referencing **runRobot.Core**
- **runRobot.Core** spawning the Python/MediaPipe subprocess (`pose_analyzer.py`)
- Both apps reading and writing user profiles and presets via `AppDataStore` to `%APPDATA%\runRobot\`
- **runRobot.Api.Tests** using `WebApplicationFactory` against the API project

Use this page to understand the overall project structure before diving into the details.

### Page 2 — Data Models
Classes in `runRobot.Models` and `runRobot.Storage`. These are the data types that flow through the entire pipeline:
- **`Landmark`** / **`Landmark2D`** — a single 3D or 2D pose landmark from MediaPipe
- **`PoseFrame`** / **`SideViewFrame`** — one video frame's worth of 33 landmarks (3D and 2D respectively)
- **`UserProfile`** — named runner with hip height and weight (persisted to JSON)
- **`SettingsPreset`** — named pipeline configuration (persisted to JSON)
- **`AppDataStore`** — static class that serialises profiles and presets to `%APPDATA%\runRobot\`

### Page 3 — Preprocessing Pipeline
Classes in `runRobot.Preprocessing`. Shows the corrector class hierarchy and how raw `PoseFrame`s are cleaned up before analysis:
- **`PoseCorrector`** — abstract base with `CorrectAll(frames)`
- **`FramewiseCorrector`** — abstract base for frame-independent corrections; override `Correct(frame)`
- **`PerspectiveDistortionCorrector`** — calibrated unprojection from screen coords to hip-relative world coords
- **`YawCorrector`** — rotates the XZ plane to align the runner's travel direction with the X axis
- **`TemporalSmoothingCorrector`** — Gaussian moving average over time to reduce jitter
- **`VisibilityInterpolator`** — fills gaps where landmarks have low MediaPipe confidence
- **`PoseCorrectorFactory`** — maps `PoseCorrectorStep` enum values to corrector instances; defines canonical step order
- **`PoseCorrectorPipeline`** — applies the active corrector steps (`Correct`), then projects to 2D side-view frames (`Project`)

### Page 4 — Analysis Core
Classes in `runRobot.Core` and `runRobot.Speed`. Shows the full analysis pipeline from pose frames to results:
- **`PoseAnalyzer`** — spawns the Python subprocess and parses its output into `List<PoseFrame>`
- **`StepEstimator`** — counts steps via zero-crossings in corrected ankle positions
- **`GaitVelocityAnalyzer`** — computes per-landmark velocities from `SideViewFrame` sequences
- **`TreadmillSpeedEstimator`** — estimates belt speed from foot velocity during stance phase
- **`SpeedEstimator`** — orchestrates velocity analysis and scale-factor computation; produces `SpeedEstimationResult`
- **`CalorieEstimator`** — MET-based calorie burn from per-frame speed and body weight
- **`AnalysisPipeline`** — top-level orchestrator used by both the WinForms app and the API; coordinates all stages and returns `AnalysisResult`

### Page 5 — UI
Classes in the `runRobot` WinForms project. Shows the GUI structure:
- **`MainWindow`** — primary window; owns the sidebar and frame view; calls `AnalysisPipeline` on run
- **`ProfilePanel`** — sidebar panel for managing named user profiles (hip height, weight)
- **`PresetPanel`** — sidebar panel for managing named settings presets (all pipeline parameters)
- **`FrameViewPanel`** — right-hand panel with `PictureBox`, playback controls, and speed/calorie labels
- **`PoseVisualizer`** — static helper that renders a `PoseFrame` to a `Bitmap` (skeleton overlay)
- **`SideLabel`** — `Label` subclass with consistent sidebar styling (used in `TableLayoutPanel` column 0)
- **`PromptDialog`** — static helper that shows a minimal single-line text input dialog

### Page 6 — API
Classes in `runRobot.Api`. Shows the HTTP microservice:
- **`ApiKeyMiddleware`** — validates `X-Api-Key` header; keys are machine-scoped
- **`AnalyzeEndpoints`** — maps `POST /analyze` (submit job) and `GET /analyze/{jobId}` (poll status)
- **`AnalysisJobStore`** — singleton in-memory store of running and completed jobs
- **`AnalysisJob`** — job record with status, progress string, and `AnalysisResult` when done
- **`AnalyzeRequest`** — request body DTO (video path, aspect ratio, optional settings and profile)
- **`JobStatusResponse`** / **`AnalyzeResultDto`** — response DTOs (slim, serialisable; omit large intermediate collections)
