# multi_device_handler.py
"""
Multi-device handler for Bluetooth bike devices.
Each device instance is completely independent to prevent interference.
"""
import asyncio
import json
import time
import threading
from pathlib import Path
from bleak import BleakClient
from pycycling.fitness_machine_service import FitnessMachineService
from pycycling.cycling_power_service import CyclingPowerService
import queue

class BikeDeviceHandler:
    """
    Handler for a single bike device.
    Each instance is completely independent to prevent interference between devices.
    """
    
    def __init__(self, device_id: str, device_name: str = None):
        self.device_id = device_id
        self.device_name = device_name or f"Device_{device_id}"
        
        # Instance-specific state (not shared between devices)
        self.last_ftms = None
        self.last_cps = None
        self.last_cpvs = None
        
        # Instance-specific handlers
        self._setup_handlers()
    
    def _setup_handlers(self):
        """Create device-specific handler functions"""
        def ftms_handler(ftms_meas):
            self.last_ftms = ftms_meas
        
        def cps_handler(cps_meas):
            self.last_cps = cps_meas
        
        def cpvs_handler(cpvs_meas):
            self.last_cpvs = cpvs_meas
        
        self.ftms_handler = ftms_handler
        self.cps_handler = cps_handler
        self.cpvs_handler = cpvs_handler
    
    async def set_resistance_level(self, ftms_service, client, resistance_level: int):
        """
        Set bike simulation parameters using FTMS Indoor Bike Simulation (opcode 0x11).
        The resistance_level here is actually the GRADIENT in units of 0.1%
        (e.g., 50 = 5.0% grade, 100 = 10.0% grade).
        
        This makes the bike feel like riding uphill — much more realistic than
        the basic resistance level command (0x04).
        """
        try:
            # Convert resistance_level to grade (it comes as 0-100 representing 0-10% grade)
            # Map: 0 → 0%, 30 → 0% (flat baseline), 55 → 5%, 80 → 10%
            # Actually, let's just use it directly as grade × 10
            # Unity sends: flatResistance(30) + slope% × resistancePerPercent(5)
            # So resistance 30 = flat, 55 = 5% grade, 80 = 10% grade
            # Convert back to grade: grade% = (resistance - 30) / 5
            grade_percent = max(0.0, (resistance_level - 30) / 5.0)
            
            # FTMS Indoor Bike Simulation Parameters (opcode 0x11)
            # Format: [0x11, wind_low, wind_high, grade_low, grade_high, crr, cw]
            control_point_uuid = "00002ad9-0000-1000-8000-00805f9b34fb"
            
            # Grade in units of 0.01% (sint16): 5.0% = 500
            grade_raw = int(grade_percent * 100)  # e.g., 5.0% → 500
            grade_bytes = grade_raw.to_bytes(2, byteorder='little', signed=True)
            
            # Wind speed = 0 m/s
            wind_bytes = (0).to_bytes(2, byteorder='little', signed=True)
            
            # Rolling resistance coefficient: 0.004 (typical road bike)
            crr = 40  # units of 0.0001
            
            # Wind resistance coefficient: 0.51 kg/m (typical cyclist)
            cw = 51  # units of 0.01
            
            command = bytes([0x11]) + wind_bytes + grade_bytes + bytes([crr, cw])
            
            await client.write_gatt_char(control_point_uuid, command)
            print(f"[{self.device_name}] Simulation: grade={grade_percent:.1f}% (raw resistance input={resistance_level})")
            return True
            
        except Exception as e:
            print(f"[{self.device_name}] Failed to set simulation params: {e}")
            # Fallback to basic resistance command
            try:
                control_point_uuid = "00002ad9-0000-1000-8000-00805f9b34fb"
                command = bytes([0x04, max(0, min(200, resistance_level))])
                await client.write_gatt_char(control_point_uuid, command)
                print(f"[{self.device_name}] Fallback: basic resistance={resistance_level}")
                return True
            except Exception as e2:
                print(f"[{self.device_name}] Fallback also failed: {e2}")
                return False
    
    def clean_dict(self, raw_data):
        """Convert measurement to clean dictionary"""
        record = {key: value for key, value in raw_data.items() if value is not None}
        record = {
            "time": time.time(),
            "device_id": self.device_id,
            "device_name": self.device_name,
            **record
        }
        return record
    
    def convert_to_dict(self, meas):
        """Convert measurement object to dictionary"""
        if isinstance(meas, dict):
            return meas
        elif hasattr(meas, "_asdict"):
            try:
                return meas._asdict()
            except Exception:
                return {}
        else:
            data = {}
            for k in dir(meas):
                if k.startswith("_"):
                    continue
                try:
                    v = getattr(meas, k)
                    if not callable(v):
                        data[k] = v
                except Exception:
                    continue
            return data
    
    def write_to_json(self, record, file):
        """Write record to JSON file"""
        file.write(json.dumps(record, default=str) + "\n")
        file.flush()
    
    async def run_device_loop(self, address: str, base_outpath: str,
                             stop_event: threading.Event,
                             data_queue: "queue.Queue" = None,
                             resistance_queue: "queue.Queue" = None,
                             recording_enabled: threading.Event = None):
        """
        Main loop for this device - completely independent from other devices
        """
        # Device-specific output paths
        device_outpath = f"{base_outpath}/device_{self.device_id}"
        
        ftms_outpath = f"{device_outpath}/ftms.ndjson"
        Path(ftms_outpath).parent.mkdir(parents=True, exist_ok=True)
        ftms_f = open(ftms_outpath, "a", encoding="utf-8")
        
        cps_outpath = f"{device_outpath}/cps.ndjson"
        Path(cps_outpath).parent.mkdir(parents=True, exist_ok=True)
        cps_f = open(cps_outpath, "a", encoding="utf-8")
        
        cpvs_outpath = f"{device_outpath}/cpvs.ndjson"
        Path(cpvs_outpath).parent.mkdir(parents=True, exist_ok=True)
        cpvs_f = open(cpvs_outpath, "a", encoding="utf-8")
        
        # Device-specific client connection with retry
        max_retries = 3
        for attempt in range(max_retries):
            client = BleakClient(address)
            try:
                print(f"[{self.device_name}] Connecting to {address} (attempt {attempt + 1}/{max_retries})...")
                await client.connect()
                print(f"[{self.device_name}] Connected to {address}")
                
                # Wait for services to stabilize
                await asyncio.sleep(2.0)
                
                break  # connection successful
            except Exception as e:
                print(f"[{self.device_name}] Connection attempt {attempt + 1} failed: {e}")
                if attempt < max_retries - 1:
                    print(f"[{self.device_name}] Retrying in 3 seconds...")
                    await asyncio.sleep(3.0)
                else:
                    print(f"[{self.device_name}] All connection attempts failed!")
                    return
        
        try:
            
            # Device-specific services (with delay for stable connection)
            await asyncio.sleep(1.0)
            
            ftms = FitnessMachineService(client)
            ftms.set_indoor_bike_data_handler(self.ftms_handler)
            await ftms.enable_indoor_bike_data_notify()
            print(f"[{self.device_name}] FTMS notifications enabled")
            
            cps = CyclingPowerService(client)
            cps.set_cycling_power_measurement_handler(self.cps_handler)
            await cps.enable_cycling_power_measurement_notifications()
            print(f"[{self.device_name}] CPS notifications enabled")
            
            cps.set_cycling_power_vector_handler(self.cpvs_handler)
            await cps.enable_cycling_power_vector_notifications()
            print(f"[{self.device_name}] CPVS notifications enabled")
            
            last_seen_ftms_obj = None
            last_seen_cps_obj = None
            last_seen_cpvs_obj = None
            
            while not stop_event.is_set():
                # Check for resistance commands (non-blocking)
                if resistance_queue is not None:
                    try:
                        resistance_value = resistance_queue.get_nowait()
                        if resistance_value is not None:
                            await self.set_resistance_level(ftms, client, resistance_value)
                    except queue.Empty:
                        pass
                
                # Process data (FTMS has highest priority)
                ftms_meas = self.last_ftms
                cps_meas = self.last_cps
                cpvs_meas = self.last_cpvs
                rec = None
                
                if ftms_meas is not None and ftms_meas is not last_seen_ftms_obj:
                    rec = self.clean_dict(self.convert_to_dict(ftms_meas))
                    print(f"[{self.device_name}] FTMS\n{rec}\n")
                    # Only write to file if recording is enabled
                    if recording_enabled is None or recording_enabled.is_set():
                        self.write_to_json(rec, ftms_f)
                    last_seen_ftms_obj = ftms_meas
                
                elif cpvs_meas is not None and cpvs_meas is not last_seen_cpvs_obj:
                    rec = self.clean_dict(self.convert_to_dict(cpvs_meas))
                    print(f"[{self.device_name}] CPVS\n{rec}\n")
                    # Only write to file if recording is enabled
                    if recording_enabled is None or recording_enabled.is_set():
                        self.write_to_json(rec, cpvs_f)
                    last_seen_cpvs_obj = cpvs_meas
                
                elif cps_meas is not None and cps_meas is not last_seen_cps_obj:
                    rec = self.clean_dict(self.convert_to_dict(cps_meas))
                    print(f"[{self.device_name}] CPS\n{rec}\n")
                    # Only write to file if recording is enabled
                    if recording_enabled is None or recording_enabled.is_set():
                        self.write_to_json(rec, cps_f)
                    last_seen_cps_obj = cps_meas
                
                if rec is not None and data_queue is not None:
                    try:
                        data_queue.put(rec, timeout=0.001)
                    except queue.Full:
                        data_queue.get_nowait()
                        data_queue.put_nowait(rec)
                
                # Publish data to Unity WebSocket server
                if rec is not None:
                    try:
                        import actuators.unity.unity_ws_server as unity_ws_server
                        
                        # Extract and publish speed
                        speed_value = rec.get("instant_speed") or rec.get("speed") or rec.get("instantaneous_speed")
                        if speed_value is not None:
                            unity_ws_server.publish_speed_kmh(float(speed_value))
                        
                        # Extract and publish power
                        power_value = rec.get("instant_power") or rec.get("power") or rec.get("instantaneous_power")
                        if power_value is not None:
                            unity_ws_server.publish_power_watts(float(power_value))
                        
                        # Extract and publish cadence
                        cadence_value = rec.get("instant_cadence") or rec.get("cadence") or rec.get("instantaneous_cadence")
                        if cadence_value is not None:
                            unity_ws_server.publish_cadence_rpm(float(cadence_value))
                        
                        # Extract and publish resistance level (if available)
                        resistance_value = rec.get("resistance_level")
                        if resistance_value is not None:
                            unity_ws_server.publish_resistance_level(int(resistance_value))
                    except Exception as e:
                        # Silently fail to avoid disrupting data collection
                        pass
                
                await asyncio.sleep(0.001)
        
        finally:
            # Cleanup
            try:
                await cps.disable_cycling_power_measurement_notifications()
                await cps.disable_cycling_power_vector_notifications()
                await ftms.disable_indoor_bike_data_notify()
                await client.disconnect()
            except Exception as e:
                print(f"[{self.device_name}] Error during cleanup: {e}")
            
            ftms_f.close()
            cps_f.close()
            cpvs_f.close()
            print(f"[{self.device_name}] Disconnected and files closed")

def run_device_handler(stop_event: threading.Event, device_id: str, device_name: str,
                      address: str, outpath: str, 
                      data_queue: "queue.Queue" = None,
                      resistance_queue: "queue.Queue" = None,
                      recording_enabled: threading.Event = None):
    """
    Thread entry point for a single device handler
    """
    handler = BikeDeviceHandler(device_id, device_name)
    asyncio.run(handler.run_device_loop(address, outpath, stop_event, data_queue, resistance_queue, recording_enabled))
