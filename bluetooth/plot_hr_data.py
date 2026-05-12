"""
Generate plots for Heart Rate data collected during experiments
"""
import json
import matplotlib.pyplot as plt
import numpy as np
from pathlib import Path
from datetime import datetime
import glob

def load_hr_data(data_path):
    """
    Load HR data from NDJSON files
    
    Args:
        data_path: Path to data directory (e.g., "./../data/Control/p0")
    
    Returns:
        list: List of HR data points with timestamps
    """
    hr_data = []
    data_path = Path(data_path)
    
    # Look for HR monitor data files
    # Pattern: data_path/device_*/data.ndjson
    device_dirs = list(data_path.glob("device_*"))
    
    for device_dir in device_dirs:
        data_file = device_dir / "data.ndjson"
        if data_file.exists():
            print(f"Loading data from: {data_file}")
            file_size = data_file.stat().st_size
            print(f"  File size: {file_size} bytes")
            
            line_count = 0
            valid_hr_count = 0
            with open(data_file, 'r', encoding='utf-8') as f:
                for line in f:
                    line = line.strip()
                    if not line:
                        continue
                    line_count += 1
                    try:
                        record = json.loads(line)
                        # Check if this is HR data
                        if 'heart_rate' in record or 'value' in record:
                            hr_value = record.get('heart_rate', {}).get('value') if isinstance(record.get('heart_rate'), dict) else record.get('heart_rate') or record.get('value')
                            if hr_value and 40 <= hr_value <= 220:  # Valid HR range
                                valid_hr_count += 1
                                timestamp = record.get('time', record.get('timestamp', 0))
                                hr_data.append({
                                    'time': timestamp,
                                    'hr': hr_value,
                                    'ibi': record.get('heart_rate', {}).get('ibi_data') if isinstance(record.get('heart_rate'), dict) else record.get('ibi_data')
                                })
                            else:
                                print(f"  Line {line_count}: Invalid HR value: {hr_value} (record keys: {list(record.keys())[:5]})")
                        else:
                            # Debug: show what keys are in non-HR records
                            if line_count <= 5:  # Only show first 5 non-HR records
                                print(f"  Line {line_count}: Not HR data (keys: {list(record.keys())[:10]})")
                    except json.JSONDecodeError as e:
                        print(f"  Line {line_count}: JSON decode error: {e}")
                        continue
            
            print(f"  Total lines: {line_count}, Valid HR records: {valid_hr_count}")
    
    # Sort by time
    hr_data.sort(key=lambda x: x['time'])
    
    return hr_data

