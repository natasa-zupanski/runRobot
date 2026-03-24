#!/usr/bin/env python3
"""
Overlay pose landmarks on video frames and save as video or frames.
Usage: python overlay_pose_on_video.py <video_path> <pose_json_path> <output_path> [--format video|frames] [--max-frames N]
"""

import cv2
import json
import sys
import os
from pathlib import Path

# Landmark names for reference
LANDMARK_NAMES = [
    "Nose", "Left Eye (Inner)", "Left Eye", "Left Eye (Outer)", "Right Eye (Inner)",
    "Right Eye", "Right Eye (Outer)", "Left Ear", "Right Ear", "Mouth (Left)",
    "Mouth (Right)", "Left Shoulder", "Right Shoulder", "Left Elbow", "Right Elbow",
    "Left Wrist", "Right Wrist", "Left Pinky", "Right Pinky", "Left Index",
    "Right Index", "Left Thumb", "Right Thumb", "Left Hip", "Right Hip",
    "Left Knee", "Right Knee", "Left Ankle", "Right Ankle", "Left Heel",
    "Right Heel", "Left Foot Index", "Right Foot Index"
]

# Skeleton connections (bone pairs)
SKELETON_CONNECTIONS = [
    (0, 1), (1, 2), (2, 3), (0, 4), (4, 5), (5, 6),  # Face
    (9, 10),  # Mouth
    (11, 12), (11, 13), (13, 15), (12, 14), (14, 16),  # Arms
    (11, 23), (12, 24), (23, 24),  # Torso
    (23, 25), (25, 27), (27, 29), (29, 31),  # Left leg
    (24, 26), (26, 28), (28, 30), (30, 32),  # Right leg
    (15, 17), (17, 19), (19, 21),  # Left hand
    (16, 18), (18, 20), (20, 22),  # Right hand
]

def get_color_by_visibility(visibility, high=(0, 255, 0), low=(0, 0, 255)):
    """Get color based on landmark visibility (confidence)."""
    # Blend from low (red) to high (green) based on visibility
    r = int(low[0] + (high[0] - low[0]) * visibility)
    g = int(low[1] + (high[1] - low[1]) * visibility)
    b = int(low[2] + (high[2] - low[2]) * visibility)
    return (b, g, r)  # OpenCV uses BGR

def draw_landmarks_on_frame(frame, landmarks, frame_width, frame_height):
    """Draw landmarks and skeleton connections on video frame."""
    output_frame = frame.copy()
    
    # Convert normalized coordinates to pixel coordinates
    points = []
    for landmark in landmarks:
        x = int(landmark['x'] * frame_width)
        y = int(landmark['y'] * frame_height)
        visibility = landmark.get('visibility', 0.5)
        points.append((x, y, visibility))
    
    # Draw skeleton connections
    for start_idx, end_idx in SKELETON_CONNECTIONS:
        if start_idx < len(points) and end_idx < len(points):
            x1, y1, v1 = points[start_idx]
            x2, y2, v2 = points[end_idx]
            
            # Only draw if both landmarks have reasonable visibility
            if v1 > 0.2 and v2 > 0.2:
                color = get_color_by_visibility((v1 + v2) / 2)
                cv2.line(output_frame, (x1, y1), (x2, y2), color, 2)
    
    # Draw landmark points
    for idx, (x, y, visibility) in enumerate(points):
        if visibility > 0.2:
            color = get_color_by_visibility(visibility)
            cv2.circle(output_frame, (x, y), 5, color, -1)
            cv2.circle(output_frame, (x, y), 5, (255, 255, 255), 1)
    
    return output_frame

