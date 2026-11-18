using UnityEngine;

namespace DragonDay
{
    public sealed partial class DragonDayGame
    {
        [Header("Gameplay music")]
        [SerializeField] private AudioClip gameplayMusic;
        private const string MusicSetting="DragonDay.MusicVolume";
        private float musicVolume;
        private AudioSource musicSource;

        private void InitializeMusic()
        {
            musicVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(MusicSetting,.30f));
            musicSource=gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake=false;
            musicSource.loop=true;
            musicSource.spatialBlend=0;
            musicSource.volume=0;
        }

        private void StartGameplayMusic()
        {
            if(gameplayMusic!=null&&!musicSource.isPlaying)
            {
                musicSource.clip=gameplayMusic;
                musicSource.volume=0;
                musicSource.Play();
            }
        }

        private void StopGameplayMusic()
        {
            if(musicSource==null)return;
            musicSource.Stop();
            musicSource.clip=null;
        }

        private void TickMusic()
        {
            if(!musicSource.isPlaying)return;
            float target=musicVolume*(mode==Mode.Pause?.32f:.43f);
            musicSource.volume=Mathf.MoveTowards(musicSource.volume,target,1.5f*Time.unscaledDeltaTime);
        }

        private void DrawMusicLevel()
        {
            GUI.Label(new Rect(283,275,180,23),"Музыка",speaker);
            GUI.Label(new Rect(494,275,58,23),Mathf.RoundToInt(musicVolume*100)+"%",control);
            float level=GUI.HorizontalSlider(new Rect(287,308,258,24),musicVolume,0f,1f,woodTrackStyle,woodGrabberStyle);
            if(!Mathf.Approximately(level,musicVolume))
            {
                musicVolume=level;
                PlayerPrefs.SetFloat(MusicSetting,level);
            }
            if(Event.current.rawType==EventType.MouseUp)PlayerPrefs.Save();
        }
    }
}
