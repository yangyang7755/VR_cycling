#!/usr/bin/env python3
"""
Plot cycling data from Unity data logger
Generates comprehensive visualizations of trial data
"""

import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
import sys
import os
from pathlib import Path

def load_data(csv_path):
    """Load cycling data from CSV file"""
    try:
        df = pd.read_csv(csv_path)
        print(f"✓ Loaded {len(df)} data points from {csv_path}")
        return df
    except Exception as e:
        print(f"✗ Error loading data: {e}")
        return None

def load_metadata(csv_path):
    """Load metadata from companion file"""
    metadata_path = csv_path.replace('.csv', '_metadata.txt')
    metadata = {}
    
    if os.path.exists(metadata_path):
        with open(metadata_path, 'r') as f:
            for line in f:
                if ':' in line and not line.startswith('='):
                    key, value = line.split(':', 1)
                    metadata[key.strip()] = value.strip()
    
    return metadata

def plot_time_series(df, metadata, output_path):
    """Create comprehensive time series plots"""
    fig, axes = plt.subplots(6, 1, figsize=(14, 16))
    fig.suptitle(f"Cycling Data - {metadata.get('Participant ID', 'Unknown')} - Trial {metadata.get('Trial Number', '?')}", 
                 fontsize=16, fontweight='bold')
    
    time = df['ElapsedTime']
    
    # 1. Speed
    ax = axes[0]
    ax.plot(time, df['Speed'] * 3.6, 'b-', linewidth=1.5, label='Speed')
    ax.fill_between(time, 0, df['Speed'] * 3.6, alpha=0.3)
    ax.set_ylabel('Speed (km/h)', fontsize=12, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(loc='upper right')
    
    # 2. Power
    ax = axes[1]
    ax.plot(time, df['Power'], 'r-', linewidth=1.5, label='Power')
    ax.fill_between(time, 0, df['Power'], alpha=0.3, color='red')
    ax.set_ylabel('Power (W)', fontsize=12, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(loc='upper right')
    
    # 3. Cadence
    ax = axes[2]
    ax.plot(time, df['Cadence'], 'g-', linewidth=1.5, label='Cadence')
    ax.fill_between(time, 0, df['Cadence'], alpha=0.3, color='green')
    ax.set_ylabel('Cadence (RPM)', fontsize=12, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(loc='upper right')
    
    # 4. Heart Rate
    ax = axes[3]
    # Color code by HR zones
    hr_zones = []
    colors = []
    for hr in df['HeartRate']:
        if hr < 100:
            colors.append('blue')
        elif hr < 140:
            colors.append('green')
        elif hr < 170:
            colors.append('orange')
        else:
            colors.append('red')
    
    ax.plot(time, df['HeartRate'], 'purple', linewidth=1.5, label='Heart Rate')
    ax.fill_between(time, 0, df['HeartRate'], alpha=0.3, color='purple')
    ax.set_ylabel('Heart Rate (BPM)', fontsize=12, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(loc='upper right')
    
    # 5. Gradient
    ax = axes[4]
    # Color code gradient
    gradient_colors = []
    for g in df['Gradient']:
        if g < -1:
            gradient_colors.append('green')
        elif g < 4:
            gradient_colors.append('yellow')
        elif g < 8:
            gradient_colors.append('orange')
        else:
            gradient_colors.append('red')
    
    ax.plot(time, df['Gradient'], 'k-', linewidth=1.5, label='Gradient')
    ax.axhline(y=0, color='gray', linestyle='--', alpha=0.5)
    ax.fill_between(time, 0, df['Gradient'], where=(df['Gradient'] >= 0), 
                     alpha=0.3, color='red', interpolate=True)
    ax.fill_between(time, 0, df['Gradient'], where=(df['Gradient'] < 0), 
                     alpha=0.3, color='green', interpolate=True)
    ax.set_ylabel('Gradient (%)', fontsize=12, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(loc='upper right')
    
    # 6. Elevation
    ax = axes[5]
    ax.plot(time, df['Elevation'], 'brown', linewidth=1.5, label='Elevation')
    ax.fill_between(time, df['Elevation'].min(), df['Elevation'], alpha=0.3, color='brown')
    ax.set_ylabel('Elevation (m)', fontsize=12, fontweight='bold')
    ax.set_xlabel('Time (s)', fontsize=12, fontweight='bold')
    ax.grid(True, alpha=0.3)
    ax.legend(loc='upper right')
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"✓ Saved time series plot: {output_path}")
    plt.close()

def plot_elevation_profile(df, metadata, output_path):
    """Create elevation profile with gradient coloring"""
    fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(14, 8), sharex=True)
    fig.suptitle(f"Elevation Profile - {metadata.get('Participant ID', 'Unknown')} - Trial {metadata.get('Trial Number', '?')}", 
                 fontsize=16, fontweight='bold')
    
    distance = df['Distance']
    
    # Elevation profile
    ax1.plot(distance, df['Elevation'], 'k-', linewidth=2)
    ax1.fill_between(distance, df['Elevation'].min(), df['Elevation'], alpha=0.3, color='brown')
    ax1.set_ylabel('Elevation (m)', fontsize=12, fontweight='bold')
    ax1.grid(True, alpha=0.3)
    ax1.set_title('Elevation Profile', fontsize=14)
    
    # Gradient profile with color coding
    for i in range(len(distance) - 1):
        gradient = df['Gradient'].iloc[i]
        if gradient < -1:
            color = 'green'
        elif gradient < 4:
            color = 'yellow'
        elif gradient < 8:
            color = 'orange'
        else:
            color = 'red'
        
        ax2.plot(distance.iloc[i:i+2], df['Gradient'].iloc[i:i+2], 
                color=color, linewidth=2)
    
    ax2.axhline(y=0, color='gray', linestyle='--', alpha=0.5)
    ax2.set_ylabel('Gradient (%)', fontsize=12, fontweight='bold')
    ax2.set_xlabel('Distance (m)', fontsize=12, fontweight='bold')
    ax2.grid(True, alpha=0.3)
    ax2.set_title('Gradient Profile', fontsize=14)
    
    # Add legend
    from matplotlib.patches import Patch
    legend_elements = [
        Patch(facecolor='green', label='Downhill (< -1%)'),
        Patch(facecolor='yellow', label='Flat (0-4%)'),
        Patch(facecolor='orange', label='Moderate (4-8%)'),
        Patch(facecolor='red', label='Steep (> 8%)')
    ]
    ax2.legend(handles=legend_elements, loc='upper right')
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"✓ Saved elevation profile: {output_path}")
    plt.close()

def plot_power_analysis(df, metadata, output_path):
    """Create power analysis plots"""
    fig, axes = plt.subplots(2, 2, figsize=(14, 10))
    fig.suptitle(f"Power Analysis - {metadata.get('Participant ID', 'Unknown')} - Trial {metadata.get('Trial Number', '?')}", 
                 fontsize=16, fontweight='bold')
    
    # 1. Power vs Speed
    ax = axes[0, 0]
    scatter = ax.scatter(df['Speed'] * 3.6, df['Power'], c=df['Gradient'], 
                        cmap='RdYlGn_r', alpha=0.6, s=20)
    ax.set_xlabel('Speed (km/h)', fontsize=12)
    ax.set_ylabel('Power (W)', fontsize=12)
    ax.set_title('Power vs Speed (colored by gradient)', fontsize=14)
    ax.grid(True, alpha=0.3)
    plt.colorbar(scatter, ax=ax, label='Gradient (%)')
    
    # 2. Power vs Cadence
    ax = axes[0, 1]
    scatter = ax.scatter(df['Cadence'], df['Power'], c=df['Gradient'], 
                        cmap='RdYlGn_r', alpha=0.6, s=20)
    ax.set_xlabel('Cadence (RPM)', fontsize=12)
    ax.set_ylabel('Power (W)', fontsize=12)
    ax.set_title('Power vs Cadence (colored by gradient)', fontsize=14)
    ax.grid(True, alpha=0.3)
    plt.colorbar(scatter, ax=ax, label='Gradient (%)')
    
    # 3. Power vs Gradient
    ax = axes[1, 0]
    ax.scatter(df['Gradient'], df['Power'], alpha=0.6, s=20, c='blue')
    # Add trend line
    z = np.polyfit(df['Gradient'], df['Power'], 1)
    p = np.poly1d(z)
    ax.plot(df['Gradient'], p(df['Gradient']), "r--", linewidth=2, label=f'Trend: y={z[0]:.1f}x+{z[1]:.1f}')
    ax.set_xlabel('Gradient (%)', fontsize=12)
    ax.set_ylabel('Power (W)', fontsize=12)
    ax.set_title('Power vs Gradient', fontsize=14)
    ax.grid(True, alpha=0.3)
    ax.legend()
    
    # 4. Power distribution
    ax = axes[1, 1]
    ax.hist(df['Power'], bins=30, alpha=0.7, color='red', edgecolor='black')
    ax.axvline(df['Power'].mean(), color='blue', linestyle='--', linewidth=2, 
              label=f'Mean: {df["Power"].mean():.1f}W')
    ax.axvline(df['Power'].median(), color='green', linestyle='--', linewidth=2, 
              label=f'Median: {df["Power"].median():.1f}W')
    ax.set_xlabel('Power (W)', fontsize=12)
    ax.set_ylabel('Frequency', fontsize=12)
    ax.set_title('Power Distribution', fontsize=14)
    ax.grid(True, alpha=0.3)
    ax.legend()
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"✓ Saved power analysis: {output_path}")
    plt.close()

def plot_hr_analysis(df, metadata, output_path):
    """Create heart rate analysis plots"""
    fig, axes = plt.subplots(2, 2, figsize=(14, 10))
    fig.suptitle(f"Heart Rate Analysis - {metadata.get('Participant ID', 'Unknown')} - Trial {metadata.get('Trial Number', '?')}", 
                 fontsize=16, fontweight='bold')
    
    # 1. HR vs Power
    ax = axes[0, 0]
    scatter = ax.scatter(df['Power'], df['HeartRate'], c=df['Gradient'], 
                        cmap='RdYlGn_r', alpha=0.6, s=20)
    ax.set_xlabel('Power (W)', fontsize=12)
    ax.set_ylabel('Heart Rate (BPM)', fontsize=12)
    ax.set_title('HR vs Power (colored by gradient)', fontsize=14)
    ax.grid(True, alpha=0.3)
    plt.colorbar(scatter, ax=ax, label='Gradient (%)')
    
    # 2. HR vs Speed
    ax = axes[0, 1]
    ax.scatter(df['Speed'] * 3.6, df['HeartRate'], alpha=0.6, s=20, c='purple')
    ax.set_xlabel('Speed (km/h)', fontsize=12)
    ax.set_ylabel('Heart Rate (BPM)', fontsize=12)
    ax.set_title('HR vs Speed', fontsize=14)
    ax.grid(True, alpha=0.3)
    
    # 3. HR vs Gradient
    ax = axes[1, 0]
    ax.scatter(df['Gradient'], df['HeartRate'], alpha=0.6, s=20, c='purple')
    # Add trend line
    z = np.polyfit(df['Gradient'], df['HeartRate'], 1)
    p = np.poly1d(z)
    ax.plot(df['Gradient'], p(df['Gradient']), "r--", linewidth=2, 
           label=f'Trend: y={z[0]:.1f}x+{z[1]:.1f}')
    ax.set_xlabel('Gradient (%)', fontsize=12)
    ax.set_ylabel('Heart Rate (BPM)', fontsize=12)
    ax.set_title('HR vs Gradient', fontsize=14)
    ax.grid(True, alpha=0.3)
    ax.legend()
    
    # 4. HR zones distribution
    ax = axes[1, 1]
    hr_zones = []
    zone_labels = []
    zone_colors = []
    
    # Define zones
    zones = [
        (0, 100, 'Zone 1: Recovery', 'lightblue'),
        (100, 140, 'Zone 2: Endurance', 'green'),
        (140, 170, 'Zone 3: Tempo', 'orange'),
        (170, 220, 'Zone 4: Threshold', 'red')
    ]
    
    for min_hr, max_hr, label, color in zones:
        count = len(df[(df['HeartRate'] >= min_hr) & (df['HeartRate'] < max_hr)])
        hr_zones.append(count)
        zone_labels.append(label)
        zone_colors.append(color)
    
    ax.bar(range(len(hr_zones)), hr_zones, color=zone_colors, edgecolor='black')
    ax.set_xticks(range(len(zone_labels)))
    ax.set_xticklabels(zone_labels, rotation=45, ha='right')
    ax.set_ylabel('Time (data points)', fontsize=12)
    ax.set_title('Time in HR Zones', fontsize=14)
    ax.grid(True, alpha=0.3, axis='y')
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"✓ Saved HR analysis: {output_path}")
    plt.close()

