using UnityEngine;

namespace MiniJam
{
    public sealed class JamGate : MonoBehaviour
    {
        public Transform bars;
        public bool IsOpen { get; private set; }
        Vector3 closed;
        void Awake() { if (bars != null) closed = bars.localPosition; }
        public void Open() { IsOpen = true; }
        void Update()
        {
            if (bars != null)
                bars.localPosition = Vector3.MoveTowards(bars.localPosition, closed + Vector3.up * (IsOpen ? 4.5f : 0), Time.deltaTime * 3);
        }
    }
}
