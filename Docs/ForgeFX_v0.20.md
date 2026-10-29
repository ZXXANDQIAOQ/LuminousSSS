# 打铁玩法素材包 v0.20（ForgeTax 风格）

## 这次加了什么

参考 ForgeTax 的锻造反馈感，给原本"点按钮+数字跳"的打铁玩法补上了：

| 类别 | 内容 |
|---|---|
| **像素美术** | 铁砧、熔炉（带循环火焰）、剑坯 4 个加热阶段（冷灰→暗红→橙红→亮黄白）、断剑、锤子 3 帧挥击动画、像素星火花 |
| **8-bit 音效** | 锤击金属「铛」、成功上行琶音、大快速琶音+金属闪光、剑碎噪声崩塌、入神上升扫频、入炉火焰呼啸、损伤闷响 |
| **代码** | `ForgeFxLoader`（运行时自动加载素材+补挂 AudioManager clip）、`ForgePanel` 内嵌打铁舞台（锤子三帧动画 + 剑随锤次变色 + 像素星粒子替换原方块） |

## 文件清单

```
Assets/
├── Art/Forge/                  15 张像素精灵 PNG（Point 过滤，自动被 PixelArtImporter 处理）
│   ├── anvil.png               铁砧 64x48
│   ├── furnace.png             熔炉 80x60
│   ├── flame_f1..f4.png        熔炉火焰 4 帧循环 20x28
│   ├── sword_cold.png          冷剑坯 56x12
│   ├── sword_hot1..hot3.png    加热三阶段 56x12
│   ├── sword_broken.png        断剑 56x12
│   ├── hammer_f1..f3.png       锤子举高/下挥/命中 32x32
│   └── spark.png               像素星火花 8x8
├── Resources/Forge/            上面精灵的副本（供 Resources.Load 运行时加载）
├── Audio/sfx_*.wav              重新合成的 7 个打铁音效（覆盖原占位文件）
└── Scripts/
    ├── Gameplay/ForgeFxLoader.cs    新增：素材加载 + AudioManager 自举
    └── UI/ForgePanel.cs            修改：BuildStage() 打铁舞台 + PlayHammerAnim()
```

## 不需要手动做的事

- **不用拖 Inspector**：`ForgeFxLoader.EnsureLoaded()` 在 `ForgePanel.Open()` 首次调用时自动跑，会：
  1. `Resources.Load<Texture2D>` 加载所有精灵并 `Sprite.Create`，运行时强制 `filterMode=Point`
  2. 如果场景里没挂 AudioManager，自动补一个常驻 GameObject 并挂上所有 `sfx_*.wav`
- **不用设导入设置**：`PixelArtImporter` 已监听 `Assets/Art/` 目录，Forge 子目录下的 PNG 打开 Unity 时自动按像素规范导入（Sprite/Point/无压缩）。

## 如果音效还是不响

按顺序排查：

1. **PlayerPrefs 里 SfxOn 是否为 1**：AudioManager 默认开，但设置面板可能关过。
2. **场景里有没有旧的 AudioManager**：如果旧 AudioManager 的 `sfxSource` 是空的，`ForgeFxLoader` 会补挂 clip 但不会补 AudioSource。确认场景里 AudioManager 组件挂了两个 AudioSource（musicSource + sfxSource）。
3. **音量**：`sfxSource.volume` 是否为 1。

## 舞台在面板里的位置

```
┌────────────────────────────────────────┐
│            铁匠铺              [×]      │
├────────────────────────────────────────┤
│  ┌────────────── Stage 220h ──────┐   │
│  │  [熔炉🔥]      [锤子]          │   │
│  │   火焰动效      [剑→在砧上]    │   │
│  │                 [铁砧]         │   │
│  └─────────────────────────────────┘   │
│  剑价值：xxx        破碎概率：xx%       │
│  本锤概率：...                          │
│  入神充能：8/10  ████████░░             │
│  状态提示                               │
│  [入炉]  [锤击]  [收剑]                 │
│  遗物兑换列表                          │
└────────────────────────────────────────┘
```

锤击时：锤子 f1 举高 → f2 下挥 → f3 命中（同时像素星火花从砧面爆出）→ 回 f1。剑的颜色随 HitCount 从冷灰渐变到亮黄白。剑碎时切成 `sword_broken`。

## 重新生成素材

```bash
# 重新画像素精灵（覆盖 Assets/Art/Forge/*.png）
python3 Tools/gen_forge_sprites.py

# 重新合成音效（覆盖 Assets/Audio/sfx_*.wav）
python3 Tools/gen_forge_audio.py
```

合成后需要把新精灵同步到 Resources：
```bash
cp Assets/Art/Forge/*.png Assets/Resources/Forge/
```

## 调色参考

- 铁砧灰：`#3C404A / #606674 / #8C94A4 / #BEC6D6`
- 剑加热：冷 `#969BA5` → 热1 `#A03C28` → 热2 `#E66E28` → 热3 `#FFD264`
- 火焰外焰 `#FF781E` / 内焰 `#FFD250` / 焰心 `#FFF5C8`
- 整体暖棕底 `#2E1F14`（舞台背景）
