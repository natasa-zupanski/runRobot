#!/usr/bin/env python3
"""
Pose Landmark Visualization - Plot 2D body landmarks with skeleton connections
"""

import sys
import json
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import numpy as np
from pathlib import Path

# Define skeleton connections (which landmarks connect to form the body)
SKELETON_CONNECTIONS = [
    # Head
    (0, 1), (1, 2), (2, 3),      # Nose to right eye
    (0, 4), (4, 5), (5, 6),      # Nose to left eye
    (3, 7), (6, 8),              # Eyes to ears
    
    # Torso
    (9, 10),                       # Mouth
    (11, 12),                      # Shoulders
    (11, 13), (13, 15),           # Left arm
    (12, 14), (14, 16),           # Right arm
    
    # Hands
    (15, 17), (15, 19), (15, 21), # Left hand fingers
    (16, 18), (16, 20), (16, 22), # Right hand fingers
    
    # Torso to hips
    (11, 23), (12, 24),           # Shoulders to hips
    
    # Legs
    (23, 25), (25, 27), (27, 29), (29, 31), # Left leg
    (24, 26), (26, 28), (28, 30), (30, 32), # Right leg
]

LANDMARK_NAMES = [
    "Nose", "L Eye Inner", "L Eye", "L Eye Outer", "R Eye Inner",
    "R Eye", "R Eye Outer", "L Ear", "R Ear", "L Mouth",
    "R Mouth", "L Shoulder", "R Shoulder", "L Elbow", "R Elbow",
    "L Wrist", "R Wrist", "L Pinky", "R Pinky", "L Index",
    "R Index", "L Thumb", "R Thumb", "L Hip", "R Hip",
    "L Knee", "R Knee", "L Ankle", "R Ankle", "L Heel",
    "R Heel", "L Foot Index", "R Foot Index"
]

def plot_frame(frame_data, frame_num, output_dir=None, show=True):
    """
    Plot 2D landmarks for a single frame
    
    Args:
        frame_data: Dictionary with 'landmarks' list
        frame_num: Frame number for title
        output_dir: Directory to save plot (if None, won't save)
        show: Whether to display the plot
    """
    fig, ax = plt.subplots(figsize=(10, 8))
    
    landmarks = frame_data.get('landmarks', [])
    if not landmarks:
        print(f"No landmarks in frame {frame_num}")
        return
    
    # Extract x, y coordinates and visibility
    x_coords = []
    y_coords = []
    visibility = []
    
    for lm in landmarks:
        x_coords.append(lm['x'])
        y_coords.append(lm['y'])
        visibility.append(lm['visibility'])
    
    x_coords = np.array(x_coords)
    y_coords = np.array(y_coords)
    visibility = np.array(visibility)
    
    # Draw skeleton connections
    for start, end in SKELETON_CONNECTIONS:
        if start < len(x_coords) and end < len(x_coords):
            vis_start = visibility[start]
            vis_end = visibility[end]
            
            # Only draw line if both landmarks have reasonable visibility
            if vis_start > 0.2 and vis_end > 0.2:
                ax.plot([x_coords[start], x_coords[end]], 
                       [y_coords[start], y_coords[end]], 
                       'b-', alpha=0.5, linewidth=1)
    
    # Draw landmarks as circles
    scatter = ax.scatter(x_coords, y_coords, 
                        c=visibility,
                        s=100,
                        cmap='viridis',
                        alpha=0.7,
                        edgecolors='black',
                        linewidth=1)
    
    # Add colorbar for visibility
    cbar = plt.colorbar(scatter, ax=ax)
    cbar.set_label('Visibility Confidence', rotation=270, labelpad=20)
    
    # Labels and formatting
    ax.set_xlabel('X (normalized)')
    ax.set_ylabel('Y (normalized)')
    ax.set_title(f'Body Pose - Frame {frame_num}')
    ax.set_xlim(-0.1, 1.1)
    ax.set_ylim(1.1, -0.1)  # Invert Y axis (image coordinates)
    ax.set_aspect('equal')
    ax.grid(True, alpha=0.3)
    
    # Add landmark labels for high-visibility points
    for i, (x, y, vis) in enumerate(zip(x_coords, y_coords, visibility)):
        if vis > 0.7:  # Only label high-confidence landmarks
            ax.annotate(str(i), (x, y), xytext=(5, 5), 
                       textcoords='offset points', fontsize=8, alpha=0.7)
    
    plt.tight_layout()
    
    if output_dir:
        output_file = Path(output_dir) / f"frame_{frame_num:04d}.png"
        plt.savefig(output_file, dpi=100, bbox_inches='tight')
        print(f"Saved: {output_file}")
    
    if show:
        plt.show()
    
    plt.close()

def plot_all_frames(json_file, output_dir=None, max_frames=None, show_first_only=False):
    """
    Plot all frames from JSON pose data
    
    Args:
        json_file: Path to pose JSON file
        output_dir: Directory to save plots
        max_frames: Limit number of frames to plot
        show_first_only: Only display first frame, save others (disable by default in terminal)
    """
    with open(json_file, 'r') as f:
        data = json.load(f)
    
    if output_dir:
        Path(output_dir).mkdir(parents=True, exist_ok=True)
    
    num_frames = len(data)
    if max_frames:
        num_frames = min(num_frames, max_frames)
    
    print(f"Plotting {num_frames} frames...")
    
    for i, frame in enumerate(data[:num_frames]):
        show = False  # Always false for batch processing
        plot_frame(frame, frame.get('frame_number', i), output_dir, show=show)
        
        if (i + 1) % 10 == 0:
            print(f"  Processed {i + 1}/{num_frames} frames")
    
    print(f"✓ Completed {num_frames} frames")

def main():
    if len(sys.argv) < 2:
        print("Usage: python visualize_pose.py <pose_json_file> [output_dir] [max_frames]")
        print("\nExample:")
        print("  python visualize_pose.py output/test_video_pose.json viz_output 10")
        sys.exit(1)
    
    json_file = sys.argv[1]
    output_dir = sys.argv[2] if len(sys.argv) > 2 else None
    max_frames = int(sys.argv[3]) if len(sys.argv) > 3 else None
    
    if not Path(json_file).exists():
        print(f"Error: File not found: {json_file}")
        sys.exit(1)
    
    plot_all_frames(json_file, output_dir, max_frames, show_first_only=False)

if __name__ == "__main__":
    main()
