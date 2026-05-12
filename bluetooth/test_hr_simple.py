"""
Simple standalone HR monitor test - connects to HR monitor and publishes to Unity WebSocket
Use this to test HR monitor before integrating with bike
"""
import asyncio
import bleak
import time
import threading
from pathlib import Path
from sensors.generic_ble_handler import GenericBLEDeviceHandler
import actuators.unity.unity_ws_server as unity_ws_server

async def test_hr_monitor_simple():
    """Simple HR monitor test with Unity WebSocket integration"""
    print("=" * 60)
    print("Heart Rate Monitor Test (with Unity WebSocket)")
    print("=" * 60)
    
    # Start WebSocket server in background
    stop_event = threading.Event()
    ws_thread = threading.Thread(
        target=unity_ws_server.start_ws_server,
        args=(stop_event,),
        daemon=True
    )
    ws_thread.start()
    print("WebSocket server starting...")
    time.sleep(2)  # Give server time to start
    
    # Scan for devices
    print("\nScanning for Bluetooth devices...")
    devices = await bleak.BleakScanner.discover(timeout=10.0)
    
    if not devices:
        print("\nNo BLE devices found.")
        stop_event.set()
        return
    
    print(f"\nFound {len(devices)} device(s):\n")
    for i, d in enumerate(devices, 1):
        name = d.name or "Unknown"
        print(f"[{i}] {name}")
        print(f"    Address: {d.address}")
        print()
    
    # Select device
    while True:
        choice = input("Select HR monitor device by number (or ENTER to exit): ").strip()
        if choice == "":
            stop_event.set()
            return
        
        if choice.isdigit():
            idx = int(choice) - 1
            if 0 <= idx < len(devices):
                selected = devices[idx]
                break
        print("Invalid selection.")
    
    print(f"\nSelected: {selected.name or 'Unknown'} ({selected.address})")
    print("\nConnecting and waiting for heart rate data...")
    print("(Press Ctrl+C to stop)\n")
    
    # Create handler
    handler = GenericBLEDeviceHandler("hr_test", selected.name or "HRMonitor", "hr_monitor")
    handler.configure_hr_monitor()
    
    # Track last published HR to avoid spam
    last_published_hr = None
    last_publish_time = 0
    
    async def hr_data_callback(sender, data):
        """Callback when HR data is received"""
        nonlocal last_published_hr, last_publish_time
        
        # Parse HR data (same as in generic_ble_handler)
        flags = data[0]
        if flags & 0x01:  # 16-bit value
            hr_value = int.from_bytes(data[1:3], byteorder='little')
        else:  # 8-bit value
            hr_value = data[1]
        
        # Only publish if changed or every 2 seconds
        current_time = time.time()
        if hr_value != last_published_hr or (current_time - last_publish_time) >= 2.0:
            unity_ws_server.publish_heart_rate_bpm(hr_value)
            print(f"HR: {hr_value} BPM")
            last_published_hr = hr_value
            last_publish_time = current_time
    
    # Connect and enable notifications
    client = bleak.BleakClient(selected.address)
    try:
        await client.connect()
        print(f"✓ Connected to {selected.address}")
        
        # Find Heart Rate Service
        services = await client.get_services()
        hr_service = None
        hr_char = None
        
        for service in services:
            if service.uuid.lower() == "0000180d-0000-1000-8000-00805f9b34fb":
                hr_service = service
                for char in service.characteristics:
                    if char.uuid.lower() == "00002a37-0000-1000-8000-00805f9b34fb":
                        hr_char = char
                        break
                break
        
        if not hr_char:
            print("ERROR: Heart Rate Service not found on this device!")
            print("This device may not be a standard HR monitor.")
            stop_event.set()
            return
        
        # Enable notifications
        await client.start_notify(hr_char.uuid, hr_data_callback)
        print("✓ Heart Rate notifications enabled")
        print("\nWaiting for heart rate data...")
        print("Unity can now connect to ws://localhost:8765 to receive HR data\n")
        
        # Keep running until stopped
        try:
            while not stop_event.is_set():
                await asyncio.sleep(1)
        except KeyboardInterrupt:
            print("\n\nStopping...")
        
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()
    finally:
        try:
            await client.stop_notify(hr_char.uuid)
            await client.disconnect()
        except:
            pass
        stop_event.set()
        print("Disconnected. WebSocket server stopped.")

if __name__ == "__main__":
    try:
        asyncio.run(test_hr_monitor_simple())
    except KeyboardInterrupt:
        print("\nTest interrupted.")
    except Exception as e:
        print(f"\nError: {e}")
        import traceback
        traceback.print_exc()
