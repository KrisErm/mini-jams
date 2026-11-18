using UnityEngine;
namespace GodniyGod
{
    // One MonoBehaviour per file: required for stable scene serialization in Unity 6.
    public sealed class GGWorldItem : MonoBehaviour
    {
        public int index;
        [System.NonSerialized] public GGEvent eventData;
        public Vector3 defaultScale=Vector3.one;
    }
}
