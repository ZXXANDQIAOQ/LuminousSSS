#!/usr/bin/env python3
"""
LuminousSSS 打铁音效 8-bit/16-bit 芯片风合成器
全部用 numpy 合成 PCM 16-bit 单声道 WAV，覆盖 Assets/Audio/sfx_*.wav
风格：Game Boy / SNES 方波 + 噪声，短促有冲击力。
"""
import os, struct, wave, math
import numpy as np

SR = 44100
OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Assets", "Audio"))
os.makedirs(OUT, exist_ok=True)

def write_wav(name, samples, sr=SR):
    samples = np.clip(samples, -1.0, 1.0)
    pcm = (samples * 32767).astype(np.int16)
    p = os.path.join(OUT, name)
    with wave.open(p, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())
    dur = len(samples)/sr
    print(f"  -> {name}  {dur:.2f}s  {len(pcm)} samples")

def env_adsr(n, a=0.005, d=0.1, s=0.7, r=0.05):
    """简单 ADSR 包络。"""
    a_n = int(a*SR); d_n = int(d*SR); r_n = int(r*SR)
    s_n = max(0, n - a_n - d_n - r_n)
    e = np.zeros(n)
    if a_n>0: e[:a_n] = np.linspace(0,1,a_n)
    if d_n>0: e[a_n:a_n+d_n] = np.linspace(1,s,d_n)
    e[a_n+d_n:a_n+d_n+s_n] = s
    if r_n>0 and a_n+d_n+s_n < n:
        e[a_n+d_n+s_n:] = np.linspace(s,0,n-(a_n+d_n+s_n))
    return e

def square(freq, dur, duty=0.5, sr=SR):
    t = np.arange(int(dur*sr))/sr
    phase = (freq*t) % 1.0
    return np.where(phase < duty, 1.0, -1.0)

def noise(dur, sr=SR):
    return np.random.uniform(-1,1,int(dur*sr))

def bandpass_noise(dur, center, q=1.0, sr=SR):
    """简单带通噪声（用单频正弦调制）。"""
    n = noise(dur, sr)
    t = np.arange(len(n))/sr
    # 用指数衰减包络 + 共振峰
    return n * np.exp(-t*8) * (np.sin(2*np.pi*center*t)*0.5+0.5)

# ---------------------------------------------------------------
# 1. sfx_hammer.wav —— 锤击铁砧「铛」
#    金属敲击：高频短噪声 + 220Hz 方波衰减 + 一点点 110Hz 体
# ---------------------------------------------------------------
def snd_hammer():
    dur = 0.25
    t = np.arange(int(dur*SR))/SR
    body = np.sin(2*np.pi*220*t) * np.exp(-t*25)
    ring = np.sin(2*np.pi*880*t) * np.exp(-t*40) * 0.3
    clink = bandpass_noise(0.05, 4000, 2) * 0.6
    s = body + ring
    s[:len(clink)] += clink
    s *= env_adsr(len(s), a=0.001, d=0.15, s=0.0, r=0.05)
    write_wav("sfx_hammer.wav", s*0.8)

# ---------------------------------------------------------------
# 2. sfx_success.wav —— 锤击成功（上行小三度琶音 C5-E5-G5）
# ---------------------------------------------------------------
def snd_success():
    notes = [523.25, 659.25, 783.99]  # C5 E5 G5
    note_dur = 0.09
    total = int((len(notes)*note_dur + 0.05)*SR)
    s = np.zeros(total)
    for i, f in enumerate(notes):
        start = int(i*note_dur*SR)
        wave = square(f, note_dur, duty=0.4) * 0.4
        wave *= np.exp(-np.arange(len(wave))/SR*8)
        s[start:start+len(wave)] += wave
    s *= env_adsr(len(s), a=0.005, d=0.1, s=0.0, r=0.08)
    write_wav("sfx_success.wav", s*0.7)

