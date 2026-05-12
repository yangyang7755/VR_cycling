# External modules
from datetime import datetime
import asyncio
import bleak as bleak
import time
import threading
import queue
import json
from pathlib import Path

# Internal modules
import sensors.ergometer.ftms_cps_data_handler as ergometer_data_handler
import sensors.ergometer.multi_device_handler as multi_device_handler
import sensors.ergometer.torque.torque_calculator as torque_calculator
import sensors.generic_ble_handler as generic_ble_handler
import actuators.unity.unity_ws_server as unity_ws_server


# Debug logging helper
def debug_log(location, message, data=None, hypothesis_id=None):
    """Write debug log to file"""
    try:
        log_path = Path(".cursor/debug.log")
        log_path.parent.mkdir(parents=True, exist_ok=True)
        log_entry = {
            "timestamp": time.time(),
            "location": location,
            "message": message,
            "data": data or {},
            "sessionId": "hr-test-session",
            "hypothesisId": hypothesis_id or "A"
        }
        with open(log_path, "a", encoding="utf-8") as f:
            f.write(json.dumps(log_entry) + "\n")
    except Exception:
        pass  # Silently fail if logging fails

async def aggregator():
    print("Starting experiment...\n ")
    time.sleep(2)

    protocol = ""
    participant_number = 0
    # Duration can be set here, or use 0 for manual stop
    print("\nData Collection Duration:")
    print("  - Enter number of seconds for automatic stop")
    print("  - Enter 0 to run until you type 'stop' or 'end'")
    duration_input = input("Enter duration (default: 0 for manual stop): ").strip()
    duration = int(duration_input) if duration_input.isdigit() else 0

    # Participant group/protocol selection
    protocol, participant_number = group_selection() # Get specified group for different stimuli protocols
    
    debug_log("aggregator.py:aggregator", "Experiment started", {
        "protocol": protocol,
        "participant_number": participant_number
    }, "A")
    
    # Ask user what type of devices to connect
    print("\nWhat type of devices do you want to connect?")
    print("1: Bike only (ergometer)")
    print("2: Heart Rate Monitor only")
    print("3: Both Bike and Heart Rate Monitor")
    print("4: Multiple bikes (original multi-device mode)")
    device_type_choice = input("Enter choice (1-4, default: 3): ").strip() or "3"
    
    debug_log("aggregator.py:aggregator", "Device type choice received", {
        "choice": device_type_choice
    }, "A")
    
    selected_devices = []
    device_types = []  # Track device type for each device: 'bike' or 'hr_monitor'
    
    # Known bike trainer device
    known_bike_address = "F9:A4:28:75:B1:08"  # NEO Bike Plus 26414
    known_bike_name = "NEO Bike Plus 26414"
    
    if device_type_choice == "1":
        # Bike only
        print("\nSelecting bike device...")
        print(f"Looking for known bike trainer: {known_bike_name} ({known_bike_address})...")
        bike_address = await ble_device_selection_with_preference(known_bike_address, known_bike_name)
        selected_devices.append({
            'address': bike_address,
            'name': 'Bike',
            'type': 'bike'
        })
        device_types.append('bike')
    elif device_type_choice == "2":
        # HR Monitor only
        print("\nSelecting Heart Rate Monitor...")
        debug_log("aggregator.py:aggregator", "Starting HR monitor selection", {}, "A")
        # Auto-connect to known HRM Pro device if available
        known_hr_address = "CD:B6:56:43:2C:8B"  # HRMPro+:629521
        known_hr_name = "HRMPro+:629521"
        
        print(f"Looking for known HR monitor: {known_hr_name} ({known_hr_address})...")
        hr_address = await ble_device_selection_with_preference(known_hr_address, known_hr_name)
        selected_devices.append({
            'address': hr_address,
            'name': 'HRMonitor',
            'type': 'hr_monitor'
        })
        device_types.append('hr_monitor')
        debug_log("aggregator.py:aggregator", "HR monitor selected", {
            "address": hr_address
        }, "A")
    elif device_type_choice == "3":
        # Both Bike and HR Monitor
        print("\nFirst, select your bike device...")
        print(f"Looking for known bike trainer: {known_bike_name} ({known_bike_address})...")
        bike_address = await ble_device_selection_with_preference(known_bike_address, known_bike_name)
        selected_devices.append({
            'address': bike_address,
            'name': 'Bike',
            'type': 'bike'
        })
        device_types.append('bike')
        
        print("\nNow, select your Heart Rate Monitor...")
        # Auto-connect to known HRM Pro device if available
        known_hr_address = "CD:B6:56:43:2C:8B"  # HRMPro+:629521
        known_hr_name = "HRMPro+:629521"
        print(f"Looking for known HR monitor: {known_hr_name} ({known_hr_address})...")
        hr_address = await ble_device_selection_with_preference(known_hr_address, known_hr_name)
        selected_devices.append({
            'address': hr_address,
            'name': 'HRMonitor',
            'type': 'hr_monitor'
        })
        device_types.append('hr_monitor')
    elif device_type_choice == "4":
        # Multiple bikes (original mode)
        num_devices_input = input("How many bike devices? (default: 3): ").strip()
        num_devices = int(num_devices_input) if num_devices_input.isdigit() else 3
        print(f"\nYou will be asked to select {num_devices} bike devices.")
        bike_devices = await multi_ble_device_selection(num_devices)
        for device in bike_devices:
            device['type'] = 'bike'
            selected_devices.append(device)
            device_types.append('bike')
    else:
        print("Invalid choice. Defaulting to both Bike and HR Monitor.")
        # Default to both
        print("\nFirst, select your bike device...")
        print(f"Looking for known bike trainer: {known_bike_name} ({known_bike_address})...")
        bike_address = await ble_device_selection_with_preference(known_bike_address, known_bike_name)
        selected_devices.append({
            'address': bike_address,
            'name': 'Bike',
            'type': 'bike'
        })
        device_types.append('bike')
        
        print("\nNow, select your Heart Rate Monitor...")
        # Auto-connect to known HRM Pro device if available
        known_hr_address = "CD:B6:56:43:2C:8B"  # HRMPro+:629521
        known_hr_name = "HRMPro+:629521"
        print(f"Looking for known HR monitor: {known_hr_name} ({known_hr_address})...")
        hr_address = await ble_device_selection_with_preference(known_hr_address, known_hr_name)
        selected_devices.append({
            'address': hr_address,
            'name': 'HRMonitor',
            'type': 'hr_monitor'
        })
        device_types.append('hr_monitor')

    OUTPUTFILE = "./../data/" + protocol + "/p" + str(participant_number)

    # Threading variables and queues
    stop_event = threading.Event()
    recording_enabled = threading.Event()  # Flag to control when data recording starts
    device_threads = []
    device_queues = []
    
    # Create queues and threads for each device
    bike_queues = []  # Separate list for bike device queues (needed for torque calculation)
    resistance_queue = queue.Queue(maxsize=10)  # Queue for resistance control commands from Unity (defined early for use in device threads)
    
    for i, device_info in enumerate(selected_devices, 1):
        device_id = f"device_{i}"
        device_name = device_info['name']
        device_address = device_info['address']
        device_type = device_info.get('type', 'bike')  # Default to bike for backward compatibility
        
        # Each device gets its own data queue
        device_queue = queue.Queue(maxsize=2000)
        device_queues.append(device_queue)
        
        # Track bike queues separately for torque calculation
        if device_type == 'bike':
            bike_queues.append(device_queue)
        
        # Create thread based on device type
        if device_type == 'bike':
            # Use bike handler for bike devices
            debug_log("aggregator.py:aggregator", "Creating bike device thread", {
                "device_id": device_id,
                "device_name": device_name,
                "address": device_address
            }, "A")
            device_thread = threading.Thread(
                target=multi_device_handler.run_device_handler,
                args=(stop_event, device_id, device_name, device_address, 
                      OUTPUTFILE, device_queue, resistance_queue, recording_enabled),
                daemon=False
            )
        elif device_type == 'hr_monitor':
            # Use generic handler for HR monitor
            debug_log("aggregator.py:aggregator", "Creating HR monitor device thread", {
                "device_id": device_id,
                "device_name": device_name,
                "address": device_address
            }, "A")
            # #region agent log
            try:
                log_path = Path(".cursor/debug.log")
                log_path.parent.mkdir(parents=True, exist_ok=True)
                log_entry = {
                    "timestamp": time.time(),
                    "location": "aggregator.py:aggregator",
                    "message": "Calling run_generic_device_handler with arguments",
                    "data": {
                        "num_args": 11,
                        "arg11_type": str(type(recording_enabled)),
                        "arg11_is_event": isinstance(recording_enabled, threading.Event),
                        "expected_init_commands_pos": 11,
                        "expected_recording_enabled_pos": 12
                    },
                    "runId": "debug-run1",
                    "hypothesisId": "A"
                }
                with open(log_path, "a", encoding="utf-8") as f:
                    f.write(json.dumps(log_entry) + "\n")
            except Exception:
                pass
            # #endregion
            device_thread = threading.Thread(
                target=generic_ble_handler.run_generic_device_handler,
                args=(stop_event, device_id, device_name, 'hr_monitor', device_address, 
                      OUTPUTFILE, device_queue, None, None, None, None, recording_enabled),
                daemon=False
            )
        else:
            print(f"Warning: Unknown device type '{device_type}', using bike handler")
            device_thread = threading.Thread(
                target=multi_device_handler.run_device_handler,
                args=(stop_event, device_id, device_name, device_address, 
                      OUTPUTFILE, device_queue, resistance_queue, recording_enabled),
                daemon=False
            )
        
        device_threads.append(device_thread)
    
    # For torque calculation, use first bike device's queue (if available)
    # If no bike devices, create empty queue (torque thread will handle gracefully)
    torque_data_queue = bike_queues[0] if bike_queues else queue.Queue(maxsize=2000)
    
    # Set resistance queue in WebSocket server so it can receive commands from Unity
    unity_ws_server.set_resistance_queue(resistance_queue)
    
    # Only start torque thread if we have bike devices
    # (Torque calculation requires bike data to function)
    torque_thread = None
    
    debug_log("aggregator.py:aggregator", "Checking bike devices for torque thread", {
        "bike_queues_count": len(bike_queues),
        "total_devices": len(selected_devices)
    }, "A")
    
    if bike_queues:
        debug_log("aggregator.py:aggregator", "Starting torque thread", {}, "A")
        torque_thread = threading.Thread(
            target=torque_calculator.run_torque_estimator,
            args=(stop_event, torque_data_queue, None),
            daemon=False
        )
    else:
        print("\nNote: No bike devices connected. Torque calculation will be skipped.")
        debug_log("aggregator.py:aggregator", "Skipping torque thread (no bike devices)", {}, "A")
    unity_ws_server_thread = threading.Thread(
        target=unity_ws_server.start_ws_server,
        args=(stop_event,),
        daemon=True
    )

    start_time = datetime.now()

    print(f"\nStarting WebSocket server...")
    unity_ws_server_thread.start()
    time.sleep(1)  # Give WebSocket server time to start
    
    print(f"\nStarting data transfer in 5 seconds...")
    print(f"Connecting to {len(selected_devices)} device(s)...")
    time.sleep(5)

    # Start all device threads
    debug_log("aggregator.py:aggregator", "Starting device threads", {
        "device_threads_count": len(device_threads)
    }, "A")
    for thread in device_threads:
        thread.start()
    
    # Start other threads (only if bike devices are connected)
    if torque_thread is not None:
        torque_thread.start()
    
    debug_log("aggregator.py:aggregator", "All threads started", {
        "torque_thread": torque_thread is not None,
        "ws_server_thread": True
    }, "A")

    # Wait for devices to connect and stabilize
    print(f"\nWaiting for devices to connect and stabilize...")
    time.sleep(3)
    
    # Check if we have both bike and HR monitor - if so, wait for pedaling
    has_bike = any(d.get('type') == 'bike' for d in selected_devices)
    has_hr = any(d.get('type') == 'hr_monitor' for d in selected_devices)
    
    if has_bike and has_hr:
        print(f"\n{'='*60}")
        print("✓ Both devices connected!")
        print(f"{'='*60}")
        print("\n⚠ IMPORTANT: Please start cycling now!")
        print("Data recording will begin automatically once pedaling is detected.")
        print("(Looking for cadence > 0 or power > 0)")
        print(f"{'='*60}\n")
        
        # Monitor bike queues for pedaling detection
        pedaling_detected = False
        max_wait_time = 300  # Wait up to 5 minutes for pedaling
        check_interval = 0.5  # Check every 0.5 seconds
        start_wait_time = time.time()
        
        while not pedaling_detected and not stop_event.is_set():
            # Check if we've waited too long
            if time.time() - start_wait_time > max_wait_time:
                print("\n⚠ Warning: Pedaling not detected after 5 minutes.")
                print("Starting data recording anyway...")
                pedaling_detected = True
                break
            
            # Check bike queues for pedaling indicators
            for bike_queue in bike_queues:
                try:
                    # Peek at queue without removing items
                    if not bike_queue.empty():
                        # Get a sample from queue (non-blocking)
                        sample_data = bike_queue.get_nowait()
                        
                        # Check for pedaling indicators
                        cadence = sample_data.get("instant_cadence") or sample_data.get("cadence") or sample_data.get("instantaneous_cadence")
                        power = sample_data.get("instant_power") or sample_data.get("power") or sample_data.get("instantaneous_power")
                        
                        if (cadence is not None and cadence > 0) or (power is not None and power > 0):
                            pedaling_detected = True
                            print(f"\n{'='*60}")
                            print("✓✓✓ PEDALING DETECTED! ✓✓✓")
                            if cadence and cadence > 0:
                                print(f"   Cadence: {cadence} RPM")
                            if power and power > 0:
                                print(f"   Power: {power}W")
                            print("✓ Data recording started!")
                            print(f"{'='*60}\n")
                            break
                        
                        # Put data back in queue (we were just checking)
                        try:
                            bike_queue.put_nowait(sample_data)
                        except queue.Full:
                            pass  # Queue is full, that's okay
                            
                except queue.Empty:
                    pass
                except Exception as e:
                    debug_log("aggregator.py:aggregator", "Error checking pedaling", {
                        "error": str(e)
                    }, "PEDAL")
            
            if not pedaling_detected:
                time.sleep(check_interval)
        
        # Enable recording once pedaling is detected
        if pedaling_detected:
            recording_enabled.set()
            debug_log("aggregator.py:aggregator", "Recording enabled - pedaling detected", {
                "pedaling_detected": True
            }, "PEDAL")
    else:
        # If not both devices, start recording immediately
        print(f"\n{'='*60}")
        print("Devices connected. Starting data collection...")
        print(f"{'='*60}\n")
        recording_enabled.set()
        debug_log("aggregator.py:aggregator", "Recording enabled immediately (not both devices)", {
            "has_bike": has_bike,
            "has_hr": has_hr
        }, "PEDAL")

    # Run until user prompts to stop
    print(f"\n{'='*60}")
    print("Data collection is now running...")
    print("Type 'stop' or 'end' and press ENTER to stop data collection")
    if duration > 0:
        print(f"(Or wait {duration} seconds for automatic stop)")
    print(f"{'='*60}\n")
    
    # Start a thread to monitor user input
    user_input_queue = queue.Queue()
    def input_monitor():
        while not stop_event.is_set():
            try:
                user_input = input().strip().lower()
                if user_input in ['stop', 'end', 'quit', 'exit']:
                    user_input_queue.put('stop')
                    break
            except (EOFError, KeyboardInterrupt):
                break
    
    input_thread = threading.Thread(target=input_monitor, daemon=True)
    input_thread.start()
    
    # Wait for either duration timeout or user input
    start_time = time.time()
    try:
        while not stop_event.is_set():
            # Check for user input
            try:
                if not user_input_queue.empty():
                    user_input_queue.get_nowait()
                    print("\n\nStopping data collection (user requested)...")
                    stop_event.set()
                    break
            except queue.Empty:
                pass
            
            # Check for duration timeout
            if duration > 0 and (time.time() - start_time) >= duration:
                print(f"\n\nStopping data collection (duration reached: {duration} seconds)...")
                stop_event.set()
                break
            
            time.sleep(0.5)  # Check every 0.5 seconds
    except KeyboardInterrupt:
        print("\n\nStopping data collection (Ctrl+C)...")
        stop_event.set()

    # Stopping threads
    print("\nStopping all threads...")
    stop_event.set()  # Signal threads to stop
    
    # Wait for all device threads
    for thread in device_threads:
        thread.join()
    
    # Wait for other threads (only if they were started)
    if torque_thread is not None:
        torque_thread.join()
    unity_ws_server_thread.join()
    
    # Signal end of data (only if queues were used)
    if bike_queues:
        torque_data_queue.put(None)
    
    print("\nAll threads stopped. Data collection complete.")
    print(f"Data saved to: {OUTPUTFILE}")
    
    # Generate plots for collected data
    print("\nGenerating analysis plots...")
    import subprocess
    import sys
    
    # Generate bike/FTMS plots if bike was used
    has_bike = any(d.get('type') == 'bike' for d in selected_devices)
    if has_bike:
        print("\nGenerating Bike/FTMS analysis plots...")
        try:
            plot_bike_script = Path(__file__).parent / "plot_bike_data.py"
            if plot_bike_script.exists():
                result = subprocess.run(
                    [sys.executable, str(plot_bike_script), OUTPUTFILE],
                    capture_output=True,
                    text=True,
                    timeout=60
                )
                if result.returncode == 0:
                    print(result.stdout)
                else:
                    print(f"Warning: Bike plot generation had issues: {result.stderr}")
            else:
                print("Warning: plot_bike_data.py not found, skipping bike plot generation")
        except Exception as e:
            print(f"Warning: Could not generate bike plots: {e}")
            print("You can manually generate plots by running:")
            print(f"  python plot_bike_data.py {OUTPUTFILE}")
    
    # Generate HR plots if HR monitor was used
    has_hr = any(d.get('type') == 'hr_monitor' for d in selected_devices)
    if has_hr:
        print("\nGenerating Heart Rate analysis plots...")
        try:
            plot_hr_script = Path(__file__).parent / "plot_hr_data.py"
            if plot_hr_script.exists():
                result = subprocess.run(
                    [sys.executable, str(plot_hr_script), OUTPUTFILE],
                    capture_output=True,
                    text=True,
                    timeout=30
                )
                if result.returncode == 0:
                    print(result.stdout)
                else:
                    print(f"Warning: HR plot generation had issues: {result.stderr}")
            else:
                print("Warning: plot_hr_data.py not found, skipping HR plot generation")
        except Exception as e:
            print(f"Warning: Could not generate HR plots: {e}")
            print("You can manually generate plots by running:")
            print(f"  python plot_hr_data.py {OUTPUTFILE}")

    return 0

