using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GodniyGod
{
    /// <summary>Visual wear only. Never changes scores, deliveries, or belt speed.</summary>
    public sealed class GGFactoryWear : IDisposable
    {
        private readonly Transform root, lid;
        private readonly Transform camera;
        private readonly List<Tween> tweens = new List<Tween>();
        private readonly List<GameObject> puffs = new List<GameObject>();
        private readonly Texture2D smokeTexture;
        private readonly Sprite smokeSprite;
        private readonly Vector3 lidClosed;
        private static readonly Vector3 BrokenAngle = new Vector3(15f, -6f, 5f);
        private int activeYear = -1;

        public GGFactoryWear(Transform factory, Camera sceneCamera, Transform inletLid)
        {
            camera = sceneCamera.transform;
            lid = inletLid;
            lidClosed = lid.localPosition;
            root = new GameObject("Дым из входного ящика").transform;
            root.SetParent(factory, false);
            // The source stays on the brown inlet, clear of the first card.
            root.localPosition = new Vector3(-5.93f, 2.66f, .49f);

            smokeTexture = CreateSmokeTexture();
            smokeSprite = Sprite.Create(smokeTexture, new Rect(0, 0, smokeTexture.width, smokeTexture.height),
                new Vector2(.5f, .5f), smokeTexture.width, 0, SpriteMeshType.FullRect);
            smokeSprite.name = "Мягкий клуб дыма";
        }

        public void SetYear(int yearIndex)
        {
            if (activeYear == yearIndex) return;
            ClearMotion();
            activeYear = yearIndex;

            bool broken = yearIndex >= 2;
            lid.localPosition = broken ? lidClosed + new Vector3(-.09f, .09f, .03f) : lidClosed;
            lid.localRotation = Quaternion.Euler(broken ? BrokenAngle : Vector3.zero);
            if (yearIndex == 0) return;

            if (broken)
            {
                // Only the loose lid moves, not the box or any of the incoming cards.
                tweens.Add(lid.DOLocalRotate(BrokenAngle + new Vector3(1.8f, 0f, 1.6f), .19f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(UpdateType.Normal, false));
            }

            int count = broken ? 15 : 7;
            var rng = new System.Random(285 + yearIndex);
            for (int i = 0; i < count; i++)
                AddPuff(i, count, broken, rng);
        }

        private void AddPuff(int index, int count, bool broken, System.Random rng)
        {
            var puff = new GameObject("Клуб дыма", typeof(SpriteRenderer));
            puffs.Add(puff);
            var t = puff.transform;
            t.SetParent(root, false);
            var sprite = puff.GetComponent<SpriteRenderer>();
            sprite.sprite = smokeSprite;
            sprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sprite.receiveShadows = false;

            float variant = (float)rng.NextDouble();
            bool secondVent = broken && index % 3 == 0;
            var start = new Vector3(secondVent ? -.64f : 0f, secondVent ? .14f : 0f, secondVent ? .37f : -.06f);
            var end = start + new Vector3(.63f + variant * (broken ? .70f : .40f),
                (broken ? 1.66f : 1.08f) + variant * .27f, .12f + .20f * variant);
            float life = broken ? 2.45f : 2.85f;
            float peak = (broken ? .83f : .70f) + .07f * variant;
            Color tint = broken ? new Color(.59f, .55f, .51f) : new Color(.72f, .71f, .67f);
            t.localPosition = start;
            t.rotation = camera.rotation * Quaternion.Euler(0, 0, variant * 360f);
            t.localScale = Vector3.one * (broken ? .44f : .30f);
            tint.a = 0f;
            sprite.color = tint;

            // Each puff rises, expands and fades. Offset their phases to keep a continuous plume.
            var drift = DOTween.Sequence();
            drift.Join(t.DOLocalMove(end, life).SetEase(Ease.Linear));
            drift.Join(t.DOScale(Vector3.one * ((broken ? 1.51f : 1.02f) + .35f * variant), life).SetEase(Ease.OutQuad));
            drift.Join(DOTween.To(() => 0f, progress =>
            {
                Color color = tint;
                color.a = peak * (progress < .16f ? progress / .16f : Mathf.Pow((1f - progress) / .84f, .76f));
                sprite.color = color;
            }, 1f, life).SetEase(Ease.Linear));
            drift.SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(UpdateType.Normal, false).Pause();
            drift.ForceInit();
            drift.Goto(index * life / count, true);
            tweens.Add(drift);
        }

        private static Texture2D CreateSmokeTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Полупрозрачная текстура дыма";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            var clouds = new[]
            {
                new Vector3(-.10f, -.11f, .68f),
                new Vector3(.31f, -.02f, .51f),
                new Vector3(-.26f, .26f, .52f),
                new Vector3(.16f, .28f, .57f)
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2((x + .5f) * 2f / size - 1f, (y + .5f) * 2f / size - 1f);
                float clear = 1f;
                foreach (Vector3 blob in clouds)
                {
                    float d = Vector2.Distance(point, new Vector2(blob.x, blob.y)) / blob.z;
                    float density = 1f - Mathf.SmoothStep(.12f, 1f, Mathf.Clamp01((d - .07f) / .93f));
                    if (d >= 1f) density = 0f;
                    clear *= 1f - density * .90f;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt((1f - clear) * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void ClearMotion()
        {
            foreach (Tween tween in tweens) tween.Kill(false);
            tweens.Clear();
            foreach (GameObject puff in puffs)
                if (puff != null)
                {
                    puff.SetActive(false);
                    Object.Destroy(puff);
                }
            puffs.Clear();
        }

        public void Dispose()
        {
            ClearMotion();
            if (root != null) Object.Destroy(root.gameObject);
            Object.Destroy(smokeSprite);
            Object.Destroy(smokeTexture);
        }
    }
}
