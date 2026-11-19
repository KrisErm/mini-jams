using UnityEngine;

namespace MiniJam
{
    public sealed class JamSpikeTrap : MonoBehaviour
    {
        public Transform spikes;
        public Renderer warning;
        public float period = 3.6f;
        public float offset;
        public bool Armed { get; private set; }
        MaterialPropertyBlock block;
        Vector3 rest;
        void Awake() { block = new MaterialPropertyBlock(); rest = spikes.localPosition; }
        void Update()
        {
            if (JamGame.Instance == null || !JamGame.Instance.CanPlay) return;
            float t = Mathf.Repeat(Time.time + offset, period);
            Armed = t >= period * 0.55f;
            bool warningNow = t >= period * 0.36f && !Armed;
            Color c = Armed ? new Color(1, 0.12f, 0.08f) : (warningNow ? new Color(1, 0.62f, 0.1f) : new Color(0.18f, 0.55f, 0.45f));
            block.SetColor("_Color", c); block.SetColor("_BaseColor", c); block.SetColor("_EmissionColor", c * 0.4f);
            warning.SetPropertyBlock(block);
            spikes.localPosition = Vector3.MoveTowards(spikes.localPosition, rest + Vector3.up * (Armed ? 0.85f : 0), Time.deltaTime * 8);
        }
        void OnTriggerStay(Collider other)
        {
            var player = other.GetComponentInParent<JamPlayer>();
            if (Armed && player != null && player.transform.position.y < transform.position.y + 0.65f) player.Hurt();
        }
    }
}