def generate_summary_report(df, metadata, output_path):
    """Generate text summary report"""
    with open(output_path, 'w') as f:
        f.write("="*60 + "\n")
        f.write("CYCLING DATA ANALYSIS SUMMARY\n")
        f.write("="*60 + "\n\n")
        
        f.write("TRIAL INFORMATION\n")
        f.write("-"*60 + "\n")
        for key, value in metadata.items():
            f.write(f"{key}: {value}\n")
        f.write("\n")
        
        f.write("DATA STATISTICS\n")
        f.write("-"*60 + "\n")
        f.write(f"Duration: {df['ElapsedTime'].max():.1f} seconds\n")
        f.write(f"Total Distance: {df['Distance'].max():.1f} meters\n")
        f.write(f"Data Points: {len(df)}\n\n")
        
        f.write("SPEED\n")
        f.write(f"  Average: {df['Speed'].mean():.2f} m/s ({df['Speed'].mean() * 3.6:.1f} km/h)\n")
        f.write(f"  Maximum: {df['Speed'].max():.2f} m/s ({df['Speed'].max() * 3.6:.1f} km/h)\n")
        f.write(f"  Minimum: {df['Speed'].min():.2f} m/s ({df['Speed'].min() * 3.6:.1f} km/h)\n\n")
        
        f.write("POWER\n")
        f.write(f"  Average: {df['Power'].mean():.1f} W\n")
        f.write(f"  Maximum: {df['Power'].max():.1f} W\n")
        f.write(f"  Minimum: {df['Power'].min():.1f} W\n\n")
        
        f.write("CADENCE\n")
        f.write(f"  Average: {df['Cadence'].mean():.1f} RPM\n")
        f.write(f"  Maximum: {df['Cadence'].max():.1f} RPM\n")
        f.write(f"  Minimum: {df['Cadence'].min():.1f} RPM\n\n")
        
        f.write("HEART RATE\n")
        f.write(f"  Average: {df['HeartRate'].mean():.0f} BPM\n")
        f.write(f"  Maximum: {df['HeartRate'].max():.0f} BPM\n")
        f.write(f"  Minimum: {df['HeartRate'].min():.0f} BPM\n\n")
        
        f.write("GRADIENT\n")
        f.write(f"  Average: {df['Gradient'].mean():.2f}%\n")
        f.write(f"  Maximum: {df['Gradient'].max():.2f}%\n")
        f.write(f"  Minimum: {df['Gradient'].min():.2f}%\n\n")
        
        f.write("ELEVATION\n")
        f.write(f"  Start: {df['Elevation'].iloc[0]:.1f} m\n")
        f.write(f"  End: {df['Elevation'].iloc[-1]:.1f} m\n")
        f.write(f"  Change: {df['Elevation'].iloc[-1] - df['Elevation'].iloc[0]:.1f} m\n")
        f.write(f"  Maximum: {df['Elevation'].max():.1f} m\n")
        f.write(f"  Minimum: {df['Elevation'].min():.1f} m\n")
    
    print(f"✓ Saved summary report: {output_path}")

