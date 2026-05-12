"""
Simple test script for Heart Rate Monitor connection
Tests HR monitor before integrating with bike system
"""
import asyncio
import bleak
import time
import threading
from sensors.generic_ble_handler import GenericBLEDeviceHandler, run_generic_device_handler

async def test_hr_monitor():
    """Test HR monitor connection and data reception"""
    print("=" * 60)
    print("Heart Rate Monitor Test")
    print("=" * 60)
    print("\nScanning for Bluetooth devices...")
    
    # Scan for devices
    devices = await bleak.BleakScanner.discover(timeout=10.0)
    
    if not devices:
        print("\nNo BLE devices found. Make sure HR monitor is turned on and discoverable.")
        return
    
    # Filter for HR monitors (devices with "HR" or "Heart" in name, or show all)
    print(f"\nFound {len(devices)} device(s):\n")
    hr_candidates = []
    
    for i, d in enumerate(devices, 1):
        name = d.name or "Unknown"
        print(f"[{i}] {name}")
        print(f"    Address: {d.address}")
        print()
        
        # Check if name suggests HR monitor
        if any(keyword in name.lower() for keyword in ['hr', 'heart', 'polar', 'garmin', 'wahoo', 'fitbit']):
            hr_candidates.append((i, d))
    
    if not hr_candidates:
        print("No obvious HR monitor devices found, but you can select any device to test.")
        print("Standard HR monitors use the Heart Rate Service (UUID: 0x180D)")
    
    # Let user select device
    while True:
        choice = input("Select device by number (or ENTER to exit): ").strip()
        if choice == "":
            return
        
        if not choice.isdigit():
            print("Please enter a number.")
            continue
        
        idx = int(choice) - 1
        if 0 <= idx < len(devices):
            selected = devices[idx]
            print(f"\nSelected: {selected.name or 'Unknown'} ({selected.address})")
            break
        else:
            print("Invalid selection.")
    
    # Test connection
    print("\n" + "=" * 60)
    print("Testing HR Monitor Connection...")
    print("=" * 60)
    
    stop_event = threading.Event()
    data_queue = asyncio.Queue()
    
    # Create handler
    handler = GenericBLEDeviceHandler("hr_test", selected.name or "HRMonitor", "hr_monitor")
    handler.configure_hr_monitor()
    
    # Create output file
    from pathlib import Path
    output_path = "./hr_test_output"
    Path(output_path).mkdir(parents=True, exist_ok=True)
    
    print(f"\nConnecting to {selected.address}...")
    print("Waiting for heart rate data...")
    print("(Press Ctrl+C to stop)\n")
    
    try:
        # Run device handler in separate thread
        device_thread = threading.Thread(
            target=run_generic_device_handler,
            args=(stop_event, "hr_test", selected.name or "HRMonitor", "hr_monitor",
                  selected.address, output_path, None),
            daemon=True
        )
        device_thread.start()
        
        # Monitor data for 30 seconds
        start_time = time.time()
        last_hr = None
        hr_count = 0
        
        while time.time() - start_time < 30:
            # Check if handler has data (simplified - in real implementation, use queue)
            await asyncio.sleep(1)
            
            # In a real implementation, you'd read from the data queue
            # For now, just wait and let the handler print data
            
        print("\n" + "=" * 60)
        print("Test complete!")
        print(f"Data saved to: {output_path}")
        print("=" * 60)
        
    except KeyboardInterrupt:
        print("\n\nStopping test...")
    finally:
        stop_event.set()
        device_thread.join(timeout=2)

if __name__ == "__main__":
    try:
        asyncio.run(test_hr_monitor())
    except KeyboardInterrupt:
        print("\nTest interrupted by user.")
    except Exception as e:
        print(f"\nError: {e}")
        import traceback
        traceback.print_exc()