def group_selection():
    participant_group = 'NA'

    print("Which group is your participant in: ")
    print("1: Control group\n"
          "2: Pain group\n")
    choice = input("Input the group number (\"1\" or \"2\"): ")

    if choice == '1': 
        participant_group = 'Control'
    elif choice == '2': 
        participant_group = 'Pain'
    else:
        print("Invalid choice. Defaulting to Control group.")
        participant_group = 'Control'

    participant_number = input("Enter a participant number: ")

    return participant_group, participant_number

async def ble_device_selection_with_preference(preferred_address: str = None, preferred_name: str = None):
    """
    Scan for BLE devices with preference for a known device.
    If preferred device is found, auto-select it. Otherwise, let user choose.
    Checks by name first (more reliable across platforms), then by address.

    Args:
        preferred_address: Preferred device address (e.g. "CD:B6:56:43:2C:8B" or UUID on macOS)
        preferred_name: Preferred device name (e.g. "HRMPro+:629521")

    Returns:
        str: Selected device address (e.g. "AA:BB:CC:DD:EE:FF" or UUID on macOS)
    """
    while True:
        print("Scanning for Bluetooth LE devices...")
        devices = await bleak.BleakScanner.discover(timeout=5.0)
        
        # Check if preferred device is found by name first (more reliable)
        if preferred_name:
            preferred_name_lower = preferred_name.lower()
            for device in devices:
                device_name = device.name or ""
                if device_name.lower() == preferred_name_lower:
                    print(f"\n✓ Found preferred device by name: {device.name} ({device.address})")
                    print("Auto-selecting preferred device...")
                    return device.address
        
        # Fallback: Check by address if name didn't match
        if preferred_address:
            preferred_address_lower = preferred_address.lower()
            for device in devices:
                if device.address.lower() == preferred_address_lower:
                    print(f"\n✓ Found preferred device by address: {device.name or preferred_name} ({device.address})")
                    print("Auto-selecting preferred device...")
                    return device.address

        if not devices:
            print("\nNo BLE devices found: Make sure the device is turned on and discoverable")
            if preferred_name:
                print(f"Looking for: {preferred_name}")
            elif preferred_address:
                print(f"Looking for: {preferred_address}")
            time.sleep(2)
            input("Press ENTER to rescan when ready...")
            continue   # restart scanning

        # Check if preferred device was found (for display purposes)
        preferred_found = False
        if preferred_name or preferred_address:
            for device in devices:
                name_match = preferred_name and (device.name or "").lower() == preferred_name.lower()
                addr_match = preferred_address and device.address.lower() == preferred_address.lower()
                if name_match or addr_match:
                    preferred_found = True
                    break

        # Devices found — list them
        print("\nThe following Bluetooth devices were found:")
        time.sleep(1)
        print(f"\nFound {len(devices)} device(s):\n")
        for i, d in enumerate(devices, 1):
            name = d.name or "Unknown"
            # Check if this is the preferred device
            is_preferred = False
            if preferred_name:
                is_preferred = (d.name or "").lower() == preferred_name.lower()
            if not is_preferred and preferred_address:
                is_preferred = d.address.lower() == preferred_address.lower()
            
            marker = " ⭐ PREFERRED" if is_preferred else ""
            print(f"[{i}] {name}{marker}")
            print(f"    Address: {d.address}")
            # print(f"    RSSI: {d.rssi}") ONLY WORKS ON WINDOWS
            print(f"    Details: {d.details}")
            print()
        
        # If preferred device was not found, show a note
        if (preferred_name or preferred_address) and not preferred_found:
            print(f"⚠ Note: Preferred device '{preferred_name or preferred_address}' not found in scan.")
            print("Please select a device manually or ensure the device is powered on and discoverable.\n")

        # Prompt user to select a device or rescan
        while True:
            choice = input("Select your device by number, or press ENTER to rescan: ").strip()

            # Rescan requested
            if choice == "":
                break  # break selection loop -> outer loop restarts scan

            # Validate numeric input
            if not choice.isdigit():
                print("Please enter a number corresponding to the device.")
                continue

            idx = int(choice) - 1
            if 0 <= idx < len(devices):
                selected = devices[idx]
                addr = selected.address
                print(f"\nSelected device: {selected.name or 'Unknown'} ({addr})")
                # Return the address string used to construct BleakClient(addr)
                return addr

            print("Invalid selection. Try again.")

