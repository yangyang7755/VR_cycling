# Experiment Session Checklist

## 1. Pre-Participant Arrival (30 min before)

### Lab Setup
- [ ] Room temperature comfortable (18-22°C) — participant will generate heat cycling
- [ ] Monitor positioned at eye level from bike saddle
- [ ] Keyboard accessible from bike handlebars (for 1-10 pain/reward ratings)
- [ ] Water bottle within reach of participant
- [ ] Towel available for sweat

### Bike Trainer
- [ ] Tacx Neo plugged in via USB-C (prevents deep sleep)
- [ ] Pedal cranks spin freely — no obstructions
- [ ] Saddle height marked/noted for quick adjustment
- [ ] Close ALL fitness apps on phone and computer (Garmin Connect, Zwift, Strava, TrainerRoad)
- [ ] Turn OFF Bluetooth on your phone (or forget the Neo)
- [ ] Pedal 2-3 revolutions to wake the trainer

### Computer
- [ ] Start Python Bluetooth bridge:
  ```bash
  cd bluetooth/sensors/ergometer
  python main.py
  ```
- [ ] Verify "Connected" message appears in terminal
- [ ] Start Python event marker receiver (separate terminal):
  ```bash
  cd Assets/Scripts
  python event_marker_receiver.py
  ```
- [ ] Open Unity project
- [ ] Press Play — verify start screen appears
- [ ] Test simulation mode (F7 → confirm road appears, cyclist moves, coins visible)
- [ ] Stop Play, then re-enter Play for clean state

### EEG (if recording)
- [ ] Amplifier powered on and warmed up (5 min)
- [ ] Recording software open (BrainVision Recorder)
- [ ] LSL stream visible in Recorder (if using LSL markers)
- [ ] Electrode gel, syringes, alcohol wipes, NuPrep ready
- [ ] Impedance check tools ready
- [ ] Cap size selected (measure participant on arrival)

### Data
- [ ] Confirm `Application.persistentDataPath` folder exists and is writable
- [ ] Note participant ID for this session (e.g., P001)
- [ ] Odd number = Reward group (coins), Even number = Control group (no coins)

---

## 2. Participant Arrives (15-20 min)

### Consent & Briefing
- [ ] Participant signs informed consent
- [ ] Explain the experiment:
  - "You'll cycle through 5 virtual hills of different steepness"
  - "Each hill is about 500 metres"
  - "Every 30 seconds a pain rating will pop up — just press a number 1-10"
  - "After each hill you'll rate your pain and how rewarding the experience was"
  - "Keep pedalling throughout — the keyboard is within reach for ratings"
  - (If Reward group): "Gold coins will appear on the road — you collect them by cycling through them. Each coin = 10p"
  - (If Control group): Do NOT mention coins
- [ ] Explain the rating scales:
  - Pain: "1 = no pain, 10 = worst pain imaginable"
  - Reward: "1 = not rewarding at all, 10 = extremely rewarding"
- [ ] Explain they can stop at any time (but encourage completing)

### Bike Fitting
- [ ] Adjust saddle height (slight knee bend at bottom of pedal stroke)
- [ ] Adjust handlebar reach if possible
- [ ] Participant clips in / positions feet on pedals
- [ ] Confirm comfortable position

### EEG Setup (if applicable)
- [ ] Measure head (nasion-inion, pre-auricular)
- [ ] Select and place cap
- [ ] Apply gel to all electrodes
- [ ] Check impedances (target: <10 kΩ wet, <50 kΩ dry)
- [ ] Apply sweatband above electrode line
- [ ] Tape cable bundle to upper back (prevent swinging)
- [ ] Verify signal: ask for blinks, jaw clench, sit still

---

## 3. Pre-Trial (5 min)

### Baseline Recording
- [ ] Start EEG recording
- [ ] Instruct: "Sit still, look at the cross on screen"
- [ ] Eyes-open baseline: 2 minutes
- [ ] Eyes-closed baseline: 2 minutes
- [ ] "You can open your eyes now"

### Familiarisation
- [ ] Unity is at the Start Screen (participant ID entry)
- [ ] Enter participant ID (e.g., P001) and block number (1)
- [ ] Explain: "The hill info will show for 10 seconds, then a 3-2-1 countdown, then start pedalling"
- [ ] Explain: "Press number keys 1-10 when the pain question appears at the bottom"
- [ ] Let participant do 30 seconds of easy pedalling to feel the resistance
- [ ] Confirm participant understands the task

