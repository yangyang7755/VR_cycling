# Experimental Protocol: EEG Correlates of Pain, Effort, and Reward During Virtual Hill Cycling

## Overview

This experiment investigates how the brain encodes **nociceptive muscle pain**, **physical effort**, and **reward valuation** during graded virtual hill cycling. Participants ride a stationary bike ergometer connected to a virtual environment that simulates hill climbs at varying gradients. EEG, physiological measures, and subjective ratings are recorded simultaneously.

---

## Study Design

| Parameter | Detail |
|-----------|--------|
| Design | Within-subjects, 2 groups (Reward vs. Control) |
| N conditions | 9 (graded gradients × steady/rolling profiles) |
| Trials per session | 9 (one block) |
| Sessions | 1-2 per participant |
| Group assignment | Odd participant # → Reward (coins visible), Even → Control |
| Duration | ~75-90 minutes total |

---

## Equipment Setup

```
┌─────────────────────────────────────────────────┐
│                   PARTICIPANT                     │
│                                                   │
│   ┌─────────┐    ┌──────────┐    ┌───────────┐  │
│   │   EEG   │    │   Bike   │    │  Display  │  │
│   │  8-16ch │    │ Ergometer│    │  Monitor  │  │
│   │ dry cap │    │(Bluetooth)│   │ (virtual  │  │
│   └────┬────┘    └────┬─────┘    │  cycling) │  │
│        │              │           └─────┬─────┘  │
└────────┼──────────────┼─────────────────┼────────┘
         │              │                 │
    ┌────▼────┐    ┌────▼─────┐     ┌────▼─────┐
    │   EEG   │    │ WebSocket│     │  Unity   │
    │ Amp/PC  │    │  Server  │     │   App    │
    │(markers)│◄───┤(Python)  │◄────┤(game +   │
    └─────────┘    └──────────┘     │ markers) │
                                    └──────────┘
```

### Hardware
- Bike ergometer with Bluetooth (power, cadence, speed, HR)
- EEG system: 8-16 channels (minimum: Fz, F3, F4, FCz, Cz, C3, C4, Pz)
- Display monitor (participant faces screen while cycling)
- Computer running Unity virtual cycling environment

### Software
- Unity application (CurvedRouteGenerator + HillClimbExperiment)
- Python WebSocket server (bluetooth/sensors/ergometer/)
- EEG recording software with LSL/TCP marker input
- EventMarkerSender (Unity → EEG system sync)

---

## Conditions

| # | Condition | Gradient | Profile | Expected Pain | Coins (Reward group) |
|---|-----------|----------|---------|---------------|---------------------|
| 1 | Flat_0pct | 0% | Steady | None (control) | 0 |
| 2 | Steady_2pct | 2% | Steady | Low | 2 |
| 3 | Rolling_2pct | 2% | Rolling | Low (variable) | 2 |
| 4 | Steady_5pct | 5% | Steady | Moderate | 4 |
| 5 | Rolling_5pct | 5% | Rolling | Moderate (variable) | 4 |
| 6 | Steady_8pct | 8% | Steady | High | 6 |
| 7 | Rolling_8pct | 8% | Rolling | High (variable) | 6 |
| 8 | Steady_10pct | 10% | Steady | Very high | 8 |
| 9 | Rolling_10pct | 10% | Rolling | Very high (variable) | 8 |

**Presentation order**: Randomized per participant (Latin square seeded by participant ID + block number)

---

## Session Timeline

```
Total: ~75-90 minutes

├── Setup & Calibration (15 min)
│   ├── EEG cap placement & impedance check
│   ├── Bike fitting & ergometer pairing
│   ├── Familiarization ride (2 min, flat)
│   └── VAS training (practice pain & effort ratings)
│
├── Resting Baseline (5 min)
│   ├── Eyes-open baseline (2 min) ← EEG: BASELINE_EO
│   └── Eyes-closed baseline (2 min) ← EEG: BASELINE_EC
│
├── Experimental Block (50-65 min, 9 conditions)
│   └── [Condition 1] → [Condition 2] → ... → [Condition 9]
│
├── Post-Experiment (5 min)
│   ├── Final resting EEG (2 min)
│   └── Debrief questionnaire
│
└── Total EEG recording: ~70 min
```

---

## Single Condition Timeline (Detailed)

Each of the 9 conditions follows this exact sequence:

