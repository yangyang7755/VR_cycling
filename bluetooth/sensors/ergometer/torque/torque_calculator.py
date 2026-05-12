import asyncio
import queue
import threading
import time
import math
from collections import deque
from typing import Optional, Callable, Dict, Any

# Constants
QUEUE_TIMEOUT = 0.01
REV_TIME_HISTORY = 10
MIN_REV_TIME = 0.2    # seconds
MAX_REV_TIME = 5.0    # seconds

async def torque_estimator_async_loop(stop_event: threading.Event, data_queue: "queue.Queue"):
    current_revolution_torque_arrays = deque(maxlen=16)
    last_rev_timestamp = None
    last_rev_counter = None
    rev_time_history = deque(REV_TIME_HISTORY)

    while not stop_event.is_set():
        try:
            rec = data_queue.get(timeout=QUEUE_TIMEOUT)
        except Exception:
            await asyncio.sleep(0)
            continue

        if rec is None:
            await asyncio.sleep(0)
            continue

        pkt_type = rec.get("packet_type")
        pkt_subtype = rec.get("packet_subtype")

        if pkt_type == "cpvs":
            if pkt_subtype == "cpvs_rev":
                anchor = rec
                anchor_timestamp = rec.get("time")
                curr_rev_counter = rec.get("cumulative_crank_revs")
                first_angle = rec.get("first_crank_measurement_angle")
                anchor_torque_array = rec.get("instantaneous_torque_magnitudes", [])

                combined_arrays = list(current_revolution_torque_arrays) + [anchor_torque_array]

                total_samples = sum(len(a) for a in combined_arrays)
                if total_samples == 0:
                    if last_rev_timestamp is not None:
                        rev_dt = anchor_timestamp - last_rev_timestamp
                        if MIN_REV_TIME < rev_dt < MAX_REV_TIME:
                            rev_time_history.append(rev_dt)
                    last_rev_time = anchor_timestamp
                    last_rev_counter = curr_rev_counter
                    continue
                
                flat_torques = []

                for arr in combined_arrays:
                    flat_torques.extend(arr)

                n = len(flat_torques)
                anchored_first_index = n - len(current_revolution_torque_arrays)

                step_degrees = 360 / n
                angles = []

                for i in range(n):
                    delta_from_anchor = i - anchored_first_index
                    ang = (first_angle + delta_from_anchor * step_degrees) % 360
                    angles.append(ang)

                peak_idx = max(range(n), key=lambda i: flat_torques[i])
                peak_torque = flat_torques[peak_idx]
                peak_angle = angles[peak_idx]

                if last_rev_time is not None:
                    delta_revs = curr_rev_counter - last_rev_counter
                    if delta_revs < 0: 
                        delta = 1
                    rev_dt = (anchor_timestamp - last_rev_time) / delta_revs
                    if MIN_REV_TIME < rev_dt < MAX_REV_TIME:
                        rev_time_history.append(rev_dt)
                    
                last_rev_timestamp = anchor_timestamp
                last_rev_counter = curr_rev_counter

                if (len(rev_time_history) > 0):
                    rev_period = sum(rev_time_history) / len(rev_time_history)
                else:
                    rev_period = 1

                angular_velocity = 360 / rev_period

                # Predict when the next peak angle will occur
                anchor_angle = first_angle
                delta_angle = (peak_angle - anchor_angle) % 360
                time_until_peak = delta_angle / angular_velocity 
                if time_until_peak is None:
                    predicted_time = anchor_timestamp + rev_period
                else:
                    predicted_time = anchor_timestamp + time_until_peak

                stim_msg = {
                    "cmd": "stim",
                    "stim_time": predicted_time,
                    "stim_angle": float(peak_angle),
                    "stim_peak_torque": float(peak_torque),
                    "revolution_number": int(curr_rev_counter+1),
                    "anchor_time": anchor_timestamp,
                    "anchor_angle": float(anchor_angle),
                    "rev_period_estimate": rev_period,
                    "timestamp": time.time(),
                }


            else: # pkt_subtype == "cpvs_tor"
                arr = rec.get("instantaneous_torque_magnitudes", [])
                if arr: 
                    current_revolution_torque_arrays.append(arr)
                    if len(current_revolution_torque_arrays) > 20:
                        current_revolution_torque_arrays.popleft()
        else:
            cadence = rec.get("instantaneous_cadence")
            if cadence:
                try:
                    rev_period = 60.0 / float(cadence)
                    if MIN_REV_TIME < rev_period < MAX_REV_TIME:
                        rev_time_history.append(rev_period)
                except Exception:
                    pass

        await asyncio.sleep(0.0)

def _angles_for_array(first_angle_deg: float, n_samples: int) -> list:
    """Return angle (deg) for each sample assuming equal spacing around crank (wraps at 360)."""
    if n_samples <= 0:
        return []
    # assume samples span a contiguous arc; simplest assumption: evenly spaced covering 360/n each
    step = 360.0 / n_samples
    return [ (first_angle_deg + i * step) % ANGULAR_WRAP for i in range(n_samples) ]
            

def run_torque_estimator(stop_event: threading.Event, data_queue: "queue.Queue" = None):
    """
    Thread target: runs until stop_event is set.
    Example:
        stop_evt = threading.Event()
        t = threading.Thread(target=run_ftms_reader, args=(stop_evt, addr, "out.ndjson"))
        t.start()
        ...
        stop_evt.set(); t.join()
    """
    asyncio.run(torque_estimator_async_loop(stop_event, data_queue))