using System.Collections;
using UnityEngine;

namespace KartCup
{
    /// Everything the race and menus need to be heard: music with a crossfade across scenes, UI clicks, and the
    /// race-structure cues (countdown voice, lap, final lap, finish). The kart's own engine, tyre and boost audio
    /// is Powerslide's KartAudio on the kart prefab — this never touches that.
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [Header("Music")]
        [SerializeField] AudioClip track;                 // the clip this scene wants
        [SerializeField] float musicVolume = 0.38f;
        [SerializeField] float crossfade = 0.6f;

        [Header("UI")]
        [SerializeField] AudioClip[] clicks = System.Array.Empty<AudioClip>();
        [SerializeField] AudioClip back, confirm;

        [Header("Race")]
        [SerializeField] AudioClip three, two, one, go, finalLap, lapDone, win, lose, pickup, useItem;

        AudioSource music, sfx;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Instance.Adopt(this);                     // the new scene's clips replace ours, the object does not
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            music = gameObject.AddComponent<AudioSource>();
            music.loop = true; music.playOnAwake = false; music.spatialBlend = 0f;
            music.volume = musicVolume; music.priority = 0;

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.priority = 32;
            sfx.ignoreListenerPause = true;   // the pause overlay's own clicks

            PlayMusic(track);
        }

        /// A second director spawned by a newly loaded scene hands over its clip set and dies.
        void Adopt(AudioDirector other)
        {
            clicks = other.clicks; back = other.back; confirm = other.confirm;
            three = other.three; two = other.two; one = other.one; go = other.go;
            finalLap = other.finalLap; lapDone = other.lapDone; win = other.win; lose = other.lose;
            pickup = other.pickup; useItem = other.useItem;
            musicVolume = other.musicVolume;
            PlayMusic(other.track);
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || music == null || music.clip == clip) return;
            StopAllCoroutines();
            StartCoroutine(Crossfade(clip));
        }

        IEnumerator Crossfade(AudioClip clip)
        {
            float start = music.volume;
            for (float t = 0; music.isPlaying && t < crossfade; t += Time.unscaledDeltaTime)
            {
                music.volume = Mathf.Lerp(start, 0f, t / crossfade);
                yield return null;
            }
            music.clip = clip; music.volume = 0f; music.Play();
            for (float t = 0; t < crossfade; t += Time.unscaledDeltaTime)
            {
                music.volume = Mathf.Lerp(0f, musicVolume, t / crossfade);
                yield return null;
            }
            music.volume = musicVolume;
        }

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || sfx == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume);
        }

        // ---- UI ----
        public void Click()
        {
            if (clicks.Length == 0) return;
            Play(clicks[Random.Range(0, clicks.Length)], 0.7f, Random.Range(0.97f, 1.04f));
        }
        public void Back() => Play(back, 0.7f);
        public void Confirm() => Play(confirm, 0.8f);

        // ---- race ----
        /// n counts 3, 2, 1 and then 0 for GO.
        public void Countdown(int n)
        {
            switch (n)
            {
                case 3: Play(three); break;
                case 2: Play(two); break;
                case 1: Play(one); break;
                case 0: Play(go, 1f); break;
            }
        }

        public void Lap(bool isFinalLap)
        {
            Play(lapDone, 0.8f);
            if (isFinalLap) StartCoroutine(After(0.45f, finalLap, 1f));
        }

        public void Finish(bool won) => Play(won ? win : lose, 1f);
        public void Pickup() => Play(pickup, 0.8f, Random.Range(0.98f, 1.05f));
        public void UseItem() => Play(useItem, 0.85f);

        IEnumerator After(float delay, AudioClip clip, float volume)
        {
            yield return new WaitForSecondsRealtime(delay);
            Play(clip, volume);
        }
    }
}
