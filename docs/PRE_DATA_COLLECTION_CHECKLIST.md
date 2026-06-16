# Pre-Data Collection Debug Checklist

## 1. Bike Ergometer (NEO Bike Plus)

- [ ] Bike powers on, display shows normally
- [ ] No other apps connected (Tacx app, Zwift, Garmin Connect all closed)
- [ ] Bike is NOT in standalone resistance mode (press both buttons to reset to 0%)
- [ ] Python script connects: see `[Bike] Connected to F9:A4:28:75:B1:08`
- [ ] FTMS data flowing: see `[Bike] FTMS` messages with speed/cadence/power
- [ ] Power reading > 0 when pedaling (check Python console shows `instant_power` > 0)
- [ ] **Resistance control works**: see `[Bike] FTMS Control requested ✓` (no 0x81 error)
- [ ] **Resistance feels different**: flat section feels easy, 5%+ feels harder on pedals
- [ ] WebSocket server running: see `WebSocket server listening on ws://0.0.0.0:8765`

## 2. Unity Application

### Connection
- [ ] Unity connects to WebSocket: see `[WebSocketClient] ✓ Connected to WebSocket server`
- [ ] Speed/power/cadence display updates in real-time (left HUD panel)
- [ ] Cyclist moves when pedaling (responsive, no large delay)

### Trial Flow (run one full trial)
- [ ] Trial starter UI appears (participant ID, trial number, date)
- [ ] After clicking Start: condition cue appears (10s, shows gradient %, difficulty bar)
- [ ] Cue transitions to countdown (3-2-1-GO)
- [ ] HillClimb phase starts: cyclist moves, road visible, progress bar appears
- [ ] GRADE display changes from +0.0% to target gradient after ~50m
- [ ] Pain VAS is visible and responsive during climb
- [ ] Coins visible on road (Reward group only)
- [ ] Coins collected when cyclist passes them (counter updates)
- [ ] Hill completes at target distance → rest screen appears
- [ ] Rest period (5s) shows timer countdown + pain VAS
- [ ] Reward question appears after rest
- [ ] Reward VAS slider works → advances to next condition
- [ ] All 9 conditions run sequentially (no skipping)

### Visual
- [ ] Road mesh visible (dark asphalt, not checkered)
- [ ] Road sits ON terrain (not floating or buried under grass)
- [ ] Terrain follows road elevation (slopes up together)
- [ ] Trees grounded to terrain (not floating)
- [ ] Camera follows cyclist smoothly

### Data Logging
- [ ] CSV file created: check `Application.persistentDataPath` for `hill_experiment_*.csv`
- [ ] CSV has correct columns (timestamp, condition, gradient, phase, distance, speed, power, cadence, hr, pain_vas, etc.)
- [ ] Data rows written during HillClimb phase (not empty)
- [ ] Event markers logged (HILL_START, HILL_COMPLETE, etc.)

## 3. EEG (Brain Vision Recorder)

### Hardware
- [ ] Amplifier powered on and connected to recording PC
- [ ] Cap placed, electrodes positioned (Fz, F3, F4, FCz, Cz, C3, C4, Pz minimum)
- [ ] Impedances checked: all channels < 20 kΩ (dry) or < 5 kΩ (gel)
- [ ] Ground (AFz) and reference (linked mastoids or Cz) connected
- [ ] EOG channels placed (if using)

### Software
- [ ] Brain Vision Recorder opens, sees amplifier
- [ ] All channels showing live signal (no flat lines)
- [ ] Signal quality: no 50Hz line noise dominating, alpha visible when eyes closed
- [ ] Sampling rate set: 500 Hz or 1000 Hz
- [ ] Filters configured: high-pass 0.1 Hz, low-pass 100 Hz (or as per protocol)

### Marker Integration
- [ ] EventMarkerSender in Unity is configured with correct IP/port for Brain Vision
- [ ] Send a test marker from Unity (press F7 or start a test trial)
- [ ] Marker appears in Brain Vision Recorder timeline
- [ ] Marker labels are correct (CUE_ONSET, HILL_START, etc.)
- [ ] Marker timing verified: markers align with visual events on screen

### Recording
- [ ] File naming convention set: `{PID}_{date}_EEG.vhdr`
- [ ] Storage location has sufficient disk space
- [ ] Test recording: start/stop, verify file is created and readable

## 4. EMG System

