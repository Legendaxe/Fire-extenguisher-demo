"""Generates the placeholder rocket-chair audio clips.

The project shipped with empty clip slots on purpose; this script fills them with
synthetic placeholders so the game is not silent, without any external asset
downloads. Everything is deterministic: the same script always writes the same
files, so re-running it is safe.

Usage (from the project root):
    python Tools/Audio/generate_rocket_audio.py
    python Tools/Audio/generate_rocket_audio.py --out Assets/Audio/RocketOffice

Output: 44.1 kHz mono 16-bit WAV files, imported by Unity as one-shot or looping
AudioClips. RocketChairSetup assigns them to the prefabs and glass pane.
"""

import argparse
import math
import os
import random
import struct
import sys
import wave

SR = 44100
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
DEFAULT_OUT = os.path.join(PROJECT_ROOT, "Assets", "Audio", "RocketOffice")


# --------------------------------------------------------------------------- dsp


def lowpass(signal, cutoff, passes=1):
    a = 1.0 - math.exp(-2.0 * math.pi * cutoff / SR)
    for _ in range(passes):
        out = [0.0] * len(signal)
        y = 0.0
        for i, x in enumerate(signal):
            y += a * (x - y)
            out[i] = y
        signal = out
    return signal


def highpass(signal, cutoff, passes=1):
    for _ in range(passes):
        lp = lowpass(signal, cutoff)
        signal = [signal[i] - lp[i] for i in range(len(signal))]
    return signal


def bandpass(signal, center, q=0.8):
    """RBJ constant-peak-gain bandpass, direct form 1."""
    w0 = 2.0 * math.pi * center / SR
    alpha = math.sin(w0) / (2.0 * q)
    b0 = alpha
    b1 = 0.0
    b2 = -alpha
    a0 = 1.0 + alpha
    a1 = -2.0 * math.cos(w0)
    a2 = 1.0 - alpha
    b0, b1, b2 = b0 / a0, b1 / a0, b2 / a0
    a1, a2 = a1 / a0, a2 / a0

    out = [0.0] * len(signal)
    x1 = x2 = y1 = y2 = 0.0
    for i, x in enumerate(signal):
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x
        y2, y1 = y1, y
        out[i] = y
    return out


def noise(count, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(count)]


def exp_env(count, tau, attack=0.002):
    attack_samples = max(1, int(attack * SR))
    env = [0.0] * count
    for i in range(count):
        value = math.exp(-(i / SR) / tau)
        if i < attack_samples:
            value *= i / attack_samples
        env[i] = value
    return env


def shaped(signal, tau, attack=0.002):
    """Applies an exponential decay envelope to a whole signal."""
    env = exp_env(len(signal), tau, attack)
    return [signal[i] * env[i] for i in range(len(signal))]


def sweep(count, f0, f1, tau, attack=0.002, wobble=0.0, wobble_rate=9.0):
    env = exp_env(count, tau, attack)
    out = [0.0] * count
    phase = 0.0
    for i in range(count):
        t = i / count
        freq = f0 * ((f1 / f0) ** t)
        if wobble:
            freq *= 1.0 + wobble * math.sin(2.0 * math.pi * wobble_rate * (i / SR))
        phase += 2.0 * math.pi * freq / SR
        out[i] = math.sin(phase) * env[i]
    return out


def mix(*layers):
    """Mixes (gain, signal) pairs, or (gain, signal, offset_seconds) triples."""
    length = 0
    for layer in layers:
        offset = int(layer[2] * SR) if len(layer) > 2 else 0
        length = max(length, offset + len(layer[1]))

    out = [0.0] * length
    for layer in layers:
        gain, signal = layer[0], layer[1]
        offset = int(layer[2] * SR) if len(layer) > 2 else 0
        for i, value in enumerate(signal):
            out[offset + i] += value * gain
    return out


def sweep_layer(f0, f1, count, tau, gain, attack=0.002, wobble=0.0):
    return gain, sweep(count, f0, f1, tau, attack, wobble)


def click(rng, count=0.008, cutoff=1500.0, gain=1.0):
    n = int(count * SR)
    return gain, highpass(noise(n, rng), cutoff, 2), 0.0