def plot_hr_data(hr_data, output_path):
    """
    Create plots for HR data
    
    Args:
        hr_data: List of HR data points
        output_path: Path to save plots
    """
    if not hr_data:
        print("No HR data found to plot!")
        return
    
    # Extract data
    times = [d['time'] for d in hr_data]
    hrs = [d['hr'] for d in hr_data]
    
    # Convert to relative time (seconds from start)
    start_time = times[0]
    times_rel = [(t - start_time) for t in times]
    
    # Create figure with subplots
    fig = plt.figure(figsize=(16, 10))
    fig.suptitle('Heart Rate Analysis', fontsize=16, fontweight='bold')
    
    # Plot 1: HR over time
    ax1 = plt.subplot(2, 2, 1)
    ax1.plot(times_rel, hrs, 'r-', linewidth=2, alpha=0.7, label='Heart Rate')
    ax1.fill_between(times_rel, hrs, alpha=0.3, color='red')
    ax1.set_xlabel('Time (seconds)', fontsize=12)
    ax1.set_ylabel('Heart Rate (BPM)', fontsize=12)
    ax1.set_title('Heart Rate Over Time', fontsize=14, fontweight='bold')
    ax1.grid(True, alpha=0.3)
    ax1.legend()
    
    # Add statistics
    mean_hr = np.mean(hrs)
    std_hr = np.std(hrs)
    min_hr = np.min(hrs)
    max_hr = np.max(hrs)
    ax1.axhline(y=mean_hr, color='blue', linestyle='--', linewidth=2, label=f'Mean: {mean_hr:.1f} BPM')
    ax1.axhline(y=mean_hr + std_hr, color='green', linestyle='--', linewidth=1, alpha=0.5, label=f'±1 SD: {std_hr:.1f}')
    ax1.axhline(y=mean_hr - std_hr, color='green', linestyle='--', linewidth=1, alpha=0.5)
    ax1.legend()
    
    # Plot 2: HR distribution histogram
    ax2 = plt.subplot(2, 2, 2)
    ax2.hist(hrs, bins=30, color='red', alpha=0.7, edgecolor='black')
    ax2.axvline(x=mean_hr, color='blue', linestyle='--', linewidth=2, label=f'Mean: {mean_hr:.1f} BPM')
    ax2.set_xlabel('Heart Rate (BPM)', fontsize=12)
    ax2.set_ylabel('Frequency', fontsize=12)
    ax2.set_title('Heart Rate Distribution', fontsize=14, fontweight='bold')
    ax2.grid(True, alpha=0.3, axis='y')
    ax2.legend()
    
    # Plot 3: HR statistics
    ax3 = plt.subplot(2, 2, 3)
    ax3.axis('off')
    stats_text = f"""
    Heart Rate Statistics
    
    Duration: {times_rel[-1]:.1f} seconds ({times_rel[-1]/60:.1f} minutes)
    Total Data Points: {len(hr_data)}
    
    Mean HR: {mean_hr:.1f} BPM
    Std Dev: {std_hr:.1f} BPM
    Min HR: {min_hr:.0f} BPM
    Max HR: {max_hr:.0f} BPM
    Range: {max_hr - min_hr:.0f} BPM
    
    Time Above Mean: {sum(1 for h in hrs if h > mean_hr)} points
    Time Below Mean: {sum(1 for h in hrs if h < mean_hr)} points
    """
    ax3.text(0.1, 0.5, stats_text, fontsize=12, family='monospace',
             verticalalignment='center', bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.5))
    
    # Plot 4: HR trend (moving average)
    ax4 = plt.subplot(2, 2, 4)
    window_size = min(30, len(hrs) // 10)  # Adaptive window size
    if window_size > 1:
        moving_avg = np.convolve(hrs, np.ones(window_size)/window_size, mode='valid')
        times_avg = times_rel[window_size-1:]
        ax4.plot(times_rel, hrs, 'r-', linewidth=1, alpha=0.3, label='Raw HR')
        ax4.plot(times_avg, moving_avg, 'b-', linewidth=2, label=f'Moving Average (window={window_size})')
    else:
        ax4.plot(times_rel, hrs, 'r-', linewidth=2, label='Heart Rate')
    ax4.set_xlabel('Time (seconds)', fontsize=12)
    ax4.set_ylabel('Heart Rate (BPM)', fontsize=12)
    ax4.set_title('Heart Rate Trend', fontsize=14, fontweight='bold')
    ax4.grid(True, alpha=0.3)
    ax4.legend()
    
    plt.tight_layout()
    
    # Save plot
    plot_file = Path(output_path) / "hr_analysis.png"
    plot_file.parent.mkdir(parents=True, exist_ok=True)
    plt.savefig(plot_file, dpi=300, bbox_inches='tight')
    print(f"\n[OK] Heart Rate plots saved to: {plot_file}")
    
    # Also save as PDF for better quality
    plot_file_pdf = Path(output_path) / "hr_analysis.pdf"
    plt.savefig(plot_file_pdf, bbox_inches='tight')
    print(f"[OK] Heart Rate plots (PDF) saved to: {plot_file_pdf}")
    
    plt.close()

def main():
    """Main function to generate HR plots"""
    import sys
    
    if len(sys.argv) < 2:
        print("Usage: python plot_hr_data.py <data_path>")
        print("Example: python plot_hr_data.py ./../data/Control/p0")
        return
    
    data_path = sys.argv[1]
    print(f"Loading HR data from: {data_path}")
    
    hr_data = load_hr_data(data_path)
    
    if hr_data:
        print(f"Loaded {len(hr_data)} HR data points")
        plot_hr_data(hr_data, data_path)
    else:
        print("No HR data found!")

if __name__ == "__main__":
    main()