async def ble_device_selection():
    """
    Scan for BLE devices, let the user pick one, and return the device address string
    suitable for passing to BleakClient(address).

    Returns:
        str: Selected device address (e.g. "AA:BB:CC:DD:EE:FF")
    """
    return await ble_device_selection_with_preference()

async def multi_ble_device_selection(num_devices: int = 3):
    """
    Scan for BLE devices and let user select multiple devices.
    
    Args:
        num_devices: Number of devices to select (default: 3)
    
    Returns:
        list: List of dictionaries with 'address' and 'name' keys
    """
    selected_devices = []
    
    for device_num in range(1, num_devices + 1):
        print(f"\n{'='*50}")
        print(f"Selecting Device {device_num} of {num_devices}")
        print(f"{'='*50}")
        
        while True:
            print("Scanning for Bluetooth LE devices...")
            devices = await bleak.BleakScanner.discover(timeout=5.0)
            
            if not devices:
                print("\nNo BLE devices found.")
                time.sleep(2)
                input("Press ENTER to rescan when ready...")
                continue
            
            # Filter out already selected devices
            selected_addresses = [d['address'] for d in selected_devices]
            available_devices = [d for d in devices if d.address not in selected_addresses]
            
            if not available_devices:
                print("\nAll discovered devices have already been selected.")
                input("Press ENTER to rescan...")
                continue
            
            print(f"\nFound {len(available_devices)} available device(s):\n")
            for i, d in enumerate(available_devices, 1):
                name = d.name or "Unknown"
                print(f"[{i}] {name}")
                print(f"    Address: {d.address}")
                print()
            
            choice = input(f"Select device {device_num} by number, or press ENTER to rescan: ").strip()
            
            if choice == "":
                continue
            
            if choice.isdigit():
                idx = int(choice) - 1
                if 0 <= idx < len(available_devices):
                    selected = available_devices[idx]
                    selected_devices.append({
                        'address': selected.address,
                        'name': selected.name or f"Device_{device_num}"
                    })
                    print(f"\nSelected: {selected.name or 'Unknown'} ({selected.address})")
                    break
            
            print("Invalid selection. Try again.")
    
    return selected_devices



