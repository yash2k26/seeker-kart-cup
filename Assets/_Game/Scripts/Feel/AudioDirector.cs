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
        // This scene's music: its own track first, then the rest of the pool. A race does not start on the track the
        // last one played, so two races in a row never sound the same (the menu has a pool of one).
        [SerializeField] AudioClip[] tracks = System.Array.Empty<AudioClip>();
        static AudioClip lastPlayed;
        [SerializeField] float musicVolume = 0.38f;
        [SerializeField] float crossfade = 0.6f;

        [Header("UI")]
        [SerializeField] AudioClip[] clicks = System.Array.Empty<AudioClip>();
        [SerializeField] AudioClip back, confirm;

        [Header("Race")]
        [SerializeField] AudioClip three, two, one, go, finalLap, lapDone, win, lose, pickup, useItem;

        [Header("Items")]
        // What using each item sounds like, by its ItemManager name (a rocket and a sun bomb carry their own sound
        // in the world, on the thing itself), and what it sounds like to be handed one or to be hit by one.
        [SerializeField] string[] itemNames = System.Array.Empty<string>();
        [SerializeField] AudioClip[] itemClips = System.Array.Empty<AudioClip>();
        [SerializeField] AudioClip podUp, bombUp, hit;

        [Header("Cues")]
        // what the HUD only showed before: a drift tier charging, a place gained, driving the wrong way, a button
        // that cannot be pressed
        [SerializeField] AudioClip tierUp, positionUp, warn, error;
        float lastWarn = -10f;

        AudioSource music, sfx, voice;
        // The music's level is two things multiplied: the crossfade between tracks, and a dip while someone speaks
        // (a driver's line, the countdown, FINAL LAP) so the words are never fought for.
        float fadeLevel, duckGain = 1f, duckUntil;
        const float Ducked = 0.32f;

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
            fadeLevel = MusicLevel; music.volume = fadeLevel; music.priority = 0;
            music.ignoreListenerPause = true;   // the pause screen pauses the race, not the soundtrack (it dips under the menu instead)

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.priority = 32;
            sfx.ignoreListenerPause = true;   // the pause overlay's own clicks

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.spatialBlend = 0f; voice.priority = 16;

            PlayMusic(Pick());
        }

        void Update()
        {
            float target = AudioListener.pause ? 0.45f : Time.unscaledTime < duckUntil ? Ducked : 1f;
            duckGain = Mathf.MoveTowards(duckGain, target, Time.unscaledDeltaTime * (target < duckGain ? 6f : 1.2f));   // dip fast, come back slowly
            if (music != null) music.volume = fadeLevel * duckGain;
        }

        /// The music steps back for `seconds` (a voice, a callout).
        public void Duck(float seconds) => duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);

        /// A driver's line (Voices.Say): one at a time, a new line cuts the last.
        public void Voice(AudioClip clip)
        {
            if (clip == null || voice == null) return;
            voice.Stop();
            voice.clip = clip;
            voice.Play();
            Duck(clip.length + 0.2f);
        }

        AudioClip Pick()
        {
            if (tracks == null || tracks.Length == 0) return null;
            var fresh = System.Array.FindAll(tracks, t => t != null && t != lastPlayed);
            if (fresh.Length == 0) return lastPlayed = tracks[0];
            // the circuit's own track half the time it is free, any of the others the rest
            return lastPlayed = fresh[0] == tracks[0] && Random.value < 0.5f ? tracks[0] : fresh[Random.Range(0, fresh.Length)];
        }

        /// A second director spawned by a newly loaded scene hands over its clip set and dies.
        void Adopt(AudioDirector other)
        {
            clicks = other.clicks; back = other.back; confirm = other.confirm;
            three = other.three; two = other.two; one = other.one; go = other.go;
            finalLap = other.finalLap; lapDone = other.lapDone; win = other.win; lose = other.lose;
            pickup = other.pickup; useItem = other.useItem;
            itemNames = other.itemNames; itemClips = other.itemClips; podUp = other.podUp; bombUp = other.bombUp; hit = other.hit;
            tierUp = other.tierUp; positionUp = other.positionUp; warn = other.warn; error = other.error;
            musicVolume = other.musicVolume;
            tracks = other.tracks;
            PlayMusic(Pick());
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || music == null || music.clip == clip) return;
            StopAllCoroutines();
            StartCoroutine(Crossfade(clip));
        }

        IEnumerator Crossfade(AudioClip clip)
        {
            float start = fadeLevel;
            for (float t = 0; music.isPlaying && t < crossfade; t += Time.unscaledDeltaTime)
            {
                fadeLevel = Mathf.Lerp(start, 0f, t / crossfade);
                yield return null;
            }
            music.clip = clip; fadeLevel = 0f; music.Play();
            for (float t = 0; t < crossfade; t += Time.unscaledDeltaTime)
            {
                fadeLevel = Mathf.Lerp(0f, MusicLevel, t / crossfade);
                yield return null;
            }
            fadeLevel = MusicLevel;
        }

        float MusicLevel => Settings.Music ? musicVolume : 0f;

        /// The MUSIC switch: the track keeps playing under it, so turning it back on picks up where it is.
        public void ApplyVolume() => fadeLevel = MusicLevel;

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || sfx == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume);
        }

        // ---- UI ----
        float lastClick = -1f;

        /// Every button clicks on press-in (PressFeel); the action it fires may click again on release. One tap is one
        /// click: a second inside 180 ms is the same tap.
        public void Click()
        {
            if (clicks.Length == 0 || Time.unscaledTime - lastClick < 0.18f) return;
            lastClick = Time.unscaledTime;
            Play(clicks[Random.Range(0, clicks.Length)], 0.7f, Random.Range(0.97f, 1.04f));
        }
        public void Back() => Play(back, 0.7f);
        public void Confirm() => Play(confirm, 0.8f);

        // ---- race ----
        /// n counts 3, 2, 1 and then 0 for GO.
        public void Countdown(int n)
        {
            Duck(n == 0 ? 1.2f : 1f);
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
            if (isFinalLap) { Duck(2.2f); StartCoroutine(After(0.45f, finalLap, 1f)); }
        }

        public void Finish(bool won) => Play(won ? win : lose, 1f);
        /// The box's chime, then the item arriving on the kart: the pod clacking up, a bomb dropping into the cradle.
        public void Pickup(string item)
        {
            Play(pickup, 0.85f, Random.Range(0.98f, 1.05f));
            var arrive = item == "Rocket" || item == "Homing Item" ? podUp : item == "Bomb" || item == "Sun Bomb" ? bombUp : null;
            if (arrive != null) StartCoroutine(After(0.16f, arrive, 0.8f));
        }

        public void UseItem(string item)
        {
            int i = System.Array.IndexOf(itemNames, item);
            if (i >= 0 && i < itemClips.Length && itemClips[i] != null) Play(itemClips[i], 0.95f);
            else if (i < 0) Play(useItem, 0.85f);      // (a name mapped to no clip sounds from the world instead)
        }

        public void Hit() => Play(hit, 0.9f, Random.Range(0.95f, 1.05f));

        /// Each drift tier a step higher than the last (the sparks changing colour, heard).
        public void TierUp(int tier) => Play(tierUp, 0.55f, 1f + 0.18f * Mathf.Clamp(tier - 1, 0, 3));
        public void PositionUp() => Play(positionUp, 0.4f, 1.05f);
        /// WRONG WAY, repeated while it is true, never faster than the callout blinks.
        public void Warn() { if (Time.unscaledTime - lastWarn < 0.9f) return; lastWarn = Time.unscaledTime; Play(warn, 0.6f); }
        public void Error() => Play(error, 0.7f);

        IEnumerator After(float delay, AudioClip clip, float volume)
        {
            yield return new WaitForSecondsRealtime(delay);
            Play(clip, volume);
        }
    }
}
