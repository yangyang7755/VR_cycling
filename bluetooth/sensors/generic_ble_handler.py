# generic_ble_handler.py
"""
Generic Bluetooth Low Energy (BLE) device handler.
Can be configured for different device types (HR monitor, EEG, etc.)
"""
import asyncio
import json
import time
import threading
import csv
from pathlib import Path
from bleak import BleakClient
import queue
from typing import List, Dict, Optional, Callable

class GenericBLEDeviceHandler:
    """
    Generic handler for any BLE device.
    Can be configured with device-specific service/characteristic UUIDs.
    """
    
    def __init__(self, device_id: str, device_name: str, device_type: str = "generic"):
        self.device_id = device_id
        self.device_name = device_name
        self.device_type = device_type  # e.g., "hr_monitor", "eeg", "bike"
        
        # Device-specific configuration (to be set before connection)
        self.service_uuids: List[str] = []
        self.data_characteristic_uuids: List[str] = []
        self.control_characteristic_uuid: Optional[str] = None
        self.status_characteristic_uuid: Optional[str] = None
        
        # Data handlers (to be set by device-specific configuration)
        self.data_handlers: Dict[str, Callable] = {}
        
        # Latest data from each characteristic
        self.latest_data: Dict[str, any] = {}
        
    def configure_hr_monitor(self):
        """Configure handler for Heart Rate Monitor (supports both standard and Garmin)"""
        def debug_log(location, message, data=None):
            try:
                log_path = Path(".cursor/debug.log")
                log_path.parent.mkdir(parents=True, exist_ok=True)
                log_entry = {
                    "timestamp": time.time(),
                    "location": location,
                    "message": message,
                    "data": data or {},
                    "sessionId": "hr-test-session",
                    "hypothesisId": "B"
                }
                with open(log_path, "a", encoding="utf-8") as f:
                    f.write(json.dumps(log_entry) + "\n")
            except Exception:
                pass
        
        self.device_type = "hr_monitor"
        # Support both standard BLE Heart Rate Service and Garmin-specific service
        # Standard Heart Rate Service UUID
        standard_service = "0000180d-0000-1000-8000-00805f9b34fb"
        # Garmin HRM Pro Service UUID
        garmin_service = "6a4e3200-667b-11e3-949a-0800200c9a66"
        self.service_uuids = [standard_service, garmin_service]
        
        # Standard Heart Rate Measurement Characteristic
        standard_char = "00002a37-0000-1000-8000-00805f9b34fb"
        # Garmin HRM Pro Characteristic UUIDs (common ones)
        # Garmin typically uses: 6A4E3201-667B-11E3-949A-0800200C9A66 for data
        garmin_char = "6a4e3201-667b-11e3-949a-0800200c9a66"
        self.data_characteristic_uuids = [standard_char, garmin_char]
        
        debug_log("generic_ble_handler.py:configure_hr_monitor", "HR monitor configured", {
            "device_name": self.device_name,
            "service_uuid": self.service_uuids[0],
            "characteristic_uuid": self.data_characteristic_uuids[0]
        })
        
        def hr_data_handler_standard(sender, data):
            """Parse standard BLE HR measurement data (supports Garmin HRM Pro format)"""
            if len(data) < 2:
                print(f"[{self.device_name}] Warning: Received incomplete HR data: {data.hex()}")
                return
            
            # Standard format: flags (1 byte) + heart rate value
            flags = data[0]
            hr_format = flags & 0x01  # 0 = 8-bit, 1 = 16-bit
            has_ibi = (flags >> 4) & 0x01  # Garmin HRM Pro includes IBI data
            
            # Extract heart rate value
            if hr_format == 1:  # 16-bit value
                if len(data) >= 3:
                    hr_value = int.from_bytes(data[1:3], byteorder='little')
                else:
                    print(f"[{self.device_name}] Warning: Data too short for 16-bit HR: {data.hex()}")
                    return
            else:  # 8-bit value
                hr_value = data[1]
            
            # Validate HR value (reasonable range: 40-220 BPM)
            if hr_value < 40 or hr_value > 220:
                print(f"[{self.device_name}] Warning: HR value out of range: {hr_value} BPM (Data: {data.hex()})")
                return
            
            # Outlier detection: Check for sudden spikes
            last_hr = getattr(self, '_last_valid_hr', None)
            if last_hr is not None:
                hr_change = abs(hr_value - last_hr)
                max_change = 30  # Maximum BPM change allowed between readings
                
                # If change is too large, it's likely an error
                if hr_change > max_change:
                    print(f"[{self.device_name}] ⚠ Outlier detected: HR jumped from {last_hr} to {hr_value} BPM (+{hr_change})")
                    print(f"[{self.device_name}] Rejecting outlier, keeping previous value: {last_hr} BPM")
                    hr_value = last_hr  # Use previous valid value
                    # Don't update _last_valid_hr, so next reading is compared to the real last value
                else:
                    self._last_valid_hr = hr_value
            else:
                # First reading, accept it
                self._last_valid_hr = hr_value
            
            # Parse IBI data if present (Garmin HRM Pro feature)
            ibi_data = []
            if has_ibi and len(data) > 2:
                ibi_start = 3 if hr_format == 1 else 2
                for i in range(ibi_start, len(data) - 1, 2):
                    if i + 1 < len(data):
                        # IBI values are stored as little-endian 16-bit values in 1/1024 seconds
                        ibi_raw = (data[i + 1] << 8) | data[i]
                        # Convert from 1/1024 seconds to milliseconds
                        ibi_ms = ibi_raw * 1000 / 1024
                        ibi_data.append(ibi_ms)
            
            # Print HR reading only every 10th reading (reduced spam)
            hr_reading_count = getattr(self, '_hr_reading_count', 0) + 1
            self._hr_reading_count = hr_reading_count
            
            if hr_reading_count == 1:
                print(f"\n{'='*60}")
                print(f"[{self.device_name}] ✓ HR Monitor Connected - Reading HR data")
                print(f"[{self.device_name}] First HR: {hr_value} BPM")
                print(f"{'='*60}\n")
            elif hr_reading_count % 10 == 0:
                print(f"[{self.device_name}] HR: {hr_value} BPM (Reading #{hr_reading_count})")
            
            # Publish to WebSocket for Unity (every reading)
            try:
                import actuators.unity.unity_ws_server as unity_ws_server
                unity_ws_server.publish_heart_rate_bpm(float(hr_value))
                if hr_reading_count == 1:
                    print(f"[HR→WebSocket] ✓ Publishing to Unity WebSocket")
            except Exception as e:
                if hr_reading_count == 1:
                    print(f"[HR→WebSocket] ✗ Failed to publish: {e}")
            
            debug_log("generic_ble_handler.py:hr_data_handler_standard", "HR data received", {
                "hr_value": hr_value,
                "flags": flags,
                "has_ibi": has_ibi,
                "ibi_count": len(ibi_data),
                "data_length": len(data)
            })
            
            self.latest_data["heart_rate"] = {
                "value": hr_value,
                "timestamp": time.time(),
                "flags": flags,
                "has_ibi": has_ibi,
                "ibi_data": ibi_data if ibi_data else None
            }
        
        def hr_data_handler_garmin(sender, data):
            """Parse Garmin HRM Pro data"""
            # Garmin HRM Pro data format - try multiple parsing methods
            hr_value = None
            
            if len(data) >= 2:
                # Method 1: Try byte 1 as HR (common in Garmin)
                hr_value = data[1]
                # Method 2: If value seems wrong, try bytes 1-2 as little-endian
                if hr_value == 0 or hr_value > 220:
                    if len(data) >= 3:
                        hr_value = int.from_bytes(data[1:3], byteorder='little')
                # Method 3: Try byte 0 if it's not a flag
                if hr_value == 0 or hr_value > 220:
                    hr_value = data[0] if data[0] > 0 and data[0] <= 220 else None
            
            if hr_value is None or hr_value == 0 or hr_value > 220:
                # Try to find HR value in the data
                for i, byte in enumerate(data):
                    if 40 <= byte <= 220:  # Reasonable HR range
                        hr_value = byte
                        break
            
            if hr_value and 40 <= hr_value <= 220:
                # Print HR reading prominently
                print(f"\n{'='*60}")
                print(f"[{self.device_name}] HEART RATE: {hr_value} BPM (Garmin HRM Pro)")
                print(f"{'='*60}\n")
                
                debug_log("generic_ble_handler.py:hr_data_handler_garmin", "HR data received", {
                    "hr_value": hr_value,
                    "raw_data": data.hex(),
                    "data_length": len(data)
                })
                
                self.latest_data["heart_rate"] = {
                    "value": hr_value,
                    "timestamp": time.time(),
                    "raw_data": data.hex()
                }
            else:
                # Debug: print raw data if we can't parse it
                print(f"[{self.device_name}] Received Garmin data (unable to parse HR): {data.hex()}")
                debug_log("generic_ble_handler.py:hr_data_handler_garmin", "HR data received but unparseable", {
                    "raw_data": data.hex(),
                    "data_length": len(data)
                })
        
        # Register handlers for both standard and Garmin characteristics
        # Also store handler reference for use when trying alternative characteristics
        self.data_handlers[standard_char] = hr_data_handler_standard
        self.data_handlers[garmin_char] = hr_data_handler_garmin
        
        # Store handler reference for use in enable_notifications
        self.hr_data_handler_standard = hr_data_handler_standard
    
    def configure_custom_device(self, 
                              service_uuids: List[str],
                              data_characteristic_uuids: List[str],
                              data_handlers: Dict[str, Callable],
                              control_characteristic_uuid: Optional[str] = None,
                              status_characteristic_uuid: Optional[str] = None):
        """
        Configure handler for custom device with specific UUIDs
        
        Args:
            service_uuids: List of service UUIDs to use
            data_characteristic_uuids: List of characteristic UUIDs that stream data
            data_handlers: Dict mapping characteristic UUID to handler function
            control_characteristic_uuid: Optional control characteristic UUID
            status_characteristic_uuid: Optional status characteristic UUID
        """
        self.service_uuids = service_uuids
        self.data_characteristic_uuids = data_characteristic_uuids
        self.data_handlers = data_handlers
        self.control_characteristic_uuid = control_characteristic_uuid
        self.status_characteristic_uuid = status_characteristic_uuid
    
    def clean_dict(self, raw_data):
        """Convert measurement to clean dictionary"""
        record = {key: value for key, value in raw_data.items() if value is not None}
        record = {
            "time": time.time(),
            "device_id": self.device_id,
            "device_name": self.device_name,
            "device_type": self.device_type,
            **record
        }
        return record
    
    def write_to_json(self, record, file):
        """Write record to JSON file"""
        file.write(json.dumps(record, default=str) + "\n")
        file.flush()
    
    def write_to_csv(self, record, csv_writer, fieldnames_written_ref):
        """Write record to CSV file"""
        # Flatten nested dictionaries for CSV
        flat_record = {}
        for key, value in record.items():
            if isinstance(value, dict):
                # Flatten nested dicts (e.g., heart_rate.value -> heart_rate_value)
                for nested_key, nested_value in value.items():
                    flat_record[f"{key}_{nested_key}"] = nested_value
            elif isinstance(value, list):
                # Convert lists to string representation
                flat_record[key] = str(value) if value else ""
            else:
                flat_record[key] = value
        
        # Write header on first write
        if not fieldnames_written_ref[0]:
            fieldnames = list(flat_record.keys())
            csv_writer.fieldnames = fieldnames
            csv_writer.writeheader()
            fieldnames_written_ref[0] = True
        else:
            # Check if new fields appeared and update fieldnames if needed
            current_fieldnames = set(csv_writer.fieldnames)
            new_fields = set(flat_record.keys()) - current_fieldnames
            if new_fields:
                # Add new fields to fieldnames
                csv_writer.fieldnames = list(csv_writer.fieldnames) + list(new_fields)
        
        # Only write fields that are in fieldnames (ignore extra fields)
        filtered_record = {k: v for k, v in flat_record.items() if k in csv_writer.fieldnames}
        csv_writer.writerow(filtered_record)
    
    async def enable_notifications(self, client, characteristic_uuid: str):
        """Enable notifications for a characteristic"""
        try:
            # Find the characteristic - use client.services (property) instead of get_services() (method)
            # This works with newer versions of bleak
            services = client.services
            
            # Get handler reference for HR monitors
            standard_char = "00002a37-0000-1000-8000-00805f9b34fb"
            hr_data_handler_standard = self.data_handlers.get(standard_char, None)
            if not hr_data_handler_standard and self.device_type == "hr_monitor":
                # Fallback: create a simple handler
                def hr_data_handler_standard(sender, data):
                    print(f"[{self.device_name}] Received data from {sender}: {data.hex()}")
                    # Try to parse as HR data
                    if len(data) >= 2:
                        flags = data[0]
                        hr_format = flags & 0x01
                        if hr_format == 1 and len(data) >= 3:
                            hr_value = int.from_bytes(data[1:3], byteorder='little')
                        else:
                            hr_value = data[1]
                        if 40 <= hr_value <= 220:
                            print(f"\n{'='*60}")
                            print(f"[{self.device_name}] HEART RATE: {hr_value} BPM")
                            print(f"{'='*60}\n")
                            self.latest_data["heart_rate"] = {
                                "value": hr_value,
                                "timestamp": time.time()
                            }
            
            # Debug: List all services and characteristics for HR monitor
            if self.device_type == "hr_monitor":
                print(f"\n[{self.device_name}] Scanning device for available services and characteristics...")
                print(f"[{self.device_name}] Looking for:")
                print(f"[{self.device_name}]   - Standard Heart Rate Service: 0000180d-0000-1000-8000-00805f9b34fb")
                print(f"[{self.device_name}]   - Garmin HRM Service: 6a4e3200-667b-11e3-949a-0800200c9a66\n")
                
                found_hr_service = False
                found_garmin_service = False
                for service in services:
                    service_uuid = service.uuid.lower()
                    print(f"[{self.device_name}] Service found: {service_uuid}")
                    
                    # Check if this is the standard Heart Rate Service
                    if "180d" in service_uuid or service_uuid == "0000180d-0000-1000-8000-00805f9b34fb":
                        found_hr_service = True
                        print(f"[{self.device_name}] ✓ Found Standard Heart Rate Service!")
                    
                    # Check if this is the Garmin HRM Service
                    if "6a4e3200" in service_uuid or service_uuid == "6a4e3200-667b-11e3-949a-0800200c9a66":
                        found_garmin_service = True
                        print(f"[{self.device_name}] ✓ Found Garmin HRM Service!")
                    
                    for char in service.characteristics:
                        char_uuid = char.uuid.lower()
                        char_props = ", ".join(char.properties)
                        print(f"[{self.device_name}]   └─ Characteristic: {char_uuid} (Properties: {char_props})")
                        
                        # Check if this is the standard Heart Rate Measurement Characteristic
                        if "2a37" in char_uuid or char_uuid == "00002a37-0000-1000-8000-00805f9b34fb":
                            print(f"[{self.device_name}]     ✓ Standard Heart Rate Measurement Characteristic!")
                        
                        # Check if this is a Garmin characteristic
                        if "6a4e3201" in char_uuid or char_uuid == "6a4e3201-667b-11e3-949a-0800200c9a66":
                            print(f"[{self.device_name}]     ✓ Garmin HRM Data Characteristic!")
                
                if not found_hr_service and not found_garmin_service:
                    print(f"\n[{self.device_name}] ⚠ Warning: Neither standard nor Garmin HR service found")
                    print(f"[{self.device_name}] Please check the device documentation\n")
            
            # Now try to find and enable the characteristic
            for service in services:
                for char in service.characteristics:
                    if char.uuid.lower() == characteristic_uuid.lower():
                        # Enable notifications (write 0x0100 to descriptor 0x2902)
                        await client.start_notify(char.uuid, self.data_handlers.get(characteristic_uuid, self._default_handler))
                        if self.device_type == "hr_monitor":
                            print(f"[{self.device_name}] ✓ Heart Rate notifications enabled")
                        else:
                            print(f"[{self.device_name}] Notifications enabled for {characteristic_uuid}")
                        return True
            
            # If not found, try partial UUID match (some devices use short UUIDs)
            target_uuid_short = characteristic_uuid[-4:].lower()  # Last 4 hex digits
            for service in services:
                for char in service.characteristics:
                    char_uuid = char.uuid.lower()
                    # Check if last 4 digits match (for short UUIDs like "2a37")
                    if char_uuid.endswith(target_uuid_short) or target_uuid_short in char_uuid:
                        print(f"[{self.device_name}] Found matching characteristic by partial UUID: {char_uuid}")
                        await client.start_notify(char.uuid, self.data_handlers.get(characteristic_uuid, self._default_handler))
                        if self.device_type == "hr_monitor":
                            print(f"[{self.device_name}] ✓ Heart Rate notifications enabled (using UUID: {char.uuid})")
                        else:
                            print(f"[{self.device_name}] Notifications enabled for {char.uuid}")
                        return True
            
            # For HR monitors, if standard characteristic not found, try all notify characteristics
            if self.device_type == "hr_monitor":
                print(f"\n[{self.device_name}] Standard HR characteristic not found. Trying all notify characteristics...")
                notify_chars = []
                for service in services:
                    for char in service.characteristics:
                        if "notify" in char.properties:
                            notify_chars.append((service.uuid, char))
                            print(f"[{self.device_name}] Found notify characteristic: {char.uuid} in service {service.uuid}")
                
                if notify_chars:
                    # Try the first notify characteristic (often the data one)
                    service_uuid, char = notify_chars[0]
                    print(f"[{self.device_name}] Attempting to enable notifications on: {char.uuid}")
                    try:
                        # Use standard handler for now, it will try to parse HR data
                        handler = hr_data_handler_standard if hr_data_handler_standard else self._default_handler
                        await client.start_notify(char.uuid, handler)
                        print(f"[{self.device_name}] ✓ Notifications enabled on {char.uuid}")
                        print(f"[{self.device_name}] Waiting for data... (make sure device is properly worn)")
                        return True
                    except Exception as e:
                        print(f"[{self.device_name}] Failed to enable notifications on {char.uuid}: {e}")
                        # Try next one
                        if len(notify_chars) > 1:
                            service_uuid, char = notify_chars[1]
                            print(f"[{self.device_name}] Trying next notify characteristic: {char.uuid}")
                            try:
                                handler = hr_data_handler_standard if hr_data_handler_standard else self._default_handler
                                await client.start_notify(char.uuid, handler)
                                print(f"[{self.device_name}] ✓ Notifications enabled on {char.uuid}")
                                return True
                            except Exception as e2:
                                print(f"[{self.device_name}] Also failed: {e2}")
            
            print(f"[{self.device_name}] Warning: Characteristic {characteristic_uuid} not found")
            return False
        except Exception as e:
            print(f"[{self.device_name}] Error enabling notifications: {e}")
            import traceback
            traceback.print_exc()
            return False
    
    def _default_handler(self, sender, data):
        """Default handler for unconfigured characteristics"""
        print(f"[{self.device_name}] Received data from {sender}: {data.hex()}")
        self.latest_data[sender] = {
            "raw_data": data.hex(),
            "timestamp": time.time()
        }
    
    async def send_command(self, client, command: bytes):
        """Send command to control characteristic"""
        if not self.control_characteristic_uuid:
            print(f"[{self.device_name}] No control characteristic configured")
            return False
        
        try:
            # Use client.services (property) instead of get_services() (method)
            services = client.services
            for service in services:
                for char in service.characteristics:
                    if char.uuid.lower() == self.control_characteristic_uuid.lower():
                        await client.write_gatt_char(char.uuid, command)
                        print(f"[{self.device_name}] Command sent: {command.hex()}")
                        return True
            return False
        except Exception as e:
            print(f"[{self.device_name}] Error sending command: {e}")
            return False
    
    async def run_device_loop(self, address: str, base_outpath: str,
                             stop_event: threading.Event,
                             data_queue: "queue.Queue" = None,
                             initialization_commands: List[bytes] = None,
                             recording_enabled: threading.Event = None):
        """
        Main loop for generic BLE device
        
        Args:
            address: Bluetooth device address
            base_outpath: Base path for data files
            stop_event: Threading event to stop the loop
            data_queue: Optional queue for data
            initialization_commands: Optional list of commands to send after connection
        """
        # Device-specific output path
        device_outpath = f"{base_outpath}/device_{self.device_id}"
        data_outpath_ndjson = f"{device_outpath}/data.ndjson"
        data_outpath_csv = f"{device_outpath}/data.csv"
        Path(data_outpath_ndjson).parent.mkdir(parents=True, exist_ok=True)
        
        # #region agent log
        try:
            log_path = Path(".cursor/debug.log")
            log_path.parent.mkdir(parents=True, exist_ok=True)
            log_entry = {
                "timestamp": time.time(),
                "location": "generic_ble_handler.py:run_device_loop",
                "message": "Device output paths created",
                "data": {
                    "device_id": self.device_id,
                    "device_name": self.device_name,
                    "device_type": self.device_type,
                    "base_outpath": base_outpath,
                    "device_outpath": device_outpath,
                    "data_outpath_ndjson": data_outpath_ndjson,
                    "data_outpath_csv": data_outpath_csv,
                    "recording_enabled": recording_enabled.is_set() if recording_enabled else None
                },
                "runId": "hr-debug",
                "hypothesisId": "A"
            }
            with open(log_path, "a", encoding="utf-8") as f:
                f.write(json.dumps(log_entry) + "\n")
        except Exception:
            pass
        # #endregion
        
        # Open both files
        data_f_json = open(data_outpath_ndjson, "a", encoding="utf-8")
        data_f_csv = open(data_outpath_csv, "a", encoding="utf-8", newline="")
        csv_writer = csv.DictWriter(data_f_csv, fieldnames=[])
        fieldnames_written = [False]  # Use list to allow modification in nested function
        
        client = BleakClient(address)
        try:
            await client.connect()
            print(f"\n{'='*60}")
            print(f"[{self.device_name}] ✓ CONNECTED to {address}")
            if self.device_type == "hr_monitor":
                print(f"[{self.device_name}] Waiting for heart rate data...")
                print(f"[{self.device_name}] Make sure the HR monitor is properly worn/positioned")
            print(f"{'='*60}\n")
            
            # Wait for services to be discovered (Garmin devices may need more time)
            # Try to access services with retries
            services_ready = False
            for attempt in range(5):
                try:
                    services = client.services
                    if services and len(services) > 0:
                        services_ready = True
                        break
                except Exception:
                    pass
                await asyncio.sleep(0.5)
            
            if not services_ready:
                print(f"[{self.device_name}] ⚠ Warning: Services not ready after connection")
                print(f"[{self.device_name}] Trying to continue anyway...")
            
            # Additional delay for Garmin devices
            if self.device_type == "hr_monitor":
                print(f"[{self.device_name}] Waiting for Garmin HRM services to initialize...")
                await asyncio.sleep(1.0)
            
            # Send initialization commands if provided
            # #region agent log
            import json
            import time
            # Path already imported at module level
            try:
                log_path = Path(".cursor/debug.log")
                log_path.parent.mkdir(parents=True, exist_ok=True)
                log_entry = {
                    "timestamp": time.time(),
                    "location": "generic_ble_handler.py:run_device_loop",
                    "message": "Checking initialization_commands parameter",
                    "data": {
                        "initialization_commands_type": str(type(initialization_commands)),
                        "initialization_commands_value": str(initialization_commands),
                        "is_none": initialization_commands is None,
                        "is_event": isinstance(initialization_commands, threading.Event) if hasattr(threading, 'Event') else False
                    },
                    "runId": "debug-run1",
                    "hypothesisId": "A"
                }
                with open(log_path, "a", encoding="utf-8") as f:
                    f.write(json.dumps(log_entry) + "\n")
            except Exception:
                pass
            # #endregion
            if initialization_commands:
                # #region agent log
                try:
                    log_entry = {
                        "timestamp": time.time(),
                        "location": "generic_ble_handler.py:run_device_loop",
                        "message": "Attempting to iterate initialization_commands",
                        "data": {
                            "initialization_commands_type": str(type(initialization_commands)),
                            "is_iterable": hasattr(initialization_commands, '__iter__')
                        },
                        "runId": "debug-run1",
                        "hypothesisId": "A"
                    }
                    with open(log_path, "a", encoding="utf-8") as f:
                        f.write(json.dumps(log_entry) + "\n")
                except Exception:
                    pass
                # #endregion
                for cmd in initialization_commands:
                    await self.send_command(client, cmd)
                    await asyncio.sleep(0.1)  # Small delay between commands
            
            # Enable notifications for all data characteristics
            for char_uuid in self.data_characteristic_uuids:
                success = await self.enable_notifications(client, char_uuid)
                if success and self.device_type == "hr_monitor":
                    print(f"[{self.device_name}] ✓ Heart Rate notifications enabled")
                    print(f"[{self.device_name}] Ready to receive heart rate data...\n")
                elif not success and self.device_type == "hr_monitor":
                    print(f"[{self.device_name}] ⚠ Warning: Failed to enable Heart Rate notifications")
                    print(f"[{self.device_name}] For Garmin HRM Pro, make sure:")
                    print(f"[{self.device_name}]   1. Device is paired in Windows Bluetooth settings")
                    print(f"[{self.device_name}]   2. Device is properly worn/positioned")
                    print(f"[{self.device_name}]   3. Device is not connected to another app")
            
            last_seen_data = {}
            hr_reading_count = 0  # Track number of HR readings received
            first_10_hr_readings = []  # Store first 10 readings for display
            
            while not stop_event.is_set():
                # Check for new data
                for key, data in self.latest_data.items():
                    if key not in last_seen_data or last_seen_data[key] != data:
                        # New data received
                        rec = self.clean_dict(data)
                        
                        # Enhanced printing for HR monitor
                        if self.device_type == "hr_monitor":
                            # Try to extract HR value from different possible structures
                            hr_value = None
                            if "heart_rate" in rec:
                                if isinstance(rec["heart_rate"], dict):
                                    hr_value = rec["heart_rate"].get("value")
                                else:
                                    hr_value = rec["heart_rate"]
                            elif "value" in rec:
                                hr_value = rec["value"]
                            
                            if hr_value:
                                hr_reading_count += 1
                                timestamp_str = time.strftime('%H:%M:%S', time.localtime(rec.get('time', time.time())))
                                recording_status = "📝 RECORDING" if (recording_enabled is None or recording_enabled.is_set()) else "⏸️  PAUSED"
                                
                                # Store first 10 readings
                                if hr_reading_count <= 10:
                                    first_10_hr_readings.append({
                                        'reading': hr_reading_count,
                                        'hr': hr_value,
                                        'time': timestamp_str,
                                        'timestamp': rec.get('time', time.time())
                                    })
                                
                                # Enhanced display for first 10 readings
                                if hr_reading_count <= 10:
                                    print(f"\n{'='*60}")
                                    print(f"[{self.device_name}] ⚡ HEART RATE READING #{hr_reading_count}: {hr_value} BPM")
                                    print(f"[{self.device_name}] Time: {timestamp_str} | Status: {recording_status}")
                                    print(f"[{self.device_name}] Data saved to: {data_outpath_ndjson}")
                                    print(f"[{self.device_name}] Data saved to: {data_outpath_csv}")
                                    if hr_reading_count == 10:
                                        print(f"\n{'='*60}")
                                        print(f"[{self.device_name}] ✓ First 10 HR readings received successfully!")
                                        print(f"[{self.device_name}] Summary of first 10 readings:")
                                        for reading in first_10_hr_readings:
                                            print(f"  [{reading['reading']:2d}] {reading['hr']:3.0f} BPM at {reading['time']}")
                                        avg_hr = sum(r['hr'] for r in first_10_hr_readings) / len(first_10_hr_readings)
                                        print(f"  Average HR (first 10): {avg_hr:.1f} BPM")
                                        print(f"{'='*60}\n")
                                    else:
                                        print(f"{'='*60}\n")
                                else:
                                    # After first 10, show simpler display every 10th reading
                                    if hr_reading_count % 10 == 0:
                                        print(f"[{self.device_name}] ⚡ HR: {hr_value} BPM (Reading #{hr_reading_count}, Time: {timestamp_str}, Status: {recording_status})")
                            else:
                                print(f"[{self.device_name}] Data: {rec}")
                        else:
                            print(f"[{self.device_name}] Data: {rec}")
                        
                        # Only write to file if recording is enabled
                        # #region agent log
                        try:
                            log_path = Path(".cursor/debug.log")
                            log_path.parent.mkdir(parents=True, exist_ok=True)
                            log_entry = {
                                "timestamp": time.time(),
                                "location": "generic_ble_handler.py:run_device_loop",
                                "message": "Data received - checking recording status",
                                "data": {
                                    "device_type": self.device_type,
                                    "recording_enabled_is_none": recording_enabled is None,
                                    "recording_enabled_is_set": recording_enabled.is_set() if recording_enabled else None,
                                    "will_write": recording_enabled is None or (recording_enabled.is_set() if recording_enabled else False),
                                    "has_heart_rate": "heart_rate" in rec,
                                    "record_keys": list(rec.keys())[:10]  # First 10 keys
                                },
                                "runId": "hr-debug",
                                "hypothesisId": "B"
                            }
                            with open(log_path, "a", encoding="utf-8") as f:
                                f.write(json.dumps(log_entry) + "\n")
                        except Exception:
                            pass
                        # #endregion
                        if recording_enabled is None or recording_enabled.is_set():
                            self.write_to_json(rec, data_f_json)
                            self.write_to_csv(rec, csv_writer, fieldnames_written)
                            data_f_csv.flush()  # Ensure CSV is written immediately
                            # #region agent log
                            try:
                                log_path = Path(".cursor/debug.log")
                                log_path.parent.mkdir(parents=True, exist_ok=True)
                                log_entry = {
                                    "timestamp": time.time(),
                                    "location": "generic_ble_handler.py:run_device_loop",
                                    "message": "Data written to files",
                                    "data": {
                                        "device_type": self.device_type,
                                        "file_ndjson": data_outpath_ndjson,
                                        "file_csv": data_outpath_csv
                                    },
                                    "runId": "hr-debug",
                                    "hypothesisId": "B"
                                }
                                with open(log_path, "a", encoding="utf-8") as f:
                                    f.write(json.dumps(log_entry) + "\n")
                            except Exception:
                                pass
                            # #endregion
                        last_seen_data[key] = data
                        
                        if data_queue is not None:
                            try:
                                data_queue.put(rec, timeout=0.001)
                            except queue.Full:
                                data_queue.get_nowait()
                                data_queue.put_nowait(rec)
                        
                        # Publish HR data to Unity WebSocket server if this is an HR monitor
                        if self.device_type == "hr_monitor" and "heart_rate" in data:
                            try:
                                def debug_log(location, message, data=None):
                                    try:
                                        log_path = Path(".cursor/debug.log")
                                        log_path.parent.mkdir(parents=True, exist_ok=True)
                                        log_entry = {
                                            "timestamp": time.time(),
                                            "location": location,
                                            "message": message,
                                            "data": data or {},
                                            "sessionId": "hr-test-session",
                                            "hypothesisId": "C"
                                        }
                                        with open(log_path, "a", encoding="utf-8") as f:
                                            f.write(json.dumps(log_entry) + "\n")
                                    except Exception:
                                        pass
                                
                                import actuators.unity.unity_ws_server as unity_ws_server
                                hr_value = data.get("value")
                                if hr_value is not None:
                                    print(f"[HR→WebSocket] Publishing HR: {hr_value} BPM")
                                    debug_log("generic_ble_handler.py:run_device_loop", "Publishing HR to WebSocket", {
                                        "hr_value": hr_value
                                    })
                                    unity_ws_server.publish_heart_rate_bpm(float(hr_value))
                                    debug_log("generic_ble_handler.py:run_device_loop", "HR published successfully", {
                                        "hr_value": hr_value
                                    })
                                else:
                                    print(f"[HR→WebSocket] WARNING: HR value is None in data: {data}")
                            except Exception as e:
                                # Log error but don't disrupt data collection
                                try:
                                    log_path = Path(".cursor/debug.log")
                                    log_path.parent.mkdir(parents=True, exist_ok=True)
                                    log_entry = {
                                        "timestamp": time.time(),
                                        "location": "generic_ble_handler.py:run_device_loop",
                                        "message": "Error publishing HR to WebSocket",
                                        "data": {"error": str(e)},
                                        "sessionId": "hr-test-session",
                                        "hypothesisId": "C"
                                    }
                                    with open(log_path, "a", encoding="utf-8") as f:
                                        f.write(json.dumps(log_entry) + "\n")
                                except:
                                    pass
                
                await asyncio.sleep(0.001)
        
        finally:
            # Cleanup
            try:
                await client.disconnect()
            except Exception as e:
                print(f"[{self.device_name}] Error during disconnect: {e}")
            
            data_f_json.close()
            data_f_csv.close()
            print(f"[{self.device_name}] Disconnected and files closed")