def tinkles(rng, count, start, spread, low, high, gain_low, gain_high, max_time=1.0):
    """Scatter of short glass/metal pings."""
    total = int(max_time * SR)
    out = [0.0] * total
    for _ in range(count):
        begin = rng.uniform(start, start + spread)
        freq = rng.uniform(low, high)
        tau = rng.uniform(0.02, 0.16)
        gain = rng.uniform(gain_low, gain_high)
        length = min(int(0.6 * SR), total - int(begin * SR))
        if length <= 4:
            continue
        env = exp_env(length, tau, 0.0005)
        offset = int(begin * SR)
        phase = 0.0
        for i in range(length):
            phase += 2.0 * math.pi * freq / SR
            value = math.sin(phase) + 0.4 * math.sin(phase * 2.71)
            out[offset + i] += value * env[i] * gain
    return out


def peak_normalize(signal, target):
    peak = max(abs(s) for s in signal) or 1.0
    scale = target / peak
    return [s * scale for s in signal]


def de_dc(signal):
    return highpass(signal, 35.0, 2)


def edge_fade(signal, fade=0.004):
    n = min(len(signal), int(fade * SR))
    for i in range(n):
        signal[i] *= i / n
        signal[-1 - i] *= i / n
    return signal


def soft_clip(signal, drive=1.15):
    norm = math.tanh(drive)
    return [math.tanh(drive * s) / norm for s in signal]


def write_wav(path, signal, peak=0.9, loop=False):
    signal = de_dc(signal)
    signal = peak_normalize(signal, peak)
    if not loop:
        signal = edge_fade(signal)

    frames = bytearray()
    for s in signal:
        value = int(max(-1.0, min(1.0, s)) * 32767.0)
        frames += struct.pack("<h", value)

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(SR)
        wav.writeframes(bytes(frames))

    peak_value = max(abs(s) for s in signal)
    rms = math.sqrt(sum(s * s for s in signal) / len(signal))
    print(
        f"  {os.path.basename(path):34s} {len(signal) / SR:5.2f}s  "
        f"peak {peak_value:4.2f}  rms {rms:4.3f}"
    )


# --------------------------------------------------------------------------- clips


def clip_discharge(rng):
    """Continuous powder hiss, seamless loop."""
    seconds = 2.0
    crossfade = 0.12
    total = int((seconds + crossfade) * SR)
    loop_length = int(seconds * SR)

    body = bandpass(noise(total, rng), 1900.0, 0.55)
    air = highpass(noise(total, rng), 5200.0, 2)
    rumble = lowpass(noise(total, rng), 170.0, 2)

    signal = [0.0] * total
    for i in range(total):
        t = i / SR
        flutter = 1.0 + 0.10 * math.sin(2 * math.pi * 4.3 * t) + 0.05 * math.sin(2 * math.pi * 1.7 * t + 1.1)
        signal[i] = (0.78 * body[i] + 0.16 * air[i] + 0.5 * rumble[i]) * flutter

    # Fold the tail into the head with an equal-power crossfade so the loop is click free.
    xfade = int(crossfade * SR)
    for i in range(xfade):
        blend = i / xfade
        signal[i] = signal[i] * math.sqrt(blend) + signal[loop_length + i] * math.sqrt(1.0 - blend)

    return signal[:loop_length]


def clip_start(rng):
    """Valve clunk plus a short, bright pressure burst."""
    count = int(0.45 * SR)
    hiss = shaped(bandpass(noise(count, rng), 2600.0, 0.6), 0.10, 0.004)
    air = shaped(highpass(noise(count, rng), 4500.0, 2), 0.06, 0.002)
    chuff = shaped(bandpass(noise(count, rng), 380.0, 0.7), 0.06, 0.004)

    return mix(
        (0.62, hiss),
        (0.22, air),
        (0.45, chuff),
        sweep_layer(170.0, 70.0, int(0.26 * SR), 0.07, 0.35),
        click(rng, count=0.012, cutoff=1800.0, gain=0.5),
    )