def main():
    if len(sys.argv) < 2:
        print("Usage: python plot_cycling_data.py <path_to_csv_file>")
        print("Example: python plot_cycling_data.py CyclingData/P001/20260220/Trial_001_143022.csv")
        sys.exit(1)
    
    csv_path = sys.argv[1]
    
    if not os.path.exists(csv_path):
        print(f"✗ File not found: {csv_path}")
        sys.exit(1)
    
    # Load data
    df = load_data(csv_path)
    if df is None:
        sys.exit(1)
    
    # Load metadata
    metadata = load_metadata(csv_path)
    
    # Create output directory
    output_dir = os.path.dirname(csv_path)
    base_name = os.path.basename(csv_path).replace('.csv', '')
    
    print(f"\nGenerating plots...")
    
    # Generate plots
    plot_time_series(df, metadata, os.path.join(output_dir, f"{base_name}_timeseries.png"))
    plot_elevation_profile(df, metadata, os.path.join(output_dir, f"{base_name}_elevation.png"))
    plot_power_analysis(df, metadata, os.path.join(output_dir, f"{base_name}_power.png"))
    plot_hr_analysis(df, metadata, os.path.join(output_dir, f"{base_name}_hr.png"))
    generate_summary_report(df, metadata, os.path.join(output_dir, f"{base_name}_summary.txt"))
    
    print(f"\n{'='*60}")
    print(f"✓ All plots generated successfully!")
    print(f"{'='*60}\n")

if __name__ == "__main__":
    main()
