using UnityEngine;

namespace DragonDay
{
    public sealed class DragonDayLoop : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float fps=7f;

        private void Update()
        {
            if(target!=null && frames!=null && frames.Length>0)
                target.sprite=frames[(int)(Time.time*fps)%frames.Length];
        }
    }
}