```
Time ─────────────────────────────────────────────────────────────────────►

│◄── Phase 1 ──►│◄── 2 ──►│◄──── Phase 3: Hill Climb ────►│◄── Phase 4 ──►│◄─ 5 ─►│◄─ 6 ─►│
│    Hill Cue    │Countdown│         Active Cycling          │ Post-Climb   │Reward │Reward │
│   (10 sec)    │ (4 sec) │        (2-5 min)                │ Rest (30s)   │ Q (5s)│Rating │
│               │         │                                  │              │       │       │

EEG:  BASELINE     COUNT    HILL_START ──── EPOCHS ──── END   REST_START    RQ_ON   RS_ON
      (no move)   DOWN     (pedaling, pain building)          (no move,     (decision)
                                                               pain fading)
```

---

### Phase 1: Hill Cue (10 seconds)
**Purpose**: EEG baseline + anticipatory response to difficulty

```
┌─────────────────────────────────────────┐
│                                         │
│          CONDITION 3 / 9                │
│                                         │
│              5%                         │
│          CHALLENGING                    │
│                                         │
│     ████████████████████                │
│    ███████████████████                  │
│   ██████████████████                    │
│  █████████████████                      │
│  ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔                │
│                                         │
│  DIFFICULTY  ■■■■■□□□□□□□              │
│                                         │
│  Profile: Steady   Distance: 450m       │
│  Climb: +23m                            │
│                                         │
│              🪙 × 4                     │
│         (Reward group only)             │
└─────────────────────────────────────────┘
```

- **Screen**: Black background, difficulty visualization
- **Bike**: Locked (no pedaling possible)
- **Participant**: Sits still, views upcoming challenge
- **EEG markers**: `CUE_ONSET` (start), includes gradient/condition info
- **Analysis window**: Clean artifact-free baseline for spectral comparison

### Phase 2: Countdown (4 seconds)
**Purpose**: Motor preparation

```
    3... 2... 1... GO!
```

- **Screen**: Countdown overlay, then transitions to cycling view
- **Bike**: Unlocked at "GO!"
- **EEG marker**: `COUNTDOWN_START`

### Phase 3: Hill Climb (2-5 minutes depending on speed)
**Purpose**: Primary data collection — pain, effort, reward processing during cycling

```
┌────────────────────────────────────────────────────────┐
│  SPEED   13.5 km/h     Block 1 — 1/9    PAIN ── ──   │
│  POWER   150 W         Steady 5pct (5%)          1    │
│  CADENCE  90 rpm       Phase: HillClimb               │
│  HR      145 bpm       Dist: 125m / 450m   3 4 5 6 7 │
│  GRADE   +5.0%                                        │
│  DIST    125 m                     🪙 0/4             │
│                                                        │
│            [Virtual cycling scene with                  │
│             curved road, trees,                        │
│             coins on road (Reward group)]              │
│                                                        │
│                                                        │
│  ┌──────────────────────────────────────────────┐     │
│  │ 125m/501m        +5.0%               24m     │     │
│  │ ███████████████████████████████████████████  │     │
│  │ 0m   100m   200m   300m   400m   500m       │     │
│  └──────────────────────────────────────────────┘     │
└────────────────────────────────────────────────────────┘
```

**Route structure**:
```
Distance: 0m ──────── 50m ─────────────────────────── 500m
           │  Flat    │        Hill (target gradient)      │
           │  0%      │        e.g. 5% steady              │
           │ lead-in  │        Pain builds here            │
```

- **Pain VAS**: Continuous slider visible, participant rates in real-time
- **Resistance**: Ergometer resistance matches virtual gradient
- **Coins**: Placed on road for Reward group (collected by riding through)
- **EEG markers**: 
  - `HILL_START` (climbing begins)
  - `EEG_EPOCH` (every 30 seconds with pain VAS, gradient, speed)
  - `COIN_COLLECTED` (each coin pickup)
  - `HILL_COMPLETE` (reached 450m)
- **Quit detection**: 10s no pedaling → trial abandoned

### Phase 4: Post-Climb Rest (30 seconds)
**Purpose**: Pain-specific EEG window (effort = 0, pain persists)

```
┌─────────────────────────────────┐
│                                 │
│       HILL COMPLETE! ✓          │
│                                 │
│    Please remain still.         │
│    Continue rating your pain.   │
│                                 │
│    PAIN: [────●──────] 6/10     │
│                                 │
│    Rest time: 15s / 30s         │
│                                 │
└─────────────────────────────────┘
```

- **Screen**: Completion message, pain VAS still visible
- **Bike**: Stopped (no resistance)
- **Participant**: Sits still, rates residual pain
- **EEG markers**: 
  - `POST_CLIMB_REST_START`
  - `POST_CLIMB_REST_10s` (with pain VAS value)
  - `POST_CLIMB_REST_20s` (with pain VAS value)
  - `POST_CLIMB_REST_END`
