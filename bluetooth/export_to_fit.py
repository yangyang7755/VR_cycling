#!/usr/bin/env python3
"""
Export cycling session data to Garmin .FIT format.
Reads the CSV data logged by Unity and creates a .FIT file
that can be uploaded to Strava, Garmin Connect, TrainingPeaks, etc.

Usage:
    python export_to_fit.py <csv_file>
    python export_to_fit.py data/Control/p1/device_1/data.csv
    
The CSV should have columns: timestamp, speed, power, cadence, heart_rate, distance, elevation
"""

import sys
import csv
import os
from datetime import datetime, timedelta
from fit_tool.fit_file_builder import FitFileBuilder
from fit_tool.profile.messages.file_id_message import FileIdMessage
from fit_tool.profile.messages.device_info_message import DeviceInfoMessage
from fit_tool.profile.messages.event_message import EventMessage
from fit_tool.profile.messages.record_message import RecordMessage
from fit_tool.profile.messages.lap_message import LapMessage
from fit_tool.profile.messages.session_message import SessionMessage
from fit_tool.profile.messages.activity_message import ActivityMessage
from fit_tool.profile.profile_type import FileType, Manufacturer, Sport, SubSport, Event, EventType


def csv_to_fit(csv_path, output_path=None):
    """Convert a cycling CSV to .FIT file."""
    
    if not os.path.exists(csv_path):
        print(f"Error: File not found: {csv_path}")
        return None
    
    if output_path is None:
        output_path = csv_path.rsplit('.', 1)[0] + '.fit'
    
    print(f"Reading: {csv_path}")
    
    # Read CSV data
    records = []
    with open(csv_path, 'r') as f:
        # Skip comment lines
        lines = f.readlines()
        data_lines = [l for l in lines if not l.startswith('#') and l.strip()]
        
        reader = csv.DictReader(data_lines)
        for row in reader:
            try:
                record = {}
                # Try different column name formats
                record['timestamp'] = float(row.get('timestamp', row.get('time', 0)))
                record['speed'] = float(row.get('speed', row.get('speed_ms', 0)))  # m/s
                record['power'] = float(row.get('power', row.get('power_watts', 0)))
                record['cadence'] = float(row.get('cadence', row.get('cadence_rpm', 0)))
                record['heart_rate'] = float(row.get('heartRate', row.get('heart_rate', row.get('hr', 0))))
                record['distance'] = float(row.get('distance', row.get('distance_m', 0)))
                record['elevation'] = float(row.get('elevation', row.get('altitude', 0)))
                record['gradient'] = float(row.get('gradient', row.get('grade', 0)))
                records.append(record)
            except (ValueError, KeyError) as e:
                continue
    
    if not records:
        print("Error: No valid records found in CSV")
        return None
    
    print(f"Found {len(records)} records")
    
    # Calculate session stats
    start_time = datetime.now() - timedelta(seconds=records[-1]['timestamp'] - records[0]['timestamp'])
    total_distance = max(r['distance'] for r in records)
    total_time = records[-1]['timestamp'] - records[0]['timestamp']
    avg_speed = total_distance / max(total_time, 1)
    avg_power = sum(r['power'] for r in records) / max(len(records), 1)
    avg_hr = sum(r['heart_rate'] for r in records if r['heart_rate'] > 0)
    hr_count = sum(1 for r in records if r['heart_rate'] > 0)
    avg_hr = avg_hr / max(hr_count, 1)
    avg_cadence = sum(r['cadence'] for r in records if r['cadence'] > 0)
    cad_count = sum(1 for r in records if r['cadence'] > 0)
    avg_cadence = avg_cadence / max(cad_count, 1)
    max_speed = max(r['speed'] for r in records)
    max_power = max(r['power'] for r in records)
    max_hr = max(r['heart_rate'] for r in records)
    total_ascent = 0
    for i in range(1, len(records)):
        diff = records[i]['elevation'] - records[i-1]['elevation']
        if diff > 0:
            total_ascent += diff
    
    # Build FIT file
    builder = FitFileBuilder()
    
    # File ID
    file_id = FileIdMessage()
    file_id.type = FileType.ACTIVITY
    file_id.manufacturer = Manufacturer.DEVELOPMENT.value
    file_id.product = 1
    file_id.serial_number = 12345
    file_id.time_created = round(start_time.timestamp() * 1000)
    builder.add(file_id)
    
    # Start event
    start_event = EventMessage()
    start_event.event = Event.TIMER
    start_event.event_type = EventType.START
    start_event.timestamp = round(start_time.timestamp() * 1000)
    builder.add(start_event)
    
    # Records (data points)
    for rec in records:
        elapsed = rec['timestamp'] - records[0]['timestamp']
        record_time = start_time + timedelta(seconds=elapsed)
        
        record_msg = RecordMessage()
        record_msg.timestamp = round(record_time.timestamp() * 1000)
        record_msg.distance = round(rec['distance'] * 100)  # in centimeters
        record_msg.speed = round(rec['speed'] * 1000)  # in mm/s
        
        if rec['power'] > 0:
            record_msg.power = round(rec['power'])
        if rec['cadence'] > 0:
            record_msg.cadence = round(rec['cadence'])
        if rec['heart_rate'] > 0:
            record_msg.heart_rate = round(rec['heart_rate'])
        if rec['elevation'] != 0:
            record_msg.altitude = round((rec['elevation'] + 500) * 5)  # FIT altitude encoding
        
        builder.add(record_msg)
    
    # Stop event
    end_time = start_time + timedelta(seconds=total_time)
    stop_event = EventMessage()
    stop_event.event = Event.TIMER
    stop_event.event_type = EventType.STOP_ALL
    stop_event.timestamp = round(end_time.timestamp() * 1000)
    builder.add(stop_event)
    
    # Lap
    lap = LapMessage()
    lap.timestamp = round(end_time.timestamp() * 1000)
    lap.start_time = round(start_time.timestamp() * 1000)
    lap.total_elapsed_time = round(total_time * 1000)
    lap.total_timer_time = round(total_time * 1000)
    lap.total_distance = round(total_distance * 100)
    lap.avg_speed = round(avg_speed * 1000)
    lap.max_speed = round(max_speed * 1000)
    lap.avg_power = round(avg_power)
    lap.max_power = round(max_power)
    if avg_hr > 0:
        lap.avg_heart_rate = round(avg_hr)
        lap.max_heart_rate = round(max_hr)
    if avg_cadence > 0:
        lap.avg_cadence = round(avg_cadence)
    lap.total_ascent = round(total_ascent)
    builder.add(lap)
    
    # Session
    session = SessionMessage()
    session.timestamp = round(end_time.timestamp() * 1000)
    session.start_time = round(start_time.timestamp() * 1000)
    session.total_elapsed_time = round(total_time * 1000)
    session.total_timer_time = round(total_time * 1000)
    session.total_distance = round(total_distance * 100)
    session.sport = Sport.CYCLING
    session.sub_sport = SubSport.VIRTUAL_ACTIVITY
    session.avg_speed = round(avg_speed * 1000)
    session.max_speed = round(max_speed * 1000)
    session.avg_power = round(avg_power)
    session.max_power = round(max_power)
    if avg_hr > 0:
        session.avg_heart_rate = round(avg_hr)
        session.max_heart_rate = round(max_hr)
    if avg_cadence > 0:
        session.avg_cadence = round(avg_cadence)
    session.total_ascent = round(total_ascent)
    builder.add(session)
    
    # Activity
    activity = ActivityMessage()
    activity.timestamp = round(end_time.timestamp() * 1000)
    activity.total_timer_time = round(total_time * 1000)
    activity.num_sessions = 1
    builder.add(activity)
    
    # Write FIT file
    fit_file = builder.build()
    fit_file.to_file(output_path)
    
    print(f"\n{'='*50}")
    print(f"FIT file exported: {output_path}")
    print(f"{'='*50}")
    print(f"Duration: {timedelta(seconds=int(total_time))}")
    print(f"Distance: {total_distance/1000:.2f} km")
    print(f"Avg Speed: {avg_speed*3.6:.1f} km/h")
    print(f"Avg Power: {avg_power:.0f} W")
    print(f"Max Power: {max_power:.0f} W")
    if avg_hr > 0:
        print(f"Avg HR: {avg_hr:.0f} bpm")
        print(f"Max HR: {max_hr:.0f} bpm")
    if avg_cadence > 0:
        print(f"Avg Cadence: {avg_cadence:.0f} rpm")
    print(f"Total Ascent: {total_ascent:.0f} m")
    print(f"{'='*50}")
    print(f"\nUpload to: strava.com/upload or connect.garmin.com")
    
    return output_path


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python export_to_fit.py <csv_file>")
        print("Example: python export_to_fit.py ~/Library/Application\\ Support/DefaultCompany/PAIN_LAB/cycling_data.csv")
        sys.exit(1)
    
    csv_path = sys.argv[1]
    output = sys.argv[2] if len(sys.argv) > 2 else None
    csv_to_fit(csv_path, output)
