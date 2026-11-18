using System;
using UnityEngine;
using DG.Tweening;

namespace GodniyGod
{
    /// <summary>
    /// All in-game animations use DOTween directly. DOTween must be installed
    /// under Assets/Plugins/Demigiant or via another supported install method.
    /// No DOTWEEN scripting define is required.
    /// </summary>
    public static class GGAnimation
    {
        // Kept for compatibility with GGGame; no coroutine animations are used.
        public static MonoBehaviour Runner;

        public static void Move(Transform target, Vector3 destination, float seconds, Action complete=null)
        {
            if (target == null) { complete?.Invoke(); return; }
            target.DOMove(destination, seconds)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => complete?.Invoke());
        }

        public static void Scale(Transform target, Vector3 value, float seconds)
        {
            if (target == null) return;
            // Kill only the previous scale tween, not a simultaneous move tween.
            string id = "GG_Scale_" + target.GetInstanceID();
            DOTween.Kill(id);
            target.DOScale(value, seconds)
                .SetEase(Ease.OutBack)
                .SetId(id);
        }

        public static void Shake(Transform target, float seconds=.30f)
        {
            if (target == null) return;
            target.DOShakePosition(seconds, .085f, 12, 45f, false, true);
        }

        public static void Spin(Transform target, float seconds=.80f)
        {
            if (target == null) return;
            target.DORotate(target.eulerAngles + Vector3.up*360f,
                    seconds, RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic);
        }

        /// <summary>A visible arc from the conveyor into the selected month box.</summary>
        public static void PutInBox(Transform target, Vector3 destination, Action complete)
        {
            if (target == null) { complete?.Invoke(); return; }
            // Selection and conveyor tweens may still be running.
            target.DOKill(false);
            Vector3 current = target.localScale;
            Sequence sequence = DOTween.Sequence();
            sequence.Append(target.DOJump(destination, .90f, 1, .57f)
                .SetEase(Ease.InOutQuad));
            sequence.Join(target.DOScale(current*.24f, .57f).SetEase(Ease.InBack));
            sequence.Join(target.DORotate(target.eulerAngles+new Vector3(0f,125f,0f),
                .57f, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            sequence.OnComplete(() => complete?.Invoke());
        }

        /// <summary>Fade in full-screen panels (menu, dialogue, reports).</summary>
        public static void Reveal(GameObject panel)
        {
            if (panel == null) return;
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null) group=panel.AddComponent<CanvasGroup>();
            group.DOKill(false);
            group.alpha=0f;
            group.DOFade(1f, .27f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }
}