- **Critical for analysis**: Any EEG signature present here is PAIN-SPECIFIC (cannot be effort or movement)

### Phase 5: Reward Question (5 seconds)
**Purpose**: Decision deliberation EEG window

```
┌─────────────────────────────────┐
│                                 │
│  How rewarding was this trial?  │
│                                 │
│  (Think about your answer...)   │
│                                 │
└─────────────────────────────────┘
```

- **EEG marker**: `REWARD_QUESTION_ONSET`
- **Analysis**: Frontal alpha asymmetry (F3/F4) during reward deliberation

### Phase 6: Reward Rating (until response)
**Purpose**: Explicit reward valuation

```
┌─────────────────────────────────┐
│                                 │
│  How rewarding was this trial?  │
│                                 │
│  [──────────●────────] 7/10     │
│                                 │
│  (Slide to rate, then confirm)  │
│                                 │
└─────────────────────────────────┘
```

- **EEG marker**: `REWARD_SLIDER_ONSET`, response logged with value
- **Transitions to**: Next condition (back to Phase 1)

---

## Data Collected Per Trial

### Continuous (sampled at ~60Hz, logged at 1Hz)
| Measure | Source | Unit |
|---------|--------|------|
| Power | Ergometer | Watts |
| Cadence | Ergometer | RPM |
| Speed | Calculated | km/h |
| Heart rate | Ergometer/chest strap | BPM |
| Gradient (virtual) | CurvedRouteGenerator | % |
| Distance | BikeController | meters |
| Pain VAS | PersistentPainVAS | 0-10 |

### Per-trial ratings
| Measure | When | Scale |
|---------|------|-------|
| Continuous pain VAS | During climb + rest | 0-10 slider |
| Reward VAS | After rest period | 0-10 slider |

### EEG markers (time-stamped events)
| Marker | Timing | Purpose |
|--------|--------|---------|
| BLOCK_START | Session begin | Sync |
| CUE_ONSET | Hill cue appears | Baseline start, anticipation |
| COUNTDOWN_START | 3-2-1-GO | Motor preparation |
| HILL_START | Pedaling begins | Climb onset |
| EEG_EPOCH | Every 30s during climb | Spectral windows |
| COIN_COLLECTED | Coin pickup | Reward event |
| HILL_COMPLETE | Reached target | Climb end |
| POST_CLIMB_REST_START | Rest begins | Pain-specific window start |
| POST_CLIMB_REST_10s | 10s into rest | Pain VAS checkpoint |
| POST_CLIMB_REST_20s | 20s into rest | Pain VAS checkpoint |
| POST_CLIMB_REST_END | 30s rest done | Pain-specific window end |
| REWARD_QUESTION_ONSET | Decision prompt | Reward deliberation |
| REWARD_SLIDER_ONSET | Rating scale | Reward valuation |
| DISENGAGE_ONSET | Stopped pedaling | Quit warning |
| DISENGAGE_QUIT | Trial abandoned | Early termination |

---

## EEG Analysis Plan

### Frequency bands of interest
| Band | Range | Construct | Key electrodes |
|------|-------|-----------|----------------|
| Theta | 4-8 Hz | Effort, pain affect | Fz, FCz |
| Alpha-1 | 8-10 Hz | Pain intensity (inverse) | C3, C4, Pz |
| Alpha-2 | 10-13 Hz | Sensorimotor processing | C3, C4, Cz |
| Beta | 15-25 Hz | Motor drive, pain inhibition | Cz, C3, C4 |
| Gamma | 30-100 Hz | Pain intensity (direct) | FCz, Cz |
| Frontal asymmetry | 8-13 Hz | Reward motivation | F3 vs F4 |

### Key contrasts

**1. Pain dose-response** (primary hypothesis):
```
Compare: Post-climb rest EEG across gradients (0% vs 2% vs 5% vs 8% vs 10%)
Predict: Gamma ↑ and Alpha ↓ scale with gradient intensity
Why valid: Effort = 0 during rest, only pain remains
```

**2. Pain vs. Effort dissociation**:
```
Compare: Hill climb EEG vs. Post-climb rest EEG (same trial)
During climb: Effort HIGH + Pain HIGH → EEG reflects both
During rest:  Effort ZERO + Pain PRESENT → EEG reflects pain only
Difference:   = pure effort component
```

