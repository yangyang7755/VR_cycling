# unity_ws_server.py
import asyncio
import json
import threading
import time
import queue
from pathlib import Path
from typing import Dict, Any, Set
import websockets   # pip install websockets

# Shared state for telemetry (in-memory) and lock for thread-safety
shared_state: Dict[str, Any] = {
    "speed_kmh": None,
    "power_watts": None,
    "cadence_rpm": None,
    "heart_rate_bpm": None,  # Heart rate in beats per minute
    "resistance_level": None,
    "speed_timestamp": None,
    "speed_monotonic": None,
}
_shared_lock = threading.Lock()

# connected clients
_clients: Set[websockets.WebSocketServerProtocol] = set()

# Resistance command queue (set by aggregator)
_resistance_queue = None
_resistance_queue_lock = threading.Lock()

PUBLISH_HZ = 25.0
PUBLISH_INTERVAL = 1.0 / PUBLISH_HZ

async def _register(ws):
    _clients.add(ws)

async def _unregister(ws):
    _clients.discard(ws)

async def _ws_handler(ws):
    """Handle WebSocket connection (websockets v16+ API - no path argument)"""
    print(f"[WebSocket] New client connected from {ws.remote_address}")
    await _register(ws)
    try:
        # Keep connection alive by waiting for messages with a timeout
        # This prevents the handler from exiting immediately when no messages are sent
        while True:
            try:
                # Wait for message with timeout to keep connection alive
                message = await asyncio.wait_for(ws.recv(), timeout=1.0)
                
                try:
                    # Parse incoming message from Unity
                    data = json.loads(message)
                    
                    # Handle resistance control command
                    if data.get("type") == "set_resistance":
                        resistance_value = data.get("resistance", 0)
                        # Validate and clamp resistance value (0-100)
                        try:
                            resistance_value = max(0, min(100, int(resistance_value)))
                            
                            # Put command into queue for bike handler
                            with _resistance_queue_lock:
                                if _resistance_queue is not None:
                                    try:
                                        _resistance_queue.put_nowait(resistance_value)
                                        print(f"[WebSocket] Resistance command queued: {resistance_value}")
                                    except queue.Full:
                                        try:
                                            _resistance_queue.get_nowait()
                                            _resistance_queue.put_nowait(resistance_value)
                                            print(f"[WebSocket] Resistance command queued (replaced): {resistance_value}")
                                        except queue.Empty:
                                            pass
                                else:
                                    print(f"[WebSocket] WARNING: Resistance queue is None! Command {resistance_value} dropped.")
                            
                            # Send confirmation message back to Unity
                            response = {
                                "type": "resistance_set",
                                "resistance": resistance_value,
                                "status": "success"
                            }
                            await _safe_send(ws, json.dumps(response))
                        except (ValueError, TypeError):
                            # Invalid resistance value
                            response = {
                                "type": "resistance_set",
                                "status": "error",
                                "message": "Invalid resistance value"
                            }
                            await _safe_send(ws, json.dumps(response))
                            
                except json.JSONDecodeError:
                    print(f"[WebSocket] Invalid JSON message from client: {message}")
                except Exception as e:
                    print(f"[WebSocket] Error processing message: {e}")
                    
            except asyncio.TimeoutError:
                # No message received in timeout period - this is normal
                # Just continue the loop to keep connection alive
                continue
            except websockets.ConnectionClosed:
                print(f"[WebSocket] Client disconnected normally")
                break
                
    except Exception as e:
        print(f"[WebSocket] Error in handler: {e}")
        import traceback
        traceback.print_exc()
    finally:
        print(f"[WebSocket] Client connection closed, unregistering")
        await _unregister(ws)

async def _safe_send(ws, payload):
    try:
        await ws.send(payload)
    except Exception:
        await _unregister(ws)