def overlay_pose_on_video(video_path, pose_json_path, output_path, output_format="video", max_frames=None):
    """Overlay pose landmarks on video frames."""
    
    # Load pose data
    print(f"Loading pose data from: {pose_json_path}")
    # debug: check file existence and size before opening
    try:
        exists = os.path.exists(pose_json_path)
        size = os.path.getsize(pose_json_path) if exists else 0
        print(f"Debug: file exists={exists}, size={size} bytes")
    except Exception:
        print("Debug: could not check file stats")

    # helper to attempt parsing with different encodings
    def _load_json_with_encodings(path):
        # read raw bytes so we can inspect BOM
        with open(path, 'rb') as bf:
            raw = bf.read()
        # try utf-8-sig (handles BOM)
        for enc in ('utf-8-sig', 'utf-8', 'utf-16', 'latin-1'):
            try:
                text = raw.decode(enc)
                return json.loads(text), enc
            except UnicodeDecodeError:
                continue
            except json.JSONDecodeError as jde:
                # even if decode succeeded, JSON may still be invalid
                print(f"DEBUG: decoded with {enc} but JSON parsing failed: {jde}")
                continue
        # If we got here, nothing worked
        raise ValueError("Unable to decode and parse JSON file with known encodings")

    try:
        pose_data, used_enc = _load_json_with_encodings(pose_json_path)
        print(f"Debug: successfully decoded pose file using encoding '{used_enc}'")
    except Exception as e:
        # more detailed error info
        print(f"ERROR: Failed to load pose data: {e}")
        try:
            with open(pose_json_path, 'rb') as f2:
                raw = f2.read(100)
                print(f"DEBUG raw bytes: {raw}")
        except Exception:
            pass
        return False

    print(f"Loaded {len(pose_data)} frames of pose data")
    
    # Open video
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"ERROR: Cannot open video: {video_path}")
        return False
    
    fps = cap.get(cv2.CAP_PROP_FPS)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    
    print(f"Video: {width}x{height} @ {fps} fps, {total_frames} frames")
    
    if max_frames:
        total_frames = min(total_frames, max_frames)
    
    # Setup output
    if output_format == "video":
        os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)
        fourcc = cv2.VideoWriter_fourcc(*'mp4v')
        out = cv2.VideoWriter(output_path, fourcc, fps, (width, height))
        if not out.isOpened():
            print(f"ERROR: Cannot create output video: {output_path}")
            return False
    else:  # frames
        output_dir = output_path
        os.makedirs(output_dir, exist_ok=True)
    
    # Process frames
    frame_idx = 0
    while frame_idx < total_frames:
        ret, frame = cap.read()
        if not ret:
            break
        
        # Get corresponding pose data
        if frame_idx < len(pose_data):
            pose_frame = pose_data[frame_idx]
            landmarks = pose_frame.get('landmarks', [])
            
            # Draw landmarks on frame
            annotated_frame = draw_landmarks_on_frame(frame, landmarks, width, height)
            
            # Add frame number text
            cv2.putText(annotated_frame, f"Frame: {frame_idx}", (10, 30),
                       cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
            
            if output_format == "video":
                out.write(annotated_frame)
            else:
                frame_file = os.path.join(output_dir, f"frame_{frame_idx:04d}.png")
                cv2.imwrite(frame_file, annotated_frame)
                print(f"Saved: {frame_file}")
        
        frame_idx += 1
        if frame_idx % 10 == 0:
            print(f"Processed {frame_idx}/{total_frames} frames")
    
    cap.release()
    if output_format == "video":
        out.release()
    
    print(f"\n✓ Successfully processed {frame_idx} frames")
    print(f"✓ Output saved to: {output_path}")
    
    return True

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python overlay_pose_on_video.py <video_path> <pose_json_path> <output_path> [--format video|frames] [--max-frames N]")
        print("\nExample:")
        print("  python overlay_pose_on_video.py input/pexels_walking.mp4 output/pexels_walking_pose.json output/pexels_overlay.mp4 --format video")
        print("  python overlay_pose_on_video.py input/pexels_walking.mp4 output/pexels_walking_pose.json output/frames_overlay --format frames --max-frames 10")
        sys.exit(1)
    
    video_path = sys.argv[1]
    pose_json_path = sys.argv[2]
    output_path = sys.argv[3]
    
    output_format = "video"  # Default
    max_frames = None
    
    # Parse optional arguments
    i = 4
    while i < len(sys.argv):
        if sys.argv[i] == "--format":
            output_format = sys.argv[i + 1]
            i += 2
        elif sys.argv[i] == "--max-frames":
            max_frames = int(sys.argv[i + 1])
            i += 2
        else:
            i += 1
    
    success = overlay_pose_on_video(video_path, pose_json_path, output_path, output_format, max_frames)
    sys.exit(0 if success else 1)
