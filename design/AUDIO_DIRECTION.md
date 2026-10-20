# Operational audio

The prototype uses original procedural foley rather than field recordings or third-party sound assets. Layered filtered noise, engine harmonics, pressure transients and mechanical envelopes replace the earlier short musical tones.

- Movement combines engine, propeller and wake sounds, with separate submarine, aircraft and mobile land-battery treatments.
- Search uses a damped sonar-style console cue with echoes; aircraft and land batteries use sensor relays and radio squelch. The ping is an interface cue, not a simulated active-sonar emission or detection report.
- Strike uses a launch transient, low pressure thump and exhaust rush. It never plays a hit-confirmation sound.
- Recovery, Hold, Patrol, Support and Replenishment use equipment motors, relays, watch pings, radio bursts, winches, hydraulics and latches.
- Sea ambience is a continuous 20-second stereo wind/water loop with overlapping boundaries and slow swells. It ducks gently during action cues.

Each cue has three deterministic waveform variants, cached lazily. Two non-spatial voices allow overlap without truncating a playing waveform. Settings retain separate Master, Effects and Ambience controls, and the matching text descriptions remain available when muted.

The persistent audio host supplies an enabled `AudioListener` when the scene has none, reusing an existing enabled listener when available. This is required even for the non-spatial command-map sources; neither generated camera originally supplied one. Listener checks cover absent, disabled, existing and inactive listeners. If playback is silent, check **Settings > Audio > Master volume** as well as Effects/Ambience: a previously saved zero Master remains muted after an update.

Audio receives only the completed action and acting formation kind. The existing Solo visibility gate suppresses enemy cues and descriptions. Audio neither reads combat outcomes nor advances gameplay random state.

Run `tools/run-tests.ps1 -Suite Audio` to export individual WAV previews, `audio-showcase.wav`, and `showcase-timestamps.txt` under `SeaOfUncertainty/TestResults/audio`. The Audio checks also run within Presentation and Regression. They cover all 144 action/formation/variant renders, finite samples, peak headroom, DC offset, fades, variant differences, continuous stereo ambience, privacy and random-state isolation. These signal checks do not establish subjective realism; audition on speakers and headphones remains the listening acceptance step.
