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
import random
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


def gen_chop():
    """砍树命中：两段式木头撞击声。

    第一下是低频「咚」（120Hz 附近快速下滑的三角感正弦，模拟斧头入木）；
    紧跟一簇短噪声「咔」（木纤维劈开的震颤），高频为主、衰减极快。
    总长 0.18s —— 玩家连砍时声音不能糊成一片。
    """
    dur = 0.18
    samples = [0.0] * int(dur * SR)
    rnd = random.Random(20261017)

    # 「咚」：低频冲击
    for i in range(len(samples)):
        t = i / SR
        if t > 0.07:
            break
        f = 130 - 60 * (t / 0.07)
        env = math.exp(-t / 0.018)
        samples[i] += 0.9 * env * math.sin(2 * math.pi * f * t)

    # 「咔」：木屑噪声（窄带感：让噪声过低通，只留中高频）
    prev = 0.0
    for i in range(len(samples)):
        t = i / SR
        if t < 0.02:
            continue
        env = math.exp(-(t - 0.02) / 0.045)
        white = rnd.uniform(-1.0, 1.0)
        prev = 0.82 * prev + 0.18 * white          # 一阶低通，留下中低频的「闷响」成分
        samples[i] += 0.35 * env * (white * 0.45 + prev * 0.55)

    write_wav(os.path.join(OUT, "sfx_chop.wav"), samples)


# ============================================================
# 打铁音效（ForgeTax 风格）v0.19
# ============================================================

def gen_hammer():
    """锤击铁砧：金属「铛」——瞬态冲击 + 金属泛音环。总长 0.25s。"""
    dur = 0.25
    samples = [0.0] * int(dur * SR)
    rnd = random.Random(20261025)
    for i in range(len(samples)):
        t = i / SR
        if t < 0.008:
            samples[i] += 0.8 * rnd.uniform(-1, 1) * math.exp(-t / 0.003)
            samples[i] += 0.6 * math.sin(2 * math.pi * 220 * t) * math.exp(-t / 0.006)
        for f, g, decay in [(1200, 0.35, 0.08), (2400, 0.22, 0.05), (3600, 0.12, 0.035), (5200, 0.06, 0.02)]:
            samples[i] += g * math.sin(2 * math.pi * f * t) * math.exp(-t / decay)
    write_wav(os.path.join(OUT, "sfx_hammer.wav"), samples)


def gen_break():
    """剑碎：噪声爆发 + 下行滑音 + 碎片散落。总长 0.6s。"""
    dur = 0.6
    samples = [0.0] * int(dur * SR)
    rnd = random.Random(20261025)
    for i in range(len(samples)):
        t = i / SR
        if t < 0.05:
            env = math.exp(-t / 0.015)
            samples[i] += 1.0 * env * rnd.uniform(-1, 1)
            for f in (1800, 2700, 4100):
                samples[i] += 0.3 * env * math.sin(2 * math.pi * f * t)
        if 0.04 < t < 0.25:
            tt = t - 0.04
            f = 400 - 320 * (tt / 0.21)
            samples[i] += math.exp(-tt / 0.08) * 0.7 * math.sin(2 * math.pi * f * t)
        if 0.15 < t < 0.55 and rnd.random() < 0.03:
            samples[i] += 0.25 * rnd.uniform(-1, 1)
    write_wav(os.path.join(OUT, "sfx_break.wav"), samples)


def gen_success():
    """锤击成功：上行双音铃（E5→A5）+ 亮泛音。总长 0.45s。"""
    dur = 0.45
    samples = [0.0] * int(dur * SR)
    rnd = random.Random(20261025)
    for i in range(len(samples)):
        t = i / SR
        if t < 0.008:
            samples[i] += 0.5 * rnd.uniform(-1, 1) * math.exp(-t / 0.003)
    note(samples, 0.02, 0.20, 76, 0.45, decay=0.10)
    note(samples, 0.10, 0.30, 81, 0.50, decay=0.18)
    note(samples, 0.10, 0.25, 93, 0.15, decay=0.12)
    write_wav(os.path.join(OUT, "sfx_success.wav"), samples)


