# Developer Testing & Debug Guide

This document maps each phase of the experimental protocol to the exact code, scripts, and console outputs a developer needs to verify.

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        UNITY (Game Engine)                        │
│                                                                   │
│  HillClimbExperiment.cs ─── Controls trial flow & phases         │
│  CurvedRouteGenerator.cs ── Generates road, terrain, elevation   │
│  BikeController.cs ──────── Moves cyclist from bike data         │
│  WebSocketClient.cs ──────── Receives data from Python           │
│  EventMarkerSender.cs ───── Sends EEG markers                   │
│  ElevationProfileBar.cs ─── Progress bar UI                      │
│  PersistentPainVAS.cs ───── Pain slider                          │
│                                                                   │
└──────────────────────────┬────────────────────────────────────────┘
                           │ WebSocket (ws://localhost:8765)
┌──────────────────────────▼────────────────────────────────────────┐
│                     PYTHON (bluetooth/)                            │
│                                                                   │
│  main.py ──────────────── Entry point                            │
│  aggregator.py ────────── Device management & coordination       │
│  multi_device_handler.py ─ BLE connection & FTMS control         │
│  unity_ws_server.py ────── WebSocket server (data + resistance)  │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

---

## How to Run (Step by Step)

### 1. Start Python (Bluetooth + WebSocket server)
```bash
cd bluetooth
source venv/bin/activate
python main.py
```
**Expected output:**
```
Starting experiment...
Enter duration (default: 0 for manual stop): 0
[Select device type: 1=Bike, 2=HR, 3=Both]
[Bike] Connected to F9:A4:28:75:B1:08
[Bike] FTMS notifications enabled
[Bike] CPS notifications enabled
WebSocket server listening on ws://0.0.0.0:8765
```

### 2. Start Unity (Press Play)
**Expected console output:**
```
[WebSocketClient] Connecting to ws://localhost:8765...
[WebSocketClient] ✓ Connected to WebSocket server
[HillExperiment] Ready. 5 conditions.
```

### 3. Start Trial (Click Start button in UI)
**Expected:**
```
[StartScreenUI] Started HillClimbExperiment: P001, Block 1
[HillExperiment] Order (sequential): Flat_0pct, Steady_1pct, Steady_3pct, Steady_5pct, Steady_8pct
```

---

## Phase-by-Phase Testing

### Phase 1: EEG Baseline (manual — not controlled by Unity)
- **What happens**: Experimenter manually starts Brain Vision recording
- **Duration**: 5 minutes (2.5 min eyes closed + 2.5 min eyes open)
- **Unity state**: Trial starter UI visible, waiting for Start click
- **Verify**: EEG recording is running, impedances OK

---

### Phase 2: Hill Cue (10 seconds)

**Code**: `HillClimbExperiment.cs` → `ShowHillCue()` + `SetPhase(Phase.HillCue)`

**What should happen on screen:**
- Full black background with difficulty visualization
- Shows: gradient %, difficulty label (EASY/HARD), hill silhouette, difficulty bar
- Condition counter (e.g., "CONDITION 1 / 5")
- Coins display for Reward group

**Console output to verify:**
```
[HillCue] Waiting... 0.3s / 10s
[HillCue] Waiting... 1.0s / 10s
...
[HillExperiment] HillCue complete → Countdown
```

**What can go wrong:**
| Symptom | Cause | Fix |
|---------|-------|-----|
| Timer stuck at 0.0s | `Time.timeScale = 0` | Check `StartBlock()` sets `Time.timeScale = 1f` |
| Cue never appears | `experimentRunning = false` | Check `StartBlock()` was called |
| Black screen but no text | `cueText` reference null | Check CuePanel has CueText child in Hierarchy |

**EEG marker sent:** `CUE_ONSET` (with condition, gradient, rolling info)

**Bike state:** LOCKED (`bikeController.LockBike()`) — no movement even if pedaling

---

### Phase 3: Countdown (4 seconds)

**Code**: `HillClimbExperiment.cs` → `SetPhase(Phase.Countdown)` → Update loop shows 3-2-1-GO

**What should happen:**
- Cue panel shows "3" → "2" → "1" → "GO!"
- Each number shows for 1 second
- After "GO!" (4s total), transitions to HillClimb

**Console output:**
```
[CountdownStart marker sent]
```

**What can go wrong:**
| Symptom | Cause | Fix |
|---------|-------|-----|
| Countdown doesn't advance | `Time.timeScale = 0` | Same fix as above |
| Bike moves during countdown | `LockBike()` not called | Check `SetPhase(Phase.Countdown)` calls `LockBike()` |

**EEG marker sent:** `COUNTDOWN_START`

**Bike state:** LOCKED

---

### Phase 4: Hill Climb (2-5 minutes)

**Code**: `HillClimbExperiment.cs` → `StartHillClimb()` + `UpdateHillClimb()`

**What should happen:**
- Black overlay disappears, cycling scene visible
- Road mesh visible with correct gradient
- Cyclist moves when pedaling
- HUD shows speed, power, cadence, grade, distance
- Progress bar visible at bottom
- Pain VAS visible (top right)
- Coins on road (Reward group)
- Grade changes from +0.0% to target after ~50m (flat lead-in)

**Console output (every 2 seconds):**
```
[BikeController] WebSocket data → Power:150W, Cadence:90rpm, Speed:21km/h, Connected:True, CurrentSpeed:5.8m/s, Dist:125m
[BikeController] RESISTANCE SENT → 55 (slope=5.0%, dist=125m)
[HillClimb] dist=125.0, phaseStart=0.0, hillDist=125.0, hillLength=450.0
```

**Python console (every 0.5s when resistance changes):**
```
[WebSocket] Resistance command queued: 55
[Bike] FTMS Control requested ✓
[Bike] Simulation: grade=5.0% (raw=55)
```

**What can go wrong:**
| Symptom | Cause | Fix |
|---------|-------|-----|
| Cyclist doesn't move | `bikeLocked` still true | Check `UnlockBike()` called in `StartHillClimb()` |
| No road visible | Road mesh outside terrain bounds | Check console for "Fallback: collected X points" |
| Road under terrain | Road Y offset too small | Increase offset in `GenerateRoadMesh()` |
| Speed=0 but pedaling | WebSocket disconnected / Power=0 | Check Python is running, check speed mode is `DirectFromTrainer` |
| No resistance felt | 0x81 FTMS error | Power cycle bike, close other BLE apps |
| Coins not visible | Coins at wrong height | Check coin Y positioning uses terrain height |
| Progress bar not showing | `ElevationProfileBar.Show()` not called | Check `SetPhase(Phase.HillClimb)` calls `profileBar.Show()` |
| Grade stays at 0% | Bike hasn't passed 50m flat lead-in | Wait for distance > 50m |

**EEG markers sent:**
- `HILL_START` (once, at phase start)
- `EEG_EPOCH` (every 30s, with pain VAS value)
- `COIN_COLLECTED` (each coin pickup)
- `HILL_COMPLETE` (when distance reaches target)

**Bike state:** UNLOCKED, resistance commands sent every 0.5s

---

### Phase 5: Post-Climb Rest + Reward Rating (5 seconds + until response)

**Code**: `HillClimbExperiment.cs` → `ShowCompletion()` → then `SetPhase(Phase.RewardQuestion)`

**What should happen:**
- "Hill Complete!" or "Hill Incomplete" message
- Pain VAS still visible
- Countdown timer (5s)
- Then reward question: "How rewarding was this trial?"
- Then reward VAS slider
- After participant responds → next condition starts

**Console output:**
```
[HillExperiment] Hill COMPLETED at 500m
[EEG] POST_CLIMB_REST_START
[EEG] POST_CLIMB_REST_END
[EEG] REWARD_QUESTION_ONSET
[EEG] REWARD_SLIDER_ONSET
```

**What can go wrong:**
| Symptom | Cause | Fix |
|---------|-------|-----|
| Stuck on rest screen | Timer not advancing | Check `Time.timeScale = 1` |
| Reward VAS not responding | VAS slider not wired up | Check `rewardVAS` reference in Inspector |
| Skips conditions | `FinishCondition()` called multiple times | Check `finishingCondition` guard |
| Never advances past reward | VAS response count not incrementing | Check VAS component is active |

**EEG markers sent:** `POST_CLIMB_REST_START`, `POST_CLIMB_REST_END`, `REWARD_QUESTION_ONSET`, `REWARD_SLIDER_ONSET`

---

## Condition Progression

After each condition completes (reward VAS submitted):
```
[HillExperiment] Condition 1 done: Flat_0pct
→ Generates new route for next condition
→ Shows next Hill Cue
→ Repeats Phase 2-5
```

**5 conditions per block (sequential):**
1. Flat 0% → 2. Steady 1% → 3. Steady 3% → 4. Steady 5% → 5. Steady 8%

**4 blocks total** (restart from condition 1 each block)

After all 5 conditions in a block:
```
[HillExperiment] Block complete!
```

---

## Key Inspector Settings to Verify

### On CurvedRouteGenerator GameObject:
- `Generate On Start`: **false** (trial system controls generation)
- `Visual Gradient Multiplier`: **8** (makes hills look steep)
- `Paint Road Texture`: **false** (prevents checkered terrain)
- `Custom Road Material`: empty or valid material (not "Missing")

### On BikeController GameObject:
- `Speed Mode`: **DirectFromTrainer** (responsive movement)
- `Simulation Mode`: **false** for real bike, **true** for testing without bike
- `Enable Resistance Control`: **true**
- `Flat Resistance`: **30**
- `Resistance Per Percent`: **5**
- `WebSocket Client`: assigned

### On HillClimbExperiment GameObject:
- `Route Generator`: assigned to CurvedRouteGenerator
- `Bike Controller`: assigned
- `Pain VAS`: assigned
- `Reward VAS`: assigned

---

## Common Full-System Test Scenarios

### Test 1: Simulation Mode (no bike needed)
1. Set BikeController → `Simulation Mode = true`
2. Press Play → Click Start Trial
3. Use Arrow Up to increase power
4. Verify: cyclist moves, road visible, progress bar, coins, completion

### Test 2: Real Bike Connection
1. Start Python first (`python main.py`)
2. Verify: `[Bike] Connected`, `WebSocket server listening`
3. Press Play in Unity
4. Verify: `[WebSocketClient] ✓ Connected`
5. Pedal → verify speed/power updating in HUD
6. Start trial → verify resistance changes on hills

### Test 3: Full Block (all 5 conditions)
1. Run Test 1 or Test 2
2. Complete all 5 conditions without stopping
3. Verify: no skipped conditions, no crashes
4. Check CSV output file has data for all 5 conditions
5. Check EEG markers appear in correct sequence

### Test 4: EEG Marker Verification
1. Start Brain Vision Recorder
2. Configure EventMarkerSender IP/port in Unity
3. Run a trial
4. Verify markers appear in BV Recorder timeline
5. Check timing: CUE_ONSET should be exactly at cue screen appearance

---

## File Locations

| What | Where |
|------|-------|
| Unity CSV data | `Application.persistentDataPath/hill_experiment_*.csv` |
| Python bike data | `bluetooth/device_1/ftms.ndjson` |
| Event markers | Sent via TCP to EEG system (EventMarkerSender) |
| Conditions config | `HillClimbExperiment.cs` → `ResetToDefaultConditions()` |
| Bluetooth config | `bluetooth/aggregator.py` → `known_bike_address` |
| Python dependencies | `bluetooth/requirements.txt` |

---

## Quick Troubleshooting Reference

| Problem | First thing to check |
|---------|---------------------|
| Nothing happens after Start | Is `experimentRunning = true`? Check console for StartBlock |
| Timer stuck | `Time.timeScale` must be 1.0 |
| Bike doesn't move | Is it locked? Check phase. Is WebSocket connected? |
| No road | Check console for "road points" count. Check terrain reference |
| No resistance | Check Python for 0x81 error. Power cycle bike |
| Conditions skip | Check `finishingCondition` guard. Check console for double FinishCondition |
| Coins don't collect | Check distance comparison includes flat approach (50m offset) |
| Progress bar missing | Check `ElevationProfileBar.Show()` is called, check `FindObjectOfType(true)` |
