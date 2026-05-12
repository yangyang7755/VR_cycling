# ftms_reader_save_all.py
import asyncio
import json
import time
import threading
from pathlib import Path
from bleak import BleakClient
from pycycling.fitness_machine_service import FitnessMachineService
from pycycling.cycling_power_service import CyclingPowerService
import queue
from actuators.unity import unity_ws_server

# ---- global slot and handler ----
LAST_FTMS = None
LAST_CPS = None
LAST_CPVS = None

def cpvs_handler(cpvs_meas):
    global LAST_CPVS
    LAST_CPVS = cpvs_meas

def cps_handler(cps_meas):
    global LAST_CPS
    LAST_CPS = cps_meas

def ftms_handler(ftms_meas):
    """Top-level handler called by FitnessMachineService; store latest measurement."""
    global LAST_FTMS
    LAST_FTMS = ftms_meas

# ---- keys we want to extract and write explicitly (from FTMS indoor bike data) ----
FTMS_KEYS = [
    "instant_speed",
    "average_speed",
    "instant_cadence",
    "average_cadence",
    "total_distance",
    "resistance_level",
    "instant_power",
    "average_power",
    "total_energy",
    "energy_per_hour",
    "energy_per_minute",
    "heart_rate",
    "metabolic_equivalent",
    "elapsed_time",
    "remaining_time",
    # other plausible names that parsers sometimes use:
    "speed",
    "power",
    "cadence",
    "instantaneous_speed",
    "instantaneous_power",
    "instantaneous_cadence",
    "average_power",
    "average_cadence",
    "total_energy",
]

# ---- resistance control function ----
async def set_resistance_level(ftms_service, client, resistance_level: int):
    """
    Set bike resistance level via FTMS
    resistance_level: 0-100 (percentage) or device-specific range
    """
    try:
        # Clamp resistance value to valid range
        resistance_level = max(0, min(100, int(resistance_level)))
        
        # Try using pycycling's method if available
        if hasattr(ftms_service, 'set_target_resistance_level'):
            await ftms_service.set_target_resistance_level(resistance_level)
            print(f"Resistance set to: {resistance_level}%")
            return True
        else:
            # Fallback: direct GATT write to FTMS Control Point
            # FTMS Control Point UUID: 0x2AD9
            control_point_uuid = "00002ad9-0000-1000-8000-00805f9b34fb"
            
            # Command format: [Op Code, Parameter]
            # Op Code 0x04 = Set Target Resistance Level
            command = bytes([0x04, resistance_level])
            
            await client.write_gatt_char(control_point_uuid, command)
            print(f"Resistance set to: {resistance_level}%")
            return True
    except Exception as e:
        print(f"Failed to set resistance: {e}")
        return False

# ---- async loop run inside the thread ----
async def ftms_async_loop(address: str, outpath: str, stop_event: threading.Event, data_queue: "queue.Queue", resistance_queue: "queue.Queue" = None):

    # ftms
    ftms_outpath = outpath + "/ftms.ndjson"
    Path(ftms_outpath).parent.mkdir(parents=True, exist_ok=True)
    ftms_f = open(ftms_outpath, "a", encoding="utf-8")

    # cps
    cps_outpath = outpath + "/cps.ndjson"
    Path(cps_outpath).parent.mkdir(parents=True, exist_ok=True)
    cps_f = open(cps_outpath, "a", encoding="utf-8")

    # cpvs
    cpvs_outpath = outpath + "/cpvs.ndjson"
    Path(cpvs_outpath).parent.mkdir(parents=True, exist_ok=True)
    cpvs_f = open(cpvs_outpath, "a", encoding="utf-8")

    client = BleakClient(address)
    await client.connect()
    print("Connected to " + address)

    #ftms
    ftms = FitnessMachineService(client)
    ftms.set_indoor_bike_data_handler(ftms_handler)
    await ftms.enable_indoor_bike_data_notify()
    print("FTMS notifications enabled")

    #cps
    cps = CyclingPowerService(client)
    cps.set_cycling_power_measurement_handler(cps_handler)
    await cps.enable_cycling_power_measurement_notifications()
    print("CPS notifications enabled")

    cps.set_cycling_power_vector_handler(cpvs_handler) # cpvs
    await cps.enable_cycling_power_vector_notifications()
    print("CPS vector notifications enabled")

    last_seen_ftms_obj = None
    last_seen_cps_obj = None
    last_seen_cpvs_obj = None
    try:
        while not stop_event.is_set():
            # Check for resistance control commands from Unity (non-blocking)
            if resistance_queue is not None:
                try:
                    resistance_value = resistance_queue.get_nowait()
                    if resistance_value is not None:
                        await set_resistance_level(ftms, client, resistance_value)
                except queue.Empty:
                    pass
            
            # Read sensor data (FTMS has highest priority)
            cps_meas = LAST_CPS
            ftms_meas = LAST_FTMS
            cpvs_meas = LAST_CPVS
            rec = None
            
            # FTMS has highest priority
            if ftms_meas is not None and ftms_meas is not last_seen_ftms_obj:
                rec = clean_dict(convert_to_dict(ftms_meas))
                print("FTMS\n", rec, "\n")
                write_to_json(rec, ftms_f)
                last_seen_ftms_obj = ftms_meas
                try:
                    # Get speed from record, try multiple possible field names
                    speed_value = rec.get("instant_speed") or rec.get("speed") or rec.get("instantaneous_speed")
                    if speed_value is not None:
                        unity_ws_server.publish_speed_kmh(float(speed_value))
                except Exception:
                    pass

            elif cpvs_meas is not None and cpvs_meas is not last_seen_cpvs_obj:
                rec = clean_dict(convert_to_dict(cpvs_meas))
                print("CPVS\n", rec, "\n")
                write_to_json(rec, cpvs_f)
                last_seen_cpvs_obj = cpvs_meas
                queue_msg = define_queue_message(rec, "cpvs")

            elif cps_meas is not None and cps_meas is not last_seen_cps_obj:
                rec = clean_dict(convert_to_dict(cps_meas))
                print("CPS\n", rec, "\n")
                write_to_json(rec, cps_f)
                last_seen_cps_obj = cps_meas

            if rec is not None:
                try:
                    data_queue.put(rec, timeout=0.001)
                except queue.Full:
                    data_queue.get_nowait()   # drop oldest
                    data_queue.put_nowait(rec)

            await asyncio.sleep(0.001)

    finally:
        await cps.disable_cycling_power_measurement_notifications()
        await cps.disable_cycling_power_vector_notifications()
        await ftms.disable_indoor_bike_data_notify()
        await client.disconnect()
        ftms_f.close()
        cps_f.close()
        cpvs_f.close()

