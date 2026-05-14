"""
Test script to detect hood button presses on the Tacx NEO Bike Plus.
Connects to the bike and listens to ALL BLE characteristics to find
which one reports button/gear data.

Usage:
    cd bluetooth
    source venv/bin/activate
    python test_buttons.py

Instructions:
    1. Run this script
    2. Wait for it to connect to the bike
    3. Press the hood buttons (left up, left down, right up, right down)
    4. Watch the output — look for any characteristic that changes when you press buttons
    5. Press Ctrl+C to stop
"""

import asyncio
import bleak
import time
import struct

# Your NEO Bike Plus
BIKE_NAME = "NEO Bike Plus 26414"
BIKE_ADDRESS = "F9:A4:28:75:B1:08"

# Known FTMS/Tacx UUIDs to monitor
KNOWN_UUIDS = {
    "00002ad2-0000-1000-8000-00805f9b34fb": "FTMS Indoor Bike Data",
    "00002ad9-0000-1000-8000-00805f9b34fb": "FTMS Control Point",
    "00002ada-0000-1000-8000-00805f9b34fb": "FTMS Status",
    "00002a63-0000-1000-8000-00805f9b34fb": "Cycling Power Measurement",
    "00002a64-0000-1000-8000-00805f9b34fb": "Cycling Power Vector",
    "00002a5b-0000-1000-8000-00805f9b34fb": "CSC Measurement",
}

# Track last values to detect changes
last_values = {}


def notification_handler(uuid_str):
    """Create a handler for a specific characteristic UUID"""
    def handler(sender, data):
        hex_data = data.hex()
        
        # Only print if data changed (to reduce noise)
        if uuid_str not in last_values or last_values[uuid_str] != hex_data:
            name = KNOWN_UUIDS.get(uuid_str, f"Unknown ({uuid_str})")
            print(f"\n[{time.strftime('%H:%M:%S')}] {name}")
            print(f"  UUID: {uuid_str}")
            print(f"  Raw hex: {hex_data}")
            print(f"  Raw bytes: {list(data)}")
            print(f"  Length: {len(data)} bytes")
            
            # Try to decode common formats
            if len(data) >= 2:
                print(f"  As uint16 (first 2 bytes): {struct.unpack_from('<H', data, 0)[0]}")
            if len(data) >= 4:
                print(f"  As uint32 (first 4 bytes): {struct.unpack_from('<I', data, 0)[0]}")
            
            last_values[uuid_str] = hex_data
    
    return handler


async def main():
    print("=" * 60)
    print("  NEO Bike Plus Button Detection Test")
    print("=" * 60)
    print(f"  Looking for: {BIKE_NAME}")
    print("=" * 60)
    print()
    
    # Find the bike
    print("Scanning for bike...")
    devices = await bleak.BleakScanner.discover(timeout=10.0)
    
    bike_address = None
    for device in devices:
        if device.name and BIKE_NAME.lower() in device.name.lower():
            bike_address = device.address
            print(f"✓ Found: {device.name} ({device.address})")
            break
    
    if not bike_address:
        # Try by address
        for device in devices:
            if device.address.lower() == BIKE_ADDRESS.lower():
                bike_address = device.address
                print(f"✓ Found by address: {device.name} ({device.address})")
                break
    
    if not bike_address:
        print("✗ Bike not found! Make sure it's powered on and not connected to another app.")
        print(f"  Visible devices: {[d.name for d in devices if d.name]}")
        return
    
    print(f"\nConnecting to {bike_address}...")
    
    async with bleak.BleakClient(bike_address) as client:
        print(f"✓ Connected!")
        print()
        
        # List all services and characteristics
        print("=" * 60)
        print("  ALL BLE Services & Characteristics")
        print("=" * 60)
        
        notify_chars = []
        
        for service in client.services:
            print(f"\nService: {service.uuid} ({service.description or 'Unknown'})")
            for char in service.characteristics:
                props = ", ".join(char.properties)
                print(f"  Char: {char.uuid} [{props}]")
                if char.description:
                    print(f"        Description: {char.description}")
                
                # Subscribe to all notifiable characteristics
                if "notify" in char.properties or "indicate" in char.properties:
                    notify_chars.append(char)
        
        print()
        print("=" * 60)
        print(f"  Subscribing to {len(notify_chars)} notifiable characteristics...")
        print("=" * 60)
        print()
        
        # Subscribe to all
        for char in notify_chars:
            try:
                await client.start_notify(char.uuid, notification_handler(char.uuid))
                name = KNOWN_UUIDS.get(char.uuid, char.description or "Unknown")
                print(f"  ✓ Subscribed: {char.uuid} ({name})")
            except Exception as e:
                print(f"  ✗ Failed: {char.uuid} — {e}")
        
        print()
        print("=" * 60)
        print("  NOW PRESS THE HOOD BUTTONS!")
        print("  Watch for new data appearing below.")
        print("  Press Ctrl+C to stop.")
        print("=" * 60)
        print()
        
        # Keep running and printing notifications
        try:
            while True:
                await asyncio.sleep(0.1)
        except KeyboardInterrupt:
            print("\n\nStopping...")
        
        # Unsubscribe
        for char in notify_chars:
            try:
                await client.stop_notify(char.uuid)
            except:
                pass
    
    print("Done.")


if __name__ == "__main__":
    asyncio.run(main())
