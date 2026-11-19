using UnityEngine;
namespace MiniJam
{
    public sealed class JamBreakable : MonoBehaviour
    {
        public int hits = 2;
        int lastHitFrame = -1;
        public void Hit()
        {
            if (hits <= 0 || lastHitFrame == Time.frameCount) return;
            lastHitFrame = Time.frameCount;
            JamGame.Instance.Sound.Play(JamSound.Cue.Hit);
            hits--;
            if (hits > 0)
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_Color", new Color(0.18f, 0.08f, 0.035f));
                    block.SetColor("_BaseColor", new Color(0.18f, 0.08f, 0.035f));
                    r.SetPropertyBlock(block);
                }
                return;
            }
            var source = GetComponent<Renderer>();
            for (int i = 0; i < 7; i++)
            {
                var fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fragment.name = "Crate splinter";
                fragment.transform.position = transform.position + Random.insideUnitSphere * 0.3f;
                fragment.transform.localScale = new Vector3(0.12f, 0.35f, 0.1f);
                if (source != null) fragment.GetComponent<Renderer>().sharedMaterial = source.sharedMaterial;
                Destroy(fragment.GetComponent<Collider>());
                var body = fragment.AddComponent<Rigidbody>();
                body.AddForce(new Vector3(Random.Range(-2f, 2f), Random.Range(2f, 4f), Random.Range(-2f, 2f)), ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * 4, ForceMode.Impulse);
                Destroy(fragment, 1.2f);
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