**3. Reward modulation of pain** (group comparison):
```
Compare: Reward group (coins visible) vs. Control group (no coins) at same gradients
Predict: Reward group shows reduced pain-related EEG + frontal asymmetry shift
Hypothesis: Reward reduces perceived pain via descending inhibition
```

**4. Anticipatory processing** (cue phase):
```
Compare: EEG during 10s cue for easy (2%) vs hard (10%) upcoming conditions
Predict: Frontal theta ↑ for harder conditions (anticipatory dread)
         Frontal asymmetry shift for Reward group (approach motivation)
```

---

## Participant Flow Diagram

```
                    ┌──────────────────┐
                    │   Recruitment    │
                    │   & Screening    │
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │  Informed Consent │
                    │  + Questionnaires │
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │   Equipment      │
                    │   Setup (15min)  │
                    │  • EEG cap       │
                    │  • Bike fit      │
                    │  • Familiarize   │
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │ Resting Baseline │
                    │    EEG (5min)    │
                    └────────┬─────────┘
                             │
              ┌──────────────▼──────────────┐
              │     EXPERIMENTAL BLOCK       │
              │                              │
              │  ┌────────────────────────┐  │
              │  │    Condition N / 9     │  │
              │  │                        │  │
              │  │  ┌─ Hill Cue (10s)    │  │
              │  │  │  [EEG baseline]     │  │
              │  │  │                     │  │
              │  │  ├─ Countdown (4s)     │  │
              │  │  │  [motor prep]       │  │
              │  │  │                     │  │
              │  │  ├─ Hill Climb (2-5m)  │  │
              │  │  │  [pain + effort]    │  │
              │  │  │  [coins if Reward]  │  │
              │  │  │                     │  │
              │  │  ├─ Post-Climb (30s)   │  │
              │  │  │  [PAIN SPECIFIC]    │  │
              │  │  │                     │  │
              │  │  ├─ Reward Q (5s)      │  │
              │  │  │  [deliberation]     │  │
              │  │  │                     │  │
              │  │  └─ Reward Rating      │  │
              │  │     [valuation]        │  │
              │  │                        │  │
              │  └──────────┬─────────────┘  │
              │             │ ×9 conditions   │
              │             │ (randomized)    │
              └─────────────┼────────────────┘
                            │
                   ┌────────▼─────────┐
                   │ Post-Experiment  │
                   │ • Final EEG rest │
                   │ • Debrief        │
                   │ • Payment        │
                   └──────────────────┘
```

---

## Experimenter Checklist

### Before participant arrives
- [ ] Boot Unity application
- [ ] Start Python WebSocket server
- [ ] Start EEG recording software
- [ ] Check ergometer battery & Bluetooth pairing
- [ ] Verify event marker connection (Unity → EEG)
- [ ] Set participant ID in TrialStarterUI

### During setup
- [ ] EEG cap on, impedances < 20 kΩ (dry) or < 5 kΩ (gel)
- [ ] Bike seat height adjusted (slight knee bend at bottom)
- [ ] Practice VAS rating (show slider, confirm understanding)
- [ ] Explain: "Rate PAIN in your legs (burning, aching)" vs "Rate EFFORT"
- [ ] Familiarization ride: 2 min flat cycling

### During experiment
- [ ] Monitor EEG signal quality on experimenter screen
- [ ] Note any talking/movement during baseline phases
- [ ] Log any interruptions or technical issues
- [ ] F6 key available to skip trials if needed

### After experiment
- [ ] Save all data files (Unity CSV + EEG recording)
- [ ] Check data integrity (all 9 conditions logged?)
- [ ] Backup to external drive
- [ ] Clean EEG cap

---

## File Outputs

| File | Contents |
|------|----------|
| `hill_experiment_{PID}_B{block}_{timestamp}.csv` | Per-frame data: timestamp, condition, gradient, phase, distance, speed, power, cadence, HR, pain_vas, gradient_current, coins |
| `{PID}_eeg_raw.edf` (or .bdf) | Continuous EEG with embedded markers |
| `{PID}_events.csv` | All event markers with timestamps |
| `{PID}_trial_results.json` | Per-condition summary: gradient, duration, avg speed, pain VAS, reward VAS, completed/abandoned |

---

## Safety & Stopping Rules

- Participant can stop at any time (verbal request)
- Trial auto-abandons after 10s no pedaling
- Maximum HR threshold: 90% of age-predicted max (220 - age)
- If pain VAS reaches 10/10, experimenter checks in verbally
- Total session does not exceed 90 minutes

---

## Version

Protocol version: 2.0
Last updated: June 2026
Unity build: HillClimbExperiment v2 with CurvedRouteGenerator
