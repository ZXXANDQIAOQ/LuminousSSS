#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LuminousSSS 音频合成器（v0.2）
生成登录/主界面用的循环 BGM 与按钮音效，全部为程序化合成、无外部素材。

用法：
    python Tools/gen_audio.py
输出：
    Assets/Audio/bgm_main.wav     循环 BGM（Am-F-C-G 琶音，9.6s 无缝循环）
    Assets/Audio/sfx_click.wav    普通按钮点击
    Assets/Audio/sfx_confirm.wav  「开始游戏」确认音
"""
import math
import os
import struct
import wave

SR = 22050
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Audio")


def midi_hz(m):
    return 440.0 * (2 ** ((m - 69) / 12.0))


def soft_tone(t, f):
    """软音头芯片音色：基音正弦 + 少量泛音，接近 8-bit 但不刺耳。"""
    return (math.sin(2 * math.pi * f * t)
            + 0.30 * math.sin(2 * math.pi * 2 * f * t)
            + 0.12 * math.sin(2 * math.pi * 3 * f * t))


def note(samples, start, dur, midi, gain, attack=0.008, decay=0.25):
    """往 samples 里叠加一个音符（起音快、指数衰减）。"""
    f = midi_hz(midi)
    n = int(dur * SR)
    s0 = int(start * SR)
    for i in range(n):
        t = i / SR
        env = min(1.0, t / attack) * math.exp(-t / decay)
        idx = s0 + i
        if 0 <= idx < len(samples):
            samples[idx] += gain * env * soft_tone(t, f)


def bass_note(samples, start, dur, midi, gain):
    """低音：纯弦感的正弦+八度，整段持续、尾部淡出。"""
    f = midi_hz(midi)
    n = int(dur * SR)
    s0 = int(start * SR)
    for i in range(n):
        t = i / SR
        fade = min(1.0, t / 0.03) * min(1.0, (dur - t) / 0.10)
        idx = s0 + i
        if 0 <= idx < len(samples):
            samples[idx] += gain * fade * (math.sin(2 * math.pi * f * t)
                                           + 0.35 * math.sin(2 * math.pi * 2 * f * t))


def write_wav(path, samples, peak=0.85):
    m = max(1e-6, max(abs(s) for s in samples))
    k = peak / m
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * k)) * 32767))
                               for s in samples))
    print(f"  {os.path.relpath(path, ROOT)}  ({len(samples)/SR:.2f}s, {os.path.getsize(path)//1024}KB)")


def gen_bgm():
    """Am - F - C - G，每和弦一小节，100 BPM，琶音走 A 小调五声音阶。"""
    bpm = 100
    beat = 60.0 / bpm            # 0.6s
    bar = beat * 4               # 2.4s
    total = bar * 4              # 9.6s，首尾和弦衔接 = 无缝循环
    samples = [0.0] * int(total * SR)

    chords = [  # (低音 midi, 琶音音池)
        (45, [57, 60, 64, 69]),   # Am: A2 + A3 C4 E4 A4
        (41, [57, 60, 65, 69]),   # F : F2 + A3 C4 F4 A4
        (48, [60, 64, 67, 72]),   # C : C3 + C4 E4 G4 C5
        (43, [59, 62, 67, 71]),   # G : G2 + B3 D4 G4 B4
    ]
    for b, (root, pool) in enumerate(chords):
        t0 = b * bar
        bass_note(samples, t0, bar, root, 0.30)
        for i in range(8):                      # 八分音符琶音
            pitch = pool[i % len(pool)]
            note(samples, t0 + i * beat / 2, beat / 2, pitch, 0.16)
        note(samples, t0 + bar - beat / 4, beat / 4, pool[-1] + 12, 0.10)  # 小节末点缀

    # 无缝循环：结尾 30ms 淡入开头
    fade = int(0.03 * SR)
    tail = samples[-fade:]
    for i in range(fade):
        w = i / fade
        samples[i] = samples[i] * w + tail[i] * (1 - w)
    samples = samples[:-fade]
    write_wav(os.path.join(OUT, "bgm_main.wav"), samples, peak=0.55)


def gen_click():
    dur = 0.06
    samples = [0.0] * int(dur * SR)
    for i in range(len(samples)):
        t = i / SR
        f = 880 - 500 * (t / dur)
        env = math.exp(-t / 0.012)
        samples[i] = env * soft_tone(t, f)
    write_wav(os.path.join(OUT, "sfx_click.wav"), samples)


def gen_confirm():
    dur = 0.32
    samples = [0.0] * int(dur * SR)
    note(samples, 0.00, 0.14, 76, 0.6, decay=0.09)   # E5
    note(samples, 0.12, 0.20, 81, 0.6, decay=0.14)   # A5
    write_wav(os.path.join(OUT, "sfx_confirm.wav"), samples)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    gen_bgm()
    gen_click()
    gen_confirm()
    print("音频生成完成。")
