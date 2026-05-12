"""
Generate comprehensive plots for Bike (FTMS) data including power, cadence, speed, etc.
"""
import json
import matplotlib.pyplot as plt
import numpy as np
from pathlib import Path
from datetime import datetime
import glob

def load_bike_data(data_path):
    """
    Load bike/FTMS data from NDJSON files
    
    Args:
        data_path: Path to data directory (e.g., "./../data/Control/p0")
    
    Returns:
        dict: Dictionary with lists of data points for each metric
    """
    bike_data = {
        'time': [],
        'power': [],
        'cadence': [],
        'speed': [],
        'distance': [],
        'resistance': [],
        'heart_rate': []
    }
    
    data_path = Path(data_path)
    
    # Look for bike device data files
    # Pattern: data_path/device_*/ftms.ndjson or cps.ndjson or cpvs.ndjson
    device_dirs = list(data_path.glob("device_*"))
    
    for device_dir in device_dirs:
        # Try FTMS file first (most common)
        ftms_file = device_dir / "ftms.ndjson"
        cps_file = device_dir / "cps.ndjson"
        cpvs_file = device_dir / "cpvs.ndjson"
        
        files_to_check = [ftms_file, cps_file, cpvs_file]
        
        for data_file in files_to_check:
            if data_file.exists():
                print(f"Loading bike data from: {data_file}")
                with open(data_file, 'r', encoding='utf-8') as f:
                    for line in f:
                        line = line.strip()
                        if not line:
                            continue
                        try:
                            record = json.loads(line)
                            timestamp = record.get('time', record.get('timestamp', 0))
                            
                            # Extract power (try multiple field names)
                            power = (record.get('instant_power') or 
                                    record.get('power') or 
                                    record.get('instantaneous_power') or
                                    record.get('instantaneous_power_watts'))
                            
                            # Extract cadence (try multiple field names)
                            cadence = (record.get('instant_cadence') or 
                                      record.get('cadence') or 
                                      record.get('instantaneous_cadence') or
                                      record.get('cadence_rpm'))
                            
                            # Extract speed (try multiple field names)
                            speed = (record.get('instant_speed') or 
                                    record.get('speed') or 
                                    record.get('instantaneous_speed') or
                                    record.get('speed_kmh'))
                            
                            # Extract distance
                            distance = (record.get('distance_traveled') or 
                                       record.get('distance') or
                                       record.get('total_distance'))
                            
                            # Extract resistance
                            resistance = (record.get('resistance_level') or 
                                         record.get('resistance') or
                                         record.get('target_resistance_level'))
                            
                            # Extract heart rate (if included in bike data)
                            hr = (record.get('heart_rate') or 
                                 record.get('hr') or
                                 (record.get('heart_rate', {}).get('value') if isinstance(record.get('heart_rate'), dict) else None))
                            
                            # Only add if we have at least one valid data point
                            if timestamp and (power is not None or cadence is not None or speed is not None):
                                bike_data['time'].append(timestamp)
                                bike_data['power'].append(power if power is not None else 0)
                                bike_data['cadence'].append(cadence if cadence is not None else 0)
                                bike_data['speed'].append(speed if speed is not None else 0)
                                bike_data['distance'].append(distance if distance is not None else 0)
                                bike_data['resistance'].append(resistance if resistance is not None else 0)
                                bike_data['heart_rate'].append(hr if hr is not None and 40 <= hr <= 220 else None)
                                
                        except json.JSONDecodeError:
                            continue
    
    # Sort by time
    if bike_data['time']:
        sorted_indices = sorted(range(len(bike_data['time'])), key=lambda i: bike_data['time'][i])
        for key in bike_data:
            bike_data[key] = [bike_data[key][i] for i in sorted_indices]
    
    return bike_data

def load_hr_data(data_path):
    """
    Load HR data from NDJSON files (for combined plots)
    
    Args:
        data_path: Path to data directory
    
    Returns:
        list: List of HR data points with timestamps
    """
    hr_data = []
    data_path = Path(data_path)
    
    device_dirs = list(data_path.glob("device_*"))
    
    for device_dir in device_dirs:
        data_file = device_dir / "data.ndjson"
        if data_file.exists():
            with open(data_file, 'r', encoding='utf-8') as f:
                for line in f:
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        record = json.loads(line)
                        if 'heart_rate' in record or 'value' in record:
                            hr_value = record.get('heart_rate', {}).get('value') or record.get('value')
                            if hr_value and 40 <= hr_value <= 220:
                                timestamp = record.get('time', record.get('timestamp', 0))
                                hr_data.append({
                                    'time': timestamp,
                                    'hr': hr_value
                                })
                    except json.JSONDecodeError:
                        continue
    
    hr_data.sort(key=lambda x: x['time'])
    return hr_data