def clip_stop(rng):
    """Valve tick and a falling hiss tail."""
    count = int(0.30 * SR)
    tail = shaped(bandpass(noise(count, rng), 2000.0, 0.55), 0.075, 0.003)
    dull = shaped(lowpass(noise(count, rng), 900.0, 2), 0.05, 0.004)

    return mix(
        (0.70, tail),
        (0.35, dull),
        sweep_layer(130.0, 80.0, int(0.12 * SR), 0.04, 0.20),
        click(rng, count=0.008, cutoff=2200.0, gain=0.40),
    )


def clip_impact_small(rng):
    count = int(0.18 * SR)
    body = shaped(bandpass(noise(count, rng), 900.0, 0.9), 0.02, 0.001)

    return mix(
        sweep_layer(240.0, 150.0, count, 0.045, 0.85),
        (0.45, body),
        click(rng, count=0.006, cutoff=1400.0, gain=0.45),
        sweep_layer(880.0, 760.0, int(0.05 * SR), 0.018, 0.22),
    )


def clip_impact_medium(rng):
    count = int(0.32 * SR)
    body = shaped(bandpass(noise(count, rng), 750.0, 0.7), 0.05, 0.001)
    ring_a = sweep_layer(640.0, 610.0, count, 0.11, 0.16)
    ring_b = sweep_layer(1010.0, 950.0, count, 0.085, 0.12)

    return mix(
        sweep_layer(175.0, 95.0, count, 0.085, 0.95),
        (0.55, body),
        ring_a,
        ring_b,
        click(rng, count=0.010, cutoff=1600.0, gain=0.42),
    )


def clip_impact_big(rng):
    count = int(0.72 * SR)
    crash = shaped(bandpass(noise(count, rng), 1700.0, 0.45), 0.16, 0.002)
    debris = tinkles(rng, 16, 0.0, 0.12, 700.0, 3400.0, 0.02, 0.07, max_time=0.7)

    clangs = []
    for freq, tau, gain in (
        (470.0, 0.30, 0.14),
        (735.0, 0.24, 0.11),
        (1090.0, 0.20, 0.09),
        (1720.0, 0.16, 0.06),
        (2450.0, 0.12, 0.04),
    ):
        clangs.append(sweep_layer(freq, freq * 0.97, int(0.5 * SR), tau, gain))

    spring = sweep_layer(430.0, 150.0, int(0.42 * SR), 0.16, 0.10, wobble=0.05)

    return mix(
        sweep_layer(98.0, 46.0, count, 0.19, 1.0),
        (0.70, crash),
        (0.55, debris),
        *(clangs + [spring]),
    )


def clip_glass_break(rng):
    count = int(1.25 * SR)

    crack = shaped(highpass(noise(int(0.03 * SR), rng), 2400.0, 2), 0.008, 0.0004)
    crack = (0.75, crack)

    body = shaped(bandpass(noise(count, rng), 3200.0, 0.4), 0.20, 0.0015)

    shards = tinkles(rng, 52, 0.01, 0.55, 2200.0, 9500.0, 0.05, 0.16, max_time=1.15)

    return mix(
        crack,
        (0.62, body),
        (0.85, shards),
        sweep_layer(120.0, 80.0, int(0.35 * SR), 0.11, 0.30),
    )


# --------------------------------------------------------------------------- main


CLIPS = (
    ("A_ExtinguisherDischarge.wav", clip_discharge, 0.62, True),
    ("A_ExtinguisherStart.wav", clip_start, 0.88, False),
    ("A_ExtinguisherStop.wav", clip_stop, 0.85, False),
    ("A_ImpactSmall.wav", clip_impact_small, 0.85, False),
    ("A_ImpactMedium.wav", clip_impact_medium, 0.90, False),
    ("A_ImpactBig.wav", clip_impact_big, 0.92, False),
    ("A_GlassBreak.wav", clip_glass_break, 0.92, False),
)


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", default=DEFAULT_OUT, help="Output folder for the WAV files.")
    args = parser.parse_args(argv)

    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)
    print(f"Writing clips to {out_dir}")

    for index, (name, builder, peak, loop) in enumerate(CLIPS):
        rng = random.Random(1337 + index * 7919)
        signal = builder(rng)
        if not loop:
            signal = soft_clip(signal)
        write_wav(os.path.join(out_dir, name), signal, peak=peak, loop=loop)

    print("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