async def _headless_find_device(preferred_name: str, preferred_address: str, max_retries: int = 10) -> str:
    """
    Headless BLE device finder. No input() calls — just retries automatically.
    Returns device address or None if not found.
    """
    for attempt in range(1, max_retries + 1):
        print(f"  Scan {attempt}/{max_retries}...")
        try:
            devices = await bleak.BleakScanner.discover(timeout=8.0)
        except Exception as e:
            print(f"  Scan error: {e}")
            time.sleep(2)
            continue

        # Match by name
        if preferred_name:
            for device in devices:
                device_name = device.name or ""
                if device_name.lower() == preferred_name.lower():
                    print(f"  ✓ Found by name: {device.name} ({device.address})")
                    return device.address

        # Match by address
        if preferred_address:
            for device in devices:
                if device.address.lower() == preferred_address.lower():
                    print(f"  ✓ Found by address: {device.name or preferred_name} ({device.address})")
                    return device.address

        found_names = [f"{d.name or 'Unknown'} ({d.address})" for d in devices]
        print(f"  Not found. {len(devices)} visible devices:")
        for dev_info in found_names:
            print(f"    - {dev_info}")

        if attempt < max_retries:
            time.sleep(3)

    return None


async def headless():
    """
    Headless mode: No prompts, no file recording on Python side.
    Just connects to known BLE devices and streams via WebSocket.
    Unity handles all metadata, recording, and trial control.
    """
    print("=" * 60)
    print("  BLE Bridge — Headless Mode (Unity-controlled)")
    print("=" * 60)
    print("  No prompts. No Python-side recording.")
    print("  WebSocket server: ws://localhost:8765")
    print("=" * 60)
    print()

    # Known devices
    known_bike_address = "F9:A4:28:75:B1:08"
    known_bike_name = "NEO Bike Plus 26414"
    known_hr_address = "CD:B6:56:43:2C:8B"
    known_hr_name = "HRMPro+:629521"

    selected_devices = []

    # --- Auto-find bike (no input() calls, just retry) ---
    print(f"[Bike] Scanning for {known_bike_name}...")
    bike_address = await _headless_find_device(known_bike_name, known_bike_address, max_retries=10)
    if bike_address:
        selected_devices.append({'address': bike_address, 'name': 'Bike', 'type': 'bike'})
        print(f"[Bike] ✓ Found: {bike_address}\n")
    else:
        print(f"[Bike] ✗ Not found after retries\n")

    # --- Auto-find HR monitor ---
    print(f"[HR] Scanning for {known_hr_name}...")
    hr_address = await _headless_find_device(known_hr_name, known_hr_address, max_retries=5)
    if hr_address:
        selected_devices.append({'address': hr_address, 'name': 'HRMonitor', 'type': 'hr_monitor'})
        print(f"[HR] ✓ Found: {hr_address}\n")
    else:
        print(f"[HR] ✗ Not found after retries\n")

    if not selected_devices:
        print("ERROR: No devices found. Exiting.")
        return 1

    # --- Setup threads (no file output — use /dev/null equivalent) ---
    import tempfile
    dummy_output = tempfile.mkdtemp(prefix="ble_headless_")

    stop_event = threading.Event()
    recording_enabled = threading.Event()
    recording_enabled.set()  # Always recording — Unity controls trial boundaries

    device_threads = []
    bike_queues = []
    resistance_queue = queue.Queue(maxsize=10)

    for i, device_info in enumerate(selected_devices, 1):
        device_id = f"device_{i}"
        device_name = device_info['name']
        device_address = device_info['address']
        device_type = device_info.get('type', 'bike')

        device_queue = queue.Queue(maxsize=2000)

        if device_type == 'bike':
            bike_queues.append(device_queue)
            device_thread = threading.Thread(
                target=multi_device_handler.run_device_handler,
                args=(stop_event, device_id, device_name, device_address,
                      dummy_output, device_queue, resistance_queue, recording_enabled),
                daemon=False
            )
        elif device_type == 'hr_monitor':
            device_thread = threading.Thread(
                target=generic_ble_handler.run_generic_device_handler,
                args=(stop_event, device_id, device_name, 'hr_monitor', device_address,
                      dummy_output, device_queue, None, None, None, None, recording_enabled),
                daemon=False
            )
        else:
            continue

        device_threads.append(device_thread)

    # Torque calculator (only if bike connected)
    torque_thread = None
    if bike_queues:
        torque_thread = threading.Thread(
            target=torque_calculator.run_torque_estimator,
            args=(stop_event, bike_queues[0], None),
            daemon=False
        )

    # WebSocket server
    unity_ws_server.set_resistance_queue(resistance_queue)
    ws_thread = threading.Thread(
        target=unity_ws_server.start_ws_server,
        args=(stop_event,),
        daemon=True
    )

    # --- Start everything ---
    print("Starting WebSocket server...")
    ws_thread.start()
    time.sleep(1)

    print(f"Connecting to {len(selected_devices)} device(s)...")
    for thread in device_threads:
        thread.start()

    if torque_thread:
        torque_thread.start()

    time.sleep(3)

    print()
    print("=" * 60)
    print("  ✓ BLE BRIDGE RUNNING")
    print(f"  Devices: {len(selected_devices)} connected")
    print("  Streaming to Unity via WebSocket...")
    print("  (Kill this process or press Ctrl+C to stop)")
    print("=" * 60)
    print()

    # Keep running until killed
    import signal
    def signal_handler(sig, frame):
        print("\nShutdown signal received.")
        stop_event.set()
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    try:
        while not stop_event.is_set():
            time.sleep(1)
    except KeyboardInterrupt:
        stop_event.set()

    # Cleanup
    print("Stopping threads...")
    stop_event.set()
    for thread in device_threads:
        thread.join(timeout=5)
    if torque_thread:
        torque_thread.join(timeout=5)
    ws_thread.join(timeout=3)

    # Clean up temp dir
    import shutil
    try:
        shutil.rmtree(dummy_output, ignore_errors=True)
    except:
        pass

    print("BLE bridge stopped.")
    return 0


async def main():
    # Check for --headless flag
    import sys
    if '--headless' in sys.argv:
        result = await headless()
        sys.exit(result or 0)
    else:
        await aggregator()

if __name__ == "__main__":
    asyncio.run(main())