### Final Checks
- [ ] Python bridge still connected (check terminal)
- [ ] Event marker receiver running
- [ ] EEG signal clean (no flat channels)
- [ ] Unity Game view is full screen / focused
- [ ] "Run In Background" enabled (Edit → Project Settings → Player)
- [ ] Keyboard within reach from cycling position

---

## 4. During Trial

### Starting
- [ ] Click Start on the participant entry screen (or participant presses Start)
- [ ] First condition begins automatically (10s hill cue → 3-2-1 → cycling)

### Monitoring (you, the experimenter)
- [ ] Watch Unity Console for errors (red messages)
- [ ] Watch Python terminal for BLE connection status
- [ ] Monitor EEG signal quality periodically
- [ ] Note any interruptions in your lab notebook
- [ ] DO NOT click away from Unity Game view (causes focus loss)
- [ ] If game pauses: click back on Game view — it should auto-resume

### What participant does (per condition)
1. Views hill difficulty cue (10s — sit still for EEG)
2. Hears/sees 3-2-1-GO countdown
3. Pedals the hill (resistance matches gradient)
4. Rates pain every 30s when prompted (press 1-10)
5. Collects coins by cycling through them (Reward group only)
6. Hill completes at 450m (or abandoned if stops for 15s)
7. Rates final pain (black screen, 1-10)
8. Rates reward (black screen, 1-10)
9. Next condition starts automatically

### If Something Goes Wrong
- **Bike disconnects**: Participant pedals to wake trainer, Python auto-reconnects
- **Game freezes**: Click Game view to regain focus
- **Participant needs break**: F6 to skip current condition, note in lab book
- **Participant wants to stop**: Let them stop, note how many conditions completed
- **Unity error/crash**: Note condition number, restart, enter same participant ID + incremented block number

---

## 5. Post-Trial

### Immediate
- [ ] All 5 conditions complete — start screen reappears
- [ ] Post-experiment resting EEG:
  - Eyes open: 2 minutes
  - Eyes closed: 2 minutes
- [ ] Stop EEG recording
- [ ] Tell participant: "You're all done, well done!"
- [ ] (Reward group): Report earnings — check Console log or `participant_earnings.csv`

### Data Backup
- [ ] Stop Python bridge (Ctrl+C in both terminals)
- [ ] Stop Unity (press Stop)
- [ ] Verify data files exist:
  - `participant_earnings.csv` (in persistentDataPath)
  - `hill_experiment_<PID>_B1_<timestamp>.csv` (trial telemetry)
  - `event_markers.csv` (EEG event log)
  - EEG raw files (.eeg/.vhdr/.vmrk)
- [ ] Copy ALL data to backup drive immediately
- [ ] Label backup: `P001_YYYYMMDD`

### Cleanup
- [ ] Remove EEG cap, clean electrodes
- [ ] Wipe bike saddle/handlebars with antibacterial wipe
- [ ] Reset Unity (stop and re-enter Play mode for next participant)
- [ ] Reset Python bridge terminals
- [ ] Offer participant water, debrief if needed
- [ ] Record any notes about the session (technical issues, participant comments)

### Payment (Reward Group)
- [ ] Check `participant_earnings.csv` for final amount
- [ ] Each coin = £0.10, max 25 coins per block = £2.50 max
- [ ] Pay participant (base payment + coin bonus)
- [ ] Record payment in payment log

---

## Quick Reference: Key Commands

| Key | Action |
|-----|--------|
| 1-9, 0 | Rate pain/reward (0 = 10) |
| ← → | Adjust rating |
| Enter | Confirm rating |
| F6 | Skip current condition (experimenter only) |
| F7 | Force start experiment (debug) |
| W/↑ | Increase power (simulation mode only) |
| S/↓ | Decrease power (simulation mode only) |

---

## Quick Reference: File Locations

| File | Path |
|------|------|
| Trial data | `~/Library/Application Support/<Company>/<Product>/hill_experiment_*.csv` |
| Earnings | `~/Library/Application Support/<Company>/<Product>/participant_earnings.csv` |
| Event markers | `~/Library/Application Support/<Company>/<Product>/event_markers.csv` |
| Python events | `Assets/Scripts/unity_events_*.csv` |
| EEG data | Your BrainVision Recorder output folder |
