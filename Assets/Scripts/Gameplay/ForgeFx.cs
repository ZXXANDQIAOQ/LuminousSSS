using System.Collections.Generic;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 打铁的世界空间特效（ForgeTax 风格）：火花迸溅、命中闪光、冲击环、烟尘、余烬。
    ///
    /// 为什么不用 ParticleSystem：
    ///   1) 粒子系统要在场景里预置对象 / 材质 / 排序层，而本项目的场景是代码生成的，
    ///      多一个必须手动维护的资产就多一处"生成器漏配"的风险；
    ///   2) 打铁只需要「一次性迸发 + 0.3~0.8 秒淡出」这一种行为，自己写反而更可控；
    ///   3) 全部用运行时生成的白图 + SpriteRenderer，验收时不需要任何美术资源。
    ///
    /// 时间基准：<b>一律用 unscaledDeltaTime</b>。锻造面板打开时 <see cref="UiModal"/>
    /// 会把 Time.timeScale 设成 0，用缩放时间的话火花会定格在半空。
    /// </summary>
    public static class ForgeFx
    {
        /// <summary>特效排序层：压在所有场景物件之上（场景物件最高约 400）。</summary>
        public const int SortingOrder = 900;

        private static ForgeFxRunner _runner;

        private static ForgeFxRunner Runner
        {
            get
            {
                if (_runner != null) return _runner;
                var go = new GameObject("[ForgeFx]");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<ForgeFxRunner>();
                return _runner;
            }
        }

        /// <summary>锤击火花：以 <paramref name="pos"/> 为中心向斜上方迸溅。</summary>
        /// <param name="count">火花数量，普通锤击 10~14，大成功 20+</param>
        /// <param name="power">力度倍率，影响初速与射程</param>
        public static void SparkBurst(Vector3 pos, int count, Color color, float power = 1f)
        {
            var r = Runner;
            for (int i = 0; i < count; i++)
            {
                // 主要往「斜上方 + 左右」飞，少量往下落 —— 铁砧上的火花就是这个分布
                float ang = Random.Range(15f, 165f) * Mathf.Deg2Rad;
                float spd = Random.Range(2.2f, 6.5f) * power;
                var vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
                vel += Vector2.up * Random.Range(0.5f, 2.2f);

                var c = Color.Lerp(color, new Color(1f, 0.95f, 0.75f, 1f), Random.value * 0.7f);
                r.Spawn(ForgeFxSprites.Spark, pos + (Vector3)(Random.insideUnitCircle * 0.06f),
                        vel, gravity: 14f,
                        life: Random.Range(0.22f, 0.55f),
                        size0: Random.Range(0.05f, 0.10f), size1: 0.01f,
                        c0: c, c1: new Color(1f, 0.35f, 0.1f, 0f),
                        order: SortingOrder + 1,
                        spin: Random.Range(-540f, 540f));
            }
        }

        /// <summary>命中闪光 + 冲击环 + 一圈烟：锤子砸到铁砧那一下的"重量感"。</summary>
        public static void Impact(Vector3 pos, Color color, float radius = 1f)
        {
            var r = Runner;
            r.Spawn(ForgeFxSprites.Glow, pos, Vector2.zero, 0f,
                    life: 0.16f, size0: radius * 0.6f, size1: radius * 2.4f,
                    c0: new Color(1f, 0.95f, 0.8f, 0.9f), c1: new Color(color.r, color.g, color.b, 0f),
                    order: SortingOrder + 3);

            r.Spawn(ForgeFxSprites.Ring, pos, Vector2.zero, 0f,
                    life: 0.32f, size0: radius * 0.5f, size1: radius * 2.8f,
                    c0: new Color(color.r, color.g, color.b, 0.85f), c1: new Color(color.r, color.g, color.b, 0f),
                    order: SortingOrder + 2);

            Smoke(pos + Vector3.up * 0.05f, 3, new Color(0.45f, 0.42f, 0.40f, 0.5f));
        }

        /// <summary>烟尘：慢速上飘、逐渐变大变淡。</summary>
        public static void Smoke(Vector3 pos, int count, Color color)
        {
            var r = Runner;
            for (int i = 0; i < count; i++)
            {
                var vel = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.5f, 1.3f));
                r.Spawn(ForgeFxSprites.Smoke, pos + (Vector3)(Random.insideUnitCircle * 0.12f),
                        vel, gravity: -0.6f,
                        life: Random.Range(0.5f, 0.9f),
                        size0: Random.Range(0.12f, 0.22f), size1: Random.Range(0.4f, 0.7f),
                        c0: color, c1: new Color(color.r, color.g, color.b, 0f),
                        order: SortingOrder - 1,
                        spin: Random.Range(-40f, 40f));
            }
        }

        /// <summary>余烬：慢慢上飘的小亮点，用于入炉加热。</summary>
        public static void Embers(Vector3 pos, int count, Color color)
        {
            var r = Runner;
            for (int i = 0; i < count; i++)
            {
                var vel = new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(0.8f, 1.9f));
                r.Spawn(ForgeFxSprites.Spark, pos + (Vector3)(Random.insideUnitCircle * 0.3f),
                        vel, gravity: -0.4f,
                        life: Random.Range(0.5f, 1.1f),
                        size0: Random.Range(0.04f, 0.08f), size1: 0.01f,
                        c0: color, c1: new Color(1f, 0.55f, 0.15f, 0f),
                        order: SortingOrder,
                        spin: Random.Range(-180f, 180f));
            }
        }

        /// <summary>垂直上冲的光柱（大成功 / 入神）。</summary>
        public static void Pillar(Vector3 pos, Color color, float height = 2.4f)
        {
            var r = Runner;
            r.Spawn(ForgeFxSprites.Beam, pos, new Vector2(0f, 1.2f), 0f,
                    life: 0.45f, size0: 0.12f, size1: height,
                    c0: new Color(color.r, color.g, color.b, 0.9f), c1: new Color(color.r, color.g, color.b, 0f),
                    order: SortingOrder + 2);
        }
    }

    /// <summary>
    /// 特效用的几张基础图（运行时生成，不入库）。
    /// 全部硬边（alpha 只有 0/255 或简单两级渐变），配 Point 过滤不糊。
    /// </summary>
    public static class ForgeFxSprites
    {
        private static Sprite _spark, _glow, _ring, _smoke, _beam;

        public static Sprite Spark
        {
            get { if (_spark == null) _spark = MakeSpark(); return _spark; }
        }

        public static Sprite Glow
        {
            get { if (_glow == null) _glow = MakeGlow(); return _glow; }
        }

        public static Sprite Ring
        {
            get { if (_ring == null) _ring = MakeRing(); return _ring; }
        }

        public static Sprite Smoke
        {
            get { if (_smoke == null) _smoke = MakeSmoke(); return _smoke; }
        }

        public static Sprite Beam
        {
            get { if (_beam == null) _beam = MakeBeam(); return _beam; }
        }

        private static Sprite Build(string name, int w, int h, System.Func<float, float, Color> f, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
            sp.name = name;
            return sp;
        }

        private static Sprite MakeSpark()
        {
            // 4x4 实心方块：拉伸后就是一道光屑，旋转能看出"飞溅"
            return Build("fx_spark", 4, 4, (u, v) => new Color(1f, 1f, 1f, 1f), 4f);
        }

        private static Sprite MakeGlow()
        {
            // 16x16 径向辉光：中心纯白，边缘透明（两级硬边，避免糊成一团）
            return Build("fx_glow", 16, 16, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = d < 0.35f ? 1f : (d < 0.7f ? 0.55f : (d < 0.95f ? 0.18f : 0f));
                return new Color(1f, 1f, 1f, a);
            }, 16f);
        }

        private static Sprite MakeRing()
        {
            // 24x24 空心环：冲击波
            return Build("fx_ring", 24, 24, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = (d > 0.72f && d < 0.95f) ? 1f : 0f;
                return new Color(1f, 1f, 1f, a);
            }, 24f);
        }

        private static Sprite MakeSmoke()
        {
            // 16x16 软烟：同心三层，外层稀薄
            return Build("fx_smoke", 16, 16, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = d < 0.45f ? 0.75f : (d < 0.75f ? 0.45f : (d < 0.98f ? 0.18f : 0f));
                return new Color(1f, 1f, 1f, a);
            }, 16f);
        }

        private static Sprite MakeBeam()
        {
            // 8x32 竖直光柱：上端尖、下端实
            return Build("fx_beam", 8, 32, (u, v) =>
            {
                float cx = Mathf.Abs(u - 0.5f) * 2f;
                float a = (cx < 0.55f) ? Mathf.Lerp(0.15f, 1f, v) : 0f;
                return new Color(1f, 1f, 1f, a);
            }, 8f);
        }
    }
}