### Hardware
- [ ] EMG amplifier powered on
- [ ] Surface electrodes placed on Vastus Lateralis (outer quad, both legs or dominant)
- [ ] Reference electrode placed (bony prominence, e.g., patella or ankle)
- [ ] Electrodes have good skin contact (clean, abraded, gel applied if needed)
- [ ] Cables secured to prevent movement artifact during pedaling

### Signal Quality
- [ ] Raw EMG signal visible when participant contracts quad
- [ ] Signal quiet at rest (no excessive noise)
- [ ] No cable movement artifacts during pedaling (secure cables with tape)
- [ ] Sampling rate: 1000–2000 Hz
- [ ] Test: ask participant to push against resistance → clear burst visible

### Synchronization
- [ ] EMG system receives same markers as EEG (or is synced via shared trigger)
- [ ] Alternatively: EMG recorded on same amplifier as EEG (Brain Vision supports this)
- [ ] Verify marker appears in EMG recording when Unity sends event

## 5. Heart Rate Monitor

- [ ] Chest strap paired via Bluetooth
- [ ] HR data flowing to Python: see `heart_rate_bpm` in WebSocket telemetry
- [ ] HR displays in Unity HUD (not showing "-- bpm")
- [ ] HR is realistic (60-80 at rest, rises with effort)
- [ ] HR logged in CSV file (not all zeros)

## 6. Full Integration Test (Dry Run)

Run one COMPLETE block (all 9 conditions) with an experimenter as participant:

- [ ] All systems recording simultaneously (EEG, EMG, HR, Unity CSV, bike data)
- [ ] Complete all 9 conditions without crashes or errors
- [ ] Verify timestamps align across systems (markers in EEG match Unity events)
- [ ] Check CSV data file is complete (9 conditions, all columns populated)
- [ ] Check EEG recording has all expected markers in correct sequence
- [ ] Verify no data gaps (continuous recording throughout)
- [ ] Total duration reasonable (~60-75 min for 9 conditions)
- [ ] All files saved and accessible after session

## 7. Participant Experience Check

- [ ] Instructions are clear (pain vs effort distinction explained)
- [ ] VAS slider is easy to use while pedaling
- [ ] Screen is visible from cycling position
- [ ] Difficulty cue is readable (text size, contrast)
- [ ] Reward VAS question is understood
- [ ] Bike seat height is adjustable
- [ ] Emergency stop procedure works (verbal request or stop pedaling for 10s)

## 8. Day-of Checklist (Before Each Participant)

- [ ] All computers booted, software open
- [ ] Python script running (WebSocket server active)
- [ ] Brain Vision Recorder in standby mode
- [ ] EMG system ready
- [ ] Fresh CSV filename set (new participant ID)
- [ ] EEG file naming updated for this participant
- [ ] Bike powered on, resistance at 0% (no other apps)
- [ ] Unity in Play mode, showing trial starter screen
- [ ] Room temperature comfortable
- [ ] Water available for participant


## 9. Voice Rating System (Soundtrack / Azure Speech)

### Setup
- [ ] Azure subscription key is valid and not expired (check `Soundtrack/Resources/`)
- [ ] Network config (`Resources/network-config.json`) points to correct PainLab server IP/port
- [ ] Microphone connected and selected as input device
- [ ] Directional microphone positioned toward participant's face (away from bike noise)

### Testing
- [ ] Run `Soundtrack/Program.cs` — see "Speech recognition setup complete"
- [ ] Send "continuous" command to start listening
- [ ] Say each number 0-10 clearly — confirm each is recognized correctly in console
- [ ] Test with bike running (pedaling noise, fan) — confirm recognition still works
- [ ] Confirm `recognition_val` is sent to PainLab server (check server logs)
- [ ] Confirm timestamps are logged (can be aligned with Unity event markers)

### During Experiment
- [ ] Start Soundtrack app BEFORE beginning trials
- [ ] Set to "continuous" recognition mode
- [ ] Experimenter prompts at fixed intervals: "Rate your pain" (every 20-30s during climb)
- [ ] Experimenter prompts after rest: "Rate how rewarding that was"
- [ ] Verbal responses logged with timestamps matching EEG markers
- [ ] Verify no false positives from breathing/grunting during hard effort

### Backup
- [ ] If voice recognition fails (noise, accent issues), fall back to VAS slider
- [ ] Consider recording raw audio as backup for manual coding later