# ---------------------------------------------------------------
# 3. sfx_critical.wav —— 大成功（快速琶音 C5-E5-G5-C6 加金属闪光）
# ---------------------------------------------------------------
def snd_critical():
    notes = [523.25, 659.25, 783.99, 1046.5, 1318.5]
    note_dur = 0.07
    total = int((len(notes)*note_dur + 0.1)*SR)
    s = np.zeros(total)
    for i, f in enumerate(notes):
        start = int(i*note_dur*SR)
        wave = square(f, note_dur, duty=0.35) * 0.35
        wave += np.sin(2*np.pi*f*2*np.arange(len(wave))/SR) * 0.15
        wave *= np.exp(-np.arange(len(wave))/SR*10)
        s[start:start+len(wave)] += wave
    # 末尾闪光噪声
    flash = bandpass_noise(0.15, 6000, 3) * 0.4
    s[len(s)-len(flash):] += flash
    s *= env_adsr(len(s), a=0.003, d=0.2, s=0.0, r=0.1)
    write_wav("sfx_critical.wav", s*0.7)

# ---------------------------------------------------------------
# 4. sfx_break.wav —— 剑碎（噪声崩塌 + 低频下滑）
# ---------------------------------------------------------------
def snd_break():
    dur = 0.6
    t = np.arange(int(dur*SR))/SR
    # 玻璃碎裂噪声（高频爆发后衰减）
    crash = noise(dur) * np.exp(-t*6) * 0.7
    # 低频下滑（剑体断裂的闷响）
    slide = np.sin(2*np.pi*(200 - 150*t)*t) * np.exp(-t*4) * 0.5
    s = crash + slide
    s *= env_adsr(len(s), a=0.002, d=0.3, s=0.0, r=0.2)
    write_wav("sfx_break.wav", s*0.8)

# ---------------------------------------------------------------
# 5. sfx_frenzy.wav —— 入神激活（上升扫频 + 闪光和弦）
# ---------------------------------------------------------------
def snd_frenzy():
    dur = 0.7
    t = np.arange(int(dur*SR))/SR
    # 上升扫频 200→1400Hz
    freq = 200 + 1200*(t/dur)
    phase = 2*np.pi*np.cumsum(freq)/SR
    sweep = np.sin(phase) * np.exp(-t*3) * 0.4
    # 末尾和弦 C5+E5+G5 持续
    chord_start = int(0.45*SR)
    chord = np.zeros(len(t))
    for f in (523.25, 659.25, 783.99):
        chord[chord_start:] += np.sin(2*np.pi*f*t[chord_start:]) * 0.15
    chord *= np.exp(-(t-0.45)*3)
    s = sweep + chord
    s *= env_adsr(len(s), a=0.01, d=0.2, s=0.0, r=0.15)
    write_wav("sfx_frenzy.wav", s*0.7)

# ---------------------------------------------------------------
# 6. sfx_heat.wav —— 入炉加热（火焰白噪声带通，0.8s）
# ---------------------------------------------------------------
def snd_heat():
    dur = 0.9
    t = np.arange(int(dur*SR))/SR
    # 火焰：低频呼啸 + 噼啪
    roar = noise(dur) * (0.4 + 0.3*np.sin(2*np.pi*5*t))
    # 噼啪：随机小脉冲
    crackle = np.zeros_like(roar)
    for _ in range(15):
        pos = np.random.randint(0, len(crackle)-500)
        crackle[pos:pos+300] += noise(300/SR) * np.exp(-np.arange(300)/SR*30) * 0.5
    s = roar*0.4 + crackle
    s *= env_adsr(len(s), a=0.05, d=0.3, s=0.7, r=0.2)
    write_wav("sfx_heat.wav", s*0.6)

# ---------------------------------------------------------------
# 7. sfx_damage.wav —— 锤击损伤（闷响低频方波下滑）
# ---------------------------------------------------------------
def snd_damage():
    dur = 0.25
    t = np.arange(int(dur*SR))/SR
    # 低频方波从 150 滑到 80
    freq = 150 - 70*(t/dur)
    phase = 2*np.pi*np.cumsum(freq)/SR
    thump = np.sign(np.sin(phase)) * 0.5
    thump *= np.exp(-t*10)
    # 一点木裂噪声
    splat = bandpass_noise(0.08, 1500, 1.5) * 0.4
    s = thump
    s[:len(splat)] += splat
    s *= env_adsr(len(s), a=0.002, d=0.15, s=0.0, r=0.05)
    write_wav("sfx_damage.wav", s*0.7)

if __name__ == "__main__":
    print("合成打铁音效到", OUT)
    snd_hammer()
    snd_success()
    snd_critical()
    snd_break()
    snd_frenzy()
    snd_heat()
    snd_damage()
    print("完成。")
