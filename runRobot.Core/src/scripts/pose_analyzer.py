#!/usr/bin/env python3
"""
MediaPipe Pose Analyzer - Processes video and outputs joint landmarks as JSON
"""

import sys
import os
import json
import cv2
import numpy as np
from typing import List, Dict, Any

class PoseAnalyzer:
    def __init__(self):
        """Initialize with MediaPipe Tasks PoseLandmarker API."""
        self.pose = None
        self.mp = None
        self._init_error = None  # stores the real failure reason for analyze_video to re-raise

        # Attempt MediaPipe imports
        try:
            import mediapipe as mp
            from mediapipe.tasks.python import vision
            from mediapipe.tasks import python as mp_python
            self.mp = mp
        except Exception as e:
            self._init_error = (
                f"MediaPipe import failed: {e}\n"
                f"Fix: pip install mediapipe"
            )
            print(self._init_error, file=sys.stderr)
            return

        # Prefer --model-path arg (passed by C# host); fall back to source-tree relative path.
        script_dir = os.path.dirname(os.path.abspath(__file__))
        _model_path_arg = None
        for _i, _a in enumerate(sys.argv):
            if _a == "--model-path" and _i + 1 < len(sys.argv):
                _model_path_arg = sys.argv[_i + 1]
                break
            if _a.startswith("--model-path="):
                _model_path_arg = _a.split("=", 1)[1]
                break
        if _model_path_arg:
            model_path = _model_path_arg
        else:
            # src/scripts/ → ../../assets/MLModels/
            model_path = os.path.join(os.path.dirname(os.path.dirname(script_dir)), "assets", "MLModels", "pose_landmarker_heavy.task")

        if not os.path.exists(model_path):
            self._init_error = (
                f"Model file not found: {model_path}\n"
                f"Expected pose_landmarker_heavy.task in the MLModels/ folder next to scripts/"
            )
            print(self._init_error, file=sys.stderr)
            return

        try:
            base_options = mp_python.BaseOptions(model_asset_path=model_path)
            options = vision.PoseLandmarkerOptions(
                base_options=base_options,
                running_mode=vision.RunningMode.VIDEO,
            )
            self.pose = vision.PoseLandmarker.create_from_options(options)
            print("Using MediaPipe Tasks PoseLandmarker", file=sys.stderr)
        except Exception as e:
            self._init_error = f"Failed to create PoseLandmarker: {e}"
            print(self._init_error, file=sys.stderr)
            self.pose = None
    
    # helper to convert results to landmarks list
    def extract_landmarks(self, result):
        lm_list = []
        if result.pose_landmarks:
            # pose_landmarks may be a list of NormalizedLandmarkList objects or
            # a list-of-lists of landmarks; handle both forms.
            for landmark_list in result.pose_landmarks:
                # landmark_list might itself be a list or have a .landmark attr
                if hasattr(landmark_list, 'landmark'):
                    iter_list = landmark_list.landmark
                else:
                    iter_list = landmark_list
                for landmark in iter_list:
                    lm_list.append({
                        "x": float(landmark.x),
                        "y": float(landmark.y),
                        "z": float(landmark.z),
                        "visibility": float(getattr(landmark, 'visibility', 0.0))
                    })
        return lm_list

    def analyze_video(self, video_path: str, max_frames: int = None) -> List[Dict[str, Any]]:
        """Analyze video and return pose landmarks for each frame"""
        # fail fast if MediaPipe was not initialized, with the real reason
        if self.pose is None:
            raise RuntimeError(self._init_error or "MediaPipe Pose is not available; aborting analysis")

        # get the video
        cap = cv2.VideoCapture(video_path)
        if not cap.isOpened():
            raise ValueError(f"Cannot open video: {video_path}")
        
        # init data
        frames_data = []
        frame_count = 0
        

        while cap.isOpened():
            ret, frame = cap.read()
            
            if not ret:
                break
            
            if max_frames and frame_count >= max_frames:
                break
            
            landmarks = []

            # convert numpy array frame to mediapipe Image
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            mp_image = self.mp.Image(image_format=self.mp.ImageFormat.SRGB, data=rgb_frame)
            timestamp = int(cap.get(cv2.CAP_PROP_POS_MSEC))

            detection_result = self.pose.detect_for_video(mp_image, timestamp)
            landmarks = self.extract_landmarks(detection_result)
            
            frame_data = {
                "frame_number": frame_count,
                "timestamp": timestamp,
                "landmarks": landmarks
            }
            
            frames_data.append(frame_data)
            frame_count += 1
        
        cap.release()

        # ensure we actually collected at least one non-empty landmark set
        any_landmarks = any(len(f.get('landmarks', [])) > 0 for f in frames_data)
        if not any_landmarks:
            raise RuntimeError("No pose landmarks were detected in any frame of the video")
        return frames_data



def main():
    if len(sys.argv) < 2:
        print(json.dumps([]))
        sys.exit(1)
    
    video_path = sys.argv[1]
    max_frames = int(sys.argv[2]) if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else None
    
    # determine indentation for output (default 2)
    indent = 2
    # allow user to pass --indent N or --pretty to enable 2-space indent
    for arg in sys.argv[1:]:
        if arg == "--pretty":
            indent = 2
        elif arg.startswith("--indent="):
            try:
                indent = int(arg.split("=",1)[1])
            except ValueError:
                pass
    try:
        analyzer = PoseAnalyzer()
        results = analyzer.analyze_video(video_path, max_frames)
        print(json.dumps(results, indent=indent))
    except Exception as e:
        import traceback
        traceback.print_exc(file=sys.stderr)
        print(json.dumps({"error": str(e)}), file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