def plot_bike_data(bike_data, hr_data=None, output_path=None):
    """
    Create comprehensive plots for bike data
    
    Args:
        bike_data: Dictionary with bike data arrays
        hr_data: Optional list of HR data points
        output_path: Path to save plots
    """
    if not bike_data['time']:
        print("No bike data found to plot!")
        return
    
    # Convert to relative time (seconds from start)
    start_time = bike_data['time'][0]
    times_rel = [(t - start_time) for t in bike_data['time']]
    
    # Filter out zero/None values for cleaner plots
    power_data = [p if p and p > 0 else None for p in bike_data['power']]
    cadence_data = [c if c and c > 0 else None for c in bike_data['cadence']]
    speed_data = [s if s and s > 0 else None for s in bike_data['speed']]
    
    # Create figure with multiple subplots
    fig = plt.figure(figsize=(20, 14))
    fig.suptitle('Bike Performance Analysis', fontsize=18, fontweight='bold')
    
    # Plot 1: Power over time
    ax1 = plt.subplot(3, 3, 1)
    ax1.plot(times_rel, power_data, 'b-', linewidth=2, alpha=0.7, label='Power')
    ax1.fill_between(times_rel, [p or 0 for p in power_data], alpha=0.3, color='blue')
    ax1.set_xlabel('Time (seconds)', fontsize=11)
    ax1.set_ylabel('Power (Watts)', fontsize=11)
    ax1.set_title('Power Output Over Time', fontsize=13, fontweight='bold')
    ax1.grid(True, alpha=0.3)
    ax1.legend()
    
    # Add power statistics
    valid_power = [p for p in power_data if p is not None and p > 0]
    if valid_power:
        mean_power = np.mean(valid_power)
        ax1.axhline(y=mean_power, color='red', linestyle='--', linewidth=2, label=f'Mean: {mean_power:.1f}W')
        ax1.legend()
    
    # Plot 2: Cadence over time
    ax2 = plt.subplot(3, 3, 2)
    ax2.plot(times_rel, cadence_data, 'g-', linewidth=2, alpha=0.7, label='Cadence')
    ax2.fill_between(times_rel, [c or 0 for c in cadence_data], alpha=0.3, color='green')
    ax2.set_xlabel('Time (seconds)', fontsize=11)
    ax2.set_ylabel('Cadence (RPM)', fontsize=11)
    ax2.set_title('Cadence Over Time', fontsize=13, fontweight='bold')
    ax2.grid(True, alpha=0.3)
    ax2.legend()
    
    # Add cadence statistics
    valid_cadence = [c for c in cadence_data if c is not None and c > 0]
    if valid_cadence:
        mean_cadence = np.mean(valid_cadence)
        ax2.axhline(y=mean_cadence, color='red', linestyle='--', linewidth=2, label=f'Mean: {mean_cadence:.1f}RPM')
        ax2.legend()
    
    # Plot 3: Speed over time
    ax3 = plt.subplot(3, 3, 3)
    ax3.plot(times_rel, speed_data, 'orange', linewidth=2, alpha=0.7, label='Speed')
    ax3.fill_between(times_rel, [s or 0 for s in speed_data], alpha=0.3, color='orange')
    ax3.set_xlabel('Time (seconds)', fontsize=11)
    ax3.set_ylabel('Speed (km/h)', fontsize=11)
    ax3.set_title('Speed Over Time', fontsize=13, fontweight='bold')
    ax3.grid(True, alpha=0.3)
    ax3.legend()
    
    # Add speed statistics
    valid_speed = [s for s in speed_data if s is not None and s > 0]
    if valid_speed:
        mean_speed = np.mean(valid_speed)
        ax3.axhline(y=mean_speed, color='red', linestyle='--', linewidth=2, label=f'Mean: {mean_speed:.1f}km/h')
        ax3.legend()
    
    # Plot 4: Power distribution histogram
    ax4 = plt.subplot(3, 3, 4)
    if valid_power:
        ax4.hist(valid_power, bins=30, color='blue', alpha=0.7, edgecolor='black')
        mean_power = np.mean(valid_power)
        ax4.axvline(x=mean_power, color='red', linestyle='--', linewidth=2, label=f'Mean: {mean_power:.1f}W')
        ax4.set_xlabel('Power (Watts)', fontsize=11)
        ax4.set_ylabel('Frequency', fontsize=11)
        ax4.set_title('Power Distribution', fontsize=13, fontweight='bold')
        ax4.grid(True, alpha=0.3, axis='y')
        ax4.legend()
    
    # Plot 5: Cadence distribution histogram
    ax5 = plt.subplot(3, 3, 5)
    if valid_cadence:
        ax5.hist(valid_cadence, bins=30, color='green', alpha=0.7, edgecolor='black')
        mean_cadence = np.mean(valid_cadence)
        ax5.axvline(x=mean_cadence, color='red', linestyle='--', linewidth=2, label=f'Mean: {mean_cadence:.1f}RPM')
        ax5.set_xlabel('Cadence (RPM)', fontsize=11)
        ax5.set_ylabel('Frequency', fontsize=11)
        ax5.set_title('Cadence Distribution', fontsize=13, fontweight='bold')
        ax5.grid(True, alpha=0.3, axis='y')
        ax5.legend()
    
    # Plot 6: Power vs Cadence scatter
    ax6 = plt.subplot(3, 3, 6)
    if valid_power and valid_cadence:
        # Match power and cadence by time
        power_cadence_pairs = [(p, c) for p, c in zip(power_data, cadence_data) 
                              if p is not None and p > 0 and c is not None and c > 0]
        if power_cadence_pairs:
            powers, cadences = zip(*power_cadence_pairs)
            ax6.scatter(cadences, powers, alpha=0.5, s=10, color='purple')
            ax6.set_xlabel('Cadence (RPM)', fontsize=11)
            ax6.set_ylabel('Power (Watts)', fontsize=11)
            ax6.set_title('Power vs Cadence', fontsize=13, fontweight='bold')
            ax6.grid(True, alpha=0.3)
    
    # Plot 7: Combined Power, Cadence, Speed (normalized)
    ax7 = plt.subplot(3, 3, 7)
    if valid_power:
        max_power = max(valid_power) if valid_power else 1
        normalized_power = [p / max_power if p else None for p in power_data]
        ax7.plot(times_rel, normalized_power, 'b-', linewidth=2, alpha=0.7, label='Power (normalized)')
    
    if valid_cadence:
        max_cadence = max(valid_cadence) if valid_cadence else 1
        normalized_cadence = [c / max_cadence if c else None for c in cadence_data]
        ax7.plot(times_rel, normalized_cadence, 'g-', linewidth=2, alpha=0.7, label='Cadence (normalized)')
    
    if valid_speed:
        max_speed = max(valid_speed) if valid_speed else 1
        normalized_speed = [s / max_speed if s else None for s in speed_data]
        ax7.plot(times_rel, normalized_speed, 'orange', linewidth=2, alpha=0.7, label='Speed (normalized)')
    
    ax7.set_xlabel('Time (seconds)', fontsize=11)
    ax7.set_ylabel('Normalized Value (0-1)', fontsize=11)
    ax7.set_title('Combined Metrics (Normalized)', fontsize=13, fontweight='bold')
    ax7.grid(True, alpha=0.3)
    ax7.legend()
    
    # Plot 8: Heart Rate (if available) or Statistics
    ax8 = plt.subplot(3, 3, 8)
    
    # Try to get HR data from bike data or separate HR data
    hr_times = []
    hr_values = []
    
    if hr_data:
        hr_times = [(d['time'] - start_time) for d in hr_data]
        hr_values = [d['hr'] for d in hr_data]
    else:
        # Try to get HR from bike data
        hr_times = times_rel
        hr_values = [hr if hr else None for hr in bike_data['heart_rate']]
        hr_values = [hr for hr in hr_values if hr is not None]
        if hr_values:
            hr_times = [t for t, hr in zip(times_rel, bike_data['heart_rate']) if hr is not None]
    
    if hr_values:
        ax8.plot(hr_times, hr_values, 'r-', linewidth=2, alpha=0.7, label='Heart Rate')
        ax8.fill_between(hr_times, hr_values, alpha=0.3, color='red')
        ax8.set_xlabel('Time (seconds)', fontsize=11)
        ax8.set_ylabel('Heart Rate (BPM)', fontsize=11)
        ax8.set_title('Heart Rate Over Time', fontsize=13, fontweight='bold')
        ax8.grid(True, alpha=0.3)
        ax8.legend()
        
        mean_hr = np.mean(hr_values)
        ax8.axhline(y=mean_hr, color='blue', linestyle='--', linewidth=2, label=f'Mean: {mean_hr:.1f} BPM')
        ax8.legend()
    else:
        # Show statistics instead
        ax8.axis('off')
        stats_text = f"""
        Bike Performance Statistics
        
        Duration: {times_rel[-1]:.1f}s ({times_rel[-1]/60:.1f} min)
        Data Points: {len(bike_data['time'])}
        
        Power:
          Mean: {np.mean(valid_power):.1f}W
          Max: {np.max(valid_power):.0f}W
          Min: {np.min(valid_power):.0f}W
        
        Cadence:
          Mean: {np.mean(valid_cadence):.1f}RPM
          Max: {np.max(valid_cadence):.0f}RPM
        
        Speed:
          Mean: {np.mean(valid_speed):.1f}km/h
          Max: {np.max(valid_speed):.1f}km/h
        """
        ax8.text(0.1, 0.5, stats_text, fontsize=10, family='monospace',
                 verticalalignment='center', bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.5))
    
    # Plot 9: Distance over time (if available)
    ax9 = plt.subplot(3, 3, 9)
    distance_data = [d if d and d > 0 else None for d in bike_data['distance']]
    valid_distance = [d for d in distance_data if d is not None]
    
    if valid_distance:
        ax9.plot(times_rel, distance_data, 'purple', linewidth=2, alpha=0.7, label='Distance')
        ax9.set_xlabel('Time (seconds)', fontsize=11)
        ax9.set_ylabel('Distance (meters)', fontsize=11)
        ax9.set_title('Distance Traveled', fontsize=13, fontweight='bold')
        ax9.grid(True, alpha=0.3)
        ax9.legend()
    else:
        # Show resistance if available
        resistance_data = [r if r and r > 0 else None for r in bike_data['resistance']]
        valid_resistance = [r for r in resistance_data if r is not None]
        if valid_resistance:
            ax9.plot(times_rel, resistance_data, 'brown', linewidth=2, alpha=0.7, label='Resistance')
            ax9.set_xlabel('Time (seconds)', fontsize=11)
            ax9.set_ylabel('Resistance Level', fontsize=11)
            ax9.set_title('Resistance Over Time', fontsize=13, fontweight='bold')
            ax9.grid(True, alpha=0.3)
            ax9.legend()
        else:
            ax9.axis('off')
            ax9.text(0.5, 0.5, 'No distance or resistance data available', 
                    ha='center', va='center', fontsize=12)
    
    plt.tight_layout()
    
    # Save plot
    if output_path:
        plot_file = Path(output_path) / "bike_analysis.png"
        plot_file.parent.mkdir(parents=True, exist_ok=True)
        plt.savefig(plot_file, dpi=300, bbox_inches='tight')
        print(f"\n[OK] Bike analysis plots saved to: {plot_file}")
        
        # Also save as PDF
        plot_file_pdf = Path(output_path) / "bike_analysis.pdf"
        plt.savefig(plot_file_pdf, bbox_inches='tight')
        print(f"[OK] Bike analysis plots (PDF) saved to: {plot_file_pdf}")
    
    plt.close()

def main():
    """Main function to generate bike plots"""
    import sys
    
    if len(sys.argv) < 2:
        print("Usage: python plot_bike_data.py <data_path>")
        print("Example: python plot_bike_data.py ./../data/Control/p0")
        return
    
    data_path = sys.argv[1]
    print(f"Loading bike data from: {data_path}")
    
    bike_data = load_bike_data(data_path)
    hr_data = load_hr_data(data_path)
    
    if bike_data['time']:
        print(f"Loaded {len(bike_data['time'])} bike data points")
        if hr_data:
            print(f"Loaded {len(hr_data)} HR data points")
        plot_bike_data(bike_data, hr_data, data_path)
    else:
        print("No bike data found!")

if __name__ == "__main__":
    main()