def write_to_json(record, file):
    file.write(json.dumps(record, default=str) + "\n")
    file.flush()

    return 0

def convert_to_dict(meas):
    if isinstance(meas, dict):
        print("place1")
        data = meas
    elif hasattr(meas, "_asdict"):
        try:
            data = meas._asdict()
            #print("place2")
        except Exception:
            data = {}
            print("place3")
    else:
        data = {}
        for k in dir(meas):
            if k.startswith("_"):
                print("place5")
                continue
            try:
                print("place6")
                v = getattr(meas, k)
            except Exception:
                print("place7")
                continue
            if not callable(v):
                print("place8")
                data[k] = v

    return data

def clean_dict(raw_data):
    record = {key: value for key, value in raw_data.items() if value is not None}
    record = {"time": time.time(), **record}
    return record

def define_queue_message(rec, packet_type):
    queue_msg = {"ts": time.time(), "packet_type": packet_type}

    if packet_type == "cpvs":
        if "cumulative_crank_revs" not in rec:
            queue_msg.update({
                "instantaneous_torque_magnitudes": rec.instantaneous_torque_magnitudes,
                "packet_subtype": cpvs_tor,
            })
        else:
            queue_msg.update({
                "first_crank_measurement_angle": rec.first_crank_measurement_angle,
                "cumulative_crank_revs": rec.cumulative_crank_revs,
                "last_crank_event_time": rec.last_crank_event_time,
                "instantaneous_torque_magnitudes": rec.instantaneous_torque_magnitudes,
                "packet_subtype": "cpvs_rev",
            })

    elif packet_type == "cps":
        if "cumulative_crank_revolutions" not in rec:
            queue_msg.update({
            "instantaneous_power": rec.instantaneous_power,
            "cumulative_wheel_revs": rec.cumulative_wheel_revs,
            "last_wheel_event_time": rec.last_wheel_event_time,
            })

        else:
            queue_msg.update({
            "instantaneous_power": rec.instantaneous_power,
            "cumulative_wheel_revs": rec.cumulative_wheel_revs,
            "last_wheel_event_time": rec.last_wheel_event_time,
            "cumulative_crank_revs": rec.cumulative_crank_revs,
            "last_crank_event_time": rec.last_crank_event_time,
            })


    elif packet_type == "ftms":
        queue_msg.update({
            "instantaneous_power": rec.instant_power,
            "instantaneous_cadence": rec.instant_cadence,
            "instantaneous_speed": rec.instant_speed,
        })

    return queue_msg

# ---- thread entry point ----
def run_ftms_reader(stop_event: threading.Event, address: str, outpath: str = "ftms_allfields.ndjson", data_queue: "queue.Queue" = None, resistance_queue: "queue.Queue" = None):
    """
    Thread target: runs until stop_event is set.
    Example:
        stop_evt = threading.Event()
        t = threading.Thread(target=run_ftms_reader, args=(stop_evt, addr, "out.ndjson"))
        t.start()
        ...
        stop_evt.set(); t.join()
    """
    asyncio.run(ftms_async_loop(address, outpath, stop_event, data_queue, resistance_queue))