def gen_critical():
    """大成功：锤击 + 低频垫 + 上行琶音 E5→A5→C#6→E6。总长 0.7s。"""
    dur = 0.7
    samples = [0.0] * int(dur * SR)
    rnd = random.Random(20261025)
    for i in range(len(samples)):
        t = i / SR
        if t < 0.010:
            samples[i] += 0.8 * rnd.uniform(-1, 1) * math.exp(-t / 0.004)
        if t < 0.15:
            samples[i] += 0.35 * math.sin(2 * math.pi * 80 * t) * math.exp(-t / 0.08)
    note(samples, 0.03, 0.18, 76, 0.45, decay=0.10)
    note(samples, 0.10, 0.22, 81, 0.50, decay=0.14)
    note(samples, 0.18, 0.25, 85, 0.50, decay=0.16)
    note(samples, 0.26, 0.40, 88, 0.55, decay=0.22)
    write_wav(os.path.join(OUT, "sfx_critical.wav"), samples)


def gen_frenzy():
    """入神激活：上升扫频 100→800Hz + 确认铃。总长 0.5s。"""
    dur = 0.5
    samples = [0.0] * int(dur * SR)
    for i in range(len(samples)):
        t = i / SR
        f = 100 + 700 * (t / dur) ** 1.5
        env = min(1.0, t / 0.05) * math.exp(-max(0, t - 0.3) / 0.15)
        samples[i] += 0.55 * env * math.sin(2 * math.pi * f * t)
        samples[i] += 0.20 * env * math.sin(2 * math.pi * f * 1.5 * t)
    note(samples, 0.32, 0.18, 81, 0.35, decay=0.12)
    note(samples, 0.36, 0.14, 88, 0.25, decay=0.10)
    write_wav(os.path.join(OUT, "sfx_frenzy.wav"), samples)


def gen_heat():
    """入炉加热：火焰呼啸（低通滤噪 + 低频轰）。总长 0.4s。"""
    dur = 0.4
    samples = [0.0] * int(dur * SR)
    rnd = random.Random(20261025)
    prev = 0.0
    for i in range(len(samples)):
        t = i / SR
        env = min(1.0, t / 0.06) * math.exp(-max(0, t - 0.15) / 0.18)
        white = rnd.uniform(-1, 1)
        prev = 0.70 * prev + 0.30 * white
        samples[i] += 0.6 * env * prev
        if t < 0.12:
            samples[i] += 0.30 * math.sin(2 * math.pi * 60 * t) * math.exp(-t / 0.06)
    write_wav(os.path.join(OUT, "sfx_heat.wav"), samples)


def gen_damage():
    """锤击损伤：闷响 + 200→100Hz 下行短滑音。总长 0.22s。"""
    dur = 0.22
    samples = [0.0] * int(dur * SR)
    rnd = random.Random(20261025)
    for i in range(len(samples)):
        t = i / SR
        if t < 0.008:
            samples[i] += 0.7 * rnd.uniform(-1, 1) * math.exp(-t / 0.003)
        samples[i] += 0.5 * math.sin(2 * math.pi * 160 * t) * math.exp(-t / 0.035)
        if t < 0.15:
            f = 200 - 100 * (t / 0.15)
            samples[i] += 0.35 * math.sin(2 * math.pi * f * t) * math.exp(-t / 0.06)
    write_wav(os.path.join(OUT, "sfx_damage.wav"), samples)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    gen_bgm()
    gen_click()
    gen_confirm()
    gen_chop()
    gen_hammer()
    gen_break()
    gen_success()
    gen_critical()
    gen_frenzy()
    gen_heat()
    gen_damage()
    print("音频生成完成（含打铁 7 音效）。")