def run_generic_device_handler(stop_event: threading.Event, 
                              device_id: str, 
                              device_name: str,
                              device_type: str,
                              address: str, 
                              outpath: str,
                              data_queue: "queue.Queue" = None,
                              service_uuids: List[str] = None,
                              data_characteristic_uuids: List[str] = None,
                              data_handlers: Dict[str, Callable] = None,
                              initialization_commands: List[bytes] = None,
                              recording_enabled: threading.Event = None):
    # #region agent log
    import json
    import time
    # Path already imported at module level
    try:
        log_path = Path(".cursor/debug.log")
        log_path.parent.mkdir(parents=True, exist_ok=True)
        log_entry = {
            "timestamp": time.time(),
            "location": "generic_ble_handler.py:run_generic_device_handler",
            "message": "Function entry - parameter types",
            "data": {
                "initialization_commands_type": str(type(initialization_commands)),
                "recording_enabled_type": str(type(recording_enabled)),
                "initialization_commands_is_none": initialization_commands is None,
                "recording_enabled_is_none": recording_enabled is None,
                "initialization_commands_is_event": isinstance(initialization_commands, threading.Event) if hasattr(threading, 'Event') else False
            },
            "runId": "debug-run1",
            "hypothesisId": "A"
        }
        with open(log_path, "a", encoding="utf-8") as f:
            f.write(json.dumps(log_entry) + "\n")
    except Exception:
        pass
    # #endregion
    """
    Thread entry point for generic device handler
    
    Args:
        stop_event: Threading event
        device_id: Unique device identifier
        device_name: Human-readable device name
        device_type: Type of device ("hr_monitor", "eeg", etc.)
        address: Bluetooth address
        outpath: Output path for data
        data_queue: Optional data queue
        service_uuids: List of service UUIDs (for custom devices)
        data_characteristic_uuids: List of data characteristic UUIDs
        data_handlers: Dict of characteristic UUID to handler function
        initialization_commands: Optional commands to send after connection
    """
    handler = GenericBLEDeviceHandler(device_id, device_name, device_type)
    
    # Configure based on device type
    if device_type == "hr_monitor":
        handler.configure_hr_monitor()
    elif service_uuids and data_characteristic_uuids and data_handlers:
        handler.configure_custom_device(
            service_uuids,
            data_characteristic_uuids,
            data_handlers
        )
    else:
        print(f"[{device_name}] Warning: Device type {device_type} not fully configured")
    
    asyncio.run(handler.run_device_loop(
        address, outpath, stop_event, data_queue, initialization_commands, recording_enabled
    ))
