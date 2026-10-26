using System.Collections.Generic;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>特效粒子池：预建对象、循环复用，避免每次锤击都 Instantiate/Destroy。</summary>
    public class ForgeFxRunner : MonoBehaviour
    {
        private class P
        {
            public Transform tr;
            public SpriteRenderer sr;
            public Vector2 vel;
            public float age, life, gravity, s0, s1, spin;
            public Color c0, c1;
            public bool active;
        }

        private readonly List<P> _all = new List<P>();
        private readonly Stack<P> _free = new Stack<P>();

        public void Spawn(Sprite sprite, Vector3 pos, Vector2 vel, float gravity,
                          float life, float size0, float size1, Color c0, Color c1,
                          int order, float spin = 0f)
        {
            P p = _free.Count > 0 ? _free.Pop() : CreateOne();
            p.active = true;
            p.tr.position = pos;
            p.tr.localScale = Vector3.one * size0;
            p.tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            p.sr.sprite = sprite;
            p.sr.sortingOrder = order;
            p.sr.color = c0;
            p.vel = vel;
            p.gravity = gravity;
            p.age = 0f;
            p.life = Mathf.Max(0.01f, life);
            p.s0 = size0;
            p.s1 = size1;
            p.spin = spin;
            p.c0 = c0;
            p.c1 = c1;
        }

        private P CreateOne()
        {
            var go = new GameObject("fx");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ForgeFxSprites.Spark;
            var p = new P { tr = go.transform, sr = sr };
            _all.Add(p);
            return p;
        }

        private void Update()
        {
            // unscaledDeltaTime：面板打开时 timeScale=0，火花不能定格
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _all.Count; i++)
            {
                var p = _all[i];
                if (!p.active) continue;

                p.age += dt;
                float k = p.age / p.life;
                if (k >= 1f)
                {
                    p.active = false;
                    p.tr.gameObject.SetActive(false);
                    _free.Push(p);
                    continue;
                }

                if (!p.tr.gameObject.activeSelf) p.tr.gameObject.SetActive(true);

                p.vel.y -= p.gravity * dt;
                p.vel *= 1f - Mathf.Clamp01(1.6f * dt);          // 空气阻力
                p.tr.position += (Vector3)(p.vel * dt);
                p.tr.localScale = Vector3.one * Mathf.Lerp(p.s0, p.s1, k);
                if (p.spin != 0f) p.tr.Rotate(0f, 0f, p.spin * dt);
                p.sr.color = Color.Lerp(p.c0, p.c1, Mathf.Sqrt(k));
            }
        }
    }
}