async def _broadcaster_loop():
    """Periodically broadcast latest telemetry to all connected clients."""
    while True:
        with _shared_lock:
            msg = {
                "type": "telemetry",
                "speed_kmh": shared_state.get("speed_kmh"),
                "power_watts": shared_state.get("power_watts"),
                "cadence_rpm": shared_state.get("cadence_rpm"),
                "heart_rate_bpm": shared_state.get("heart_rate_bpm"),
                "resistance_level": shared_state.get("resistance_level"),
                "speed_timestamp": shared_state.get("speed_timestamp"),
                "speed_monotonic": shared_state.get("speed_monotonic"),
            }
        payload = json.dumps(msg)
        if _clients:
            # fire-and-forget safe sends (gather avoids raising on first failure)
            await asyncio.gather(*[_safe_send(c, payload) for c in list(_clients)], return_exceptions=True)
        await asyncio.sleep(PUBLISH_INTERVAL)

async def _stopper(wait_event: threading.Event, loop: asyncio.AbstractEventLoop):
    """Coroutine that runs in loop; returns when wait_event is set and stops the loop."""
    # run blocking wait in executor so loop isn't blocked
    await asyncio.get_event_loop().run_in_executor(None, wait_event.wait)
    # stop the loop
    loop.stop()

def start_ws_server(stop_event: threading.Event, host: str = "0.0.0.0", port: int = 8765):
    """
    Start websocket server in a new thread. The function blocks in that thread until stop_event is set.
    Call this via threading.Thread(target=unity_ws_server.start_ws_server_thread, args=(stop_event,))
    """
    # Create and run a fresh asyncio loop in this thread
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)

    async def _main():
        server = await websockets.serve(_ws_handler, host, port)
        # start broadcaster
        loop.create_task(_broadcaster_loop())
        # start stopper that waits for the threading.Event to be set
        loop.create_task(_stopper(stop_event, loop))
        print(f"WebSocket server listening on ws://{host}:{port}")
        return server

    server = loop.run_until_complete(_main())
    try:
        loop.run_forever()
    finally:
        # cleanup: close server and loop
        server.close()
        loop.run_until_complete(server.wait_closed())
        pending = asyncio.all_tasks(loop=loop)
        for t in pending:
            t.cancel()
        try:
            loop.run_until_complete(asyncio.gather(*pending, return_exceptions=True))
        except Exception:
            pass
        loop.close()
        print("WebSocket server stopped")

# helper for other threads to publish speed
def publish_speed_kmh(speed_kmh: float):
    """Thread-safe publish of the latest speed value."""
    now_wall = time.time()
    now_mon = time.monotonic()
    with _shared_lock:
        shared_state["speed_kmh"] = float(speed_kmh) if speed_kmh is not None else None
        shared_state["speed_timestamp"] = now_wall
        shared_state["speed_monotonic"] = now_mon

def publish_power_watts(power_watts: float):
    """Thread-safe publish of the latest power value."""
    with _shared_lock:
        shared_state["power_watts"] = float(power_watts) if power_watts is not None else None

def publish_cadence_rpm(cadence_rpm: float):
    """Thread-safe publish of the latest cadence value."""
    with _shared_lock:
        shared_state["cadence_rpm"] = float(cadence_rpm) if cadence_rpm is not None else None

def publish_resistance_level(resistance_level: int):
    """Thread-safe publish of the latest resistance level."""
    with _shared_lock:
        shared_state["resistance_level"] = int(resistance_level) if resistance_level is not None else None

def publish_heart_rate_bpm(heart_rate_bpm: float):
    """Thread-safe publish of the latest heart rate value."""
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
                "hypothesisId": "D"
            }
            with open(log_path, "a", encoding="utf-8") as f:
                f.write(json.dumps(log_entry) + "\n")
        except Exception:
            pass
    
    with _shared_lock:
        old_value = shared_state.get("heart_rate_bpm")
        shared_state["heart_rate_bpm"] = float(heart_rate_bpm) if heart_rate_bpm is not None else None
        debug_log("unity_ws_server.py:publish_heart_rate_bpm", "HR value updated in shared state", {
            "old_value": old_value,
            "new_value": shared_state["heart_rate_bpm"]
        })

def set_resistance_queue(resistance_queue):
    """Set the resistance command queue for receiving commands from Unity."""
    global _resistance_queue
    with _resistance_queue_lock:
        _resistance_queue = resistance_queue
