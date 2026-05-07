#!/usr/bin/env python3
"""
Unity Event Marker Receiver (Middleware)

Listens for UDP event markers from Unity and:
1. Prints them to console in real-time
2. Logs them to a CSV file
3. Can forward them to other systems (LSL, serial port, TCP, etc.)

Usage:
    python event_marker_receiver.py

Events received:
    SESSION_START, SESSION_END
    TRIAL_START, TRIAL_END
    CLIMB_START, CLIMB_END
    PAIN_VAS_SHOW, PAIN_VAS_RESPONSE
    REWARD_VAS_SHOW, REWARD_VAS_RESPONSE
    GRADIENT_CHANGE
    CUSTOM

Each event is a JSON packet with:
    id, type, unity_time, timestamp, distance, gradient, data
"""

import socket
import json
import csv
import os
from datetime import datetime

# Configuration
UDP_IP = "127.0.0.1"
UDP_PORT = 9000
LOG_FILE = f"unity_events_{datetime.now().strftime('%Y%m%d_%H%M%S')}.csv"

# Optional: LSL (Lab Streaming Layer) integration
# Uncomment if you have pylsl installed: pip install pylsl
USE_LSL = False
# try:
#     from pylsl import StreamInfo, StreamOutlet
#     USE_LSL = True
# except ImportError:
#     USE_LSL = False


def setup_lsl():
    """Set up LSL outlet for event markers."""
    if not USE_LSL:
        return None
    info = StreamInfo(
        name='UnityEventMarkers',
        type='Markers',
        channel_count=1,
        nominal_srate=0,  # irregular rate
        channel_format='string',
        source_id='unity_cycling_experiment'
    )
    outlet = StreamOutlet(info)
    print(f"[LSL] Stream created: UnityEventMarkers")
    return outlet


def main():
    # Setup UDP socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((UDP_IP, UDP_PORT))
    sock.settimeout(1.0)  # 1 second timeout for clean shutdown
    
    print(f"=" * 60)
    print(f"Unity Event Marker Receiver")
    print(f"=" * 60)
    print(f"Listening on {UDP_IP}:{UDP_PORT}")
    print(f"Log file: {LOG_FILE}")
    print(f"LSL: {'Enabled' if USE_LSL else 'Disabled (install pylsl to enable)'}")
    print(f"Press Ctrl+C to stop")
    print(f"=" * 60)
    print()
    
    # Setup LSL
    lsl_outlet = setup_lsl() if USE_LSL else None
    
    # Setup CSV log
    csv_file = open(LOG_FILE, 'w', newline='')
    csv_writer = csv.writer(csv_file)
    csv_writer.writerow(['event_id', 'type', 'unity_time', 'system_timestamp', 
                         'receive_timestamp', 'distance_m', 'gradient_pct', 'data'])
    
    event_count = 0
    
    try:
        while True:
            try:
                data, addr = sock.recvfrom(4096)
                message = data.decode('utf-8')
                
                try:
                    event = json.loads(message)
                    event_count += 1
                    receive_time = datetime.now().strftime('%Y-%m-%d %H:%M:%S.%f')[:-3]
                    
                    # Print to console with color coding
                    event_type = event.get('type', 'UNKNOWN')
                    color = get_color(event_type)
                    
                    print(f"{color}[{event_count:04d}] {event_type:<25} "
                          f"t={event.get('unity_time', 0):>8.2f}s  "
                          f"d={event.get('distance', 0):>6.0f}m  "
                          f"g={event.get('gradient', 0):>5.1f}%  "
                          f"{event.get('data', '')}\033[0m")
                    
                    # Write to CSV
                    csv_writer.writerow([
                        event.get('id', ''),
                        event_type,
                        event.get('unity_time', ''),
                        event.get('timestamp', ''),
                        receive_time,
                        event.get('distance', ''),
                        event.get('gradient', ''),
                        event.get('data', '')
                    ])
                    csv_file.flush()
                    
                    # Forward to LSL
                    if lsl_outlet:
                        lsl_outlet.push_sample([f"{event_type}:{event.get('data', '')}"])
                    
                except json.JSONDecodeError:
                    print(f"[WARN] Invalid JSON: {message}")
                    
            except socket.timeout:
                continue
                
    except KeyboardInterrupt:
        print(f"\n{'=' * 60}")
        print(f"Receiver stopped. {event_count} events received.")
        print(f"Log saved to: {os.path.abspath(LOG_FILE)}")
        print(f"{'=' * 60}")
    finally:
        csv_file.close()
        sock.close()


def get_color(event_type):
    """ANSI color codes for different event types."""
    colors = {
        'SESSION_START': '\033[96m',    # cyan
        'SESSION_END': '\033[96m',
        'TRIAL_START': '\033[92m',      # green
        'TRIAL_END': '\033[92m',
        'CLIMB_START': '\033[93m',      # yellow
        'CLIMB_END': '\033[93m',
        'PAIN_VAS_SHOW': '\033[91m',    # red
        'PAIN_VAS_RESPONSE': '\033[91m',
        'REWARD_VAS_SHOW': '\033[95m',  # magenta
        'REWARD_VAS_RESPONSE': '\033[95m',
        'GRADIENT_CHANGE': '\033[33m',  # dark yellow
    }
    return colors.get(event_type, '\033[0m')


if __name__ == '__main__':
    main()
