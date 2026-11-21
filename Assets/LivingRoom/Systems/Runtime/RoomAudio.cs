using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamRoulette.Room3
{
    [AddComponentMenu("Team Roulette/Room Audio")]
    [DisallowMultipleComponent]
    public sealed class RoomAudio : MonoBehaviour
    {
        private enum Category
        {
            [InspectorName("배경음악")] BackgroundMusic,
            [InspectorName("효과음")] SoundEffect
        }

        [Serializable]
        private sealed class Sound
        {
            [Tooltip("게임 코드가 사운드를 찾을 때 사용하는 이름입니다. 클립이나 음량을 바꿀 때는 이 이름을 유지하세요.")]
            public string id;
            public Category category;
            public AudioClip clip;
            [Tooltip("이 클립의 기본 음량입니다. 게임 메뉴의 배경음악 또는 효과음 음량도 함께 적용됩니다.")]
            [Range(0f, 1f)] public float volume = 1f;
            public bool loop;
            public bool playOnStart;
            [Tooltip("소리가 기본 음량까지 커지는 시간입니다. 단위는 초입니다.")]
            [Min(0f)] public float fadeInSeconds;

            [NonSerialized] public AudioSource source;
            [NonSerialized] public bool requested;
            [NonSerialized] public float fadeElapsed;

            public OptionType Option => category == Category.BackgroundMusic ? OptionType.BGM : OptionType.SFX;
        }

        [Header("이 씬의 배경음악과 효과음")]
        [SerializeField] private List<Sound> sounds = new List<Sound>();

        public static RoomAudio For(Component caller)
        {
            foreach (var audio in FindObjectsByType<RoomAudio>(FindObjectsSortMode.None))
                if (audio.isActiveAndEnabled && audio.gameObject.scene == caller.gameObject.scene)
                    return audio;

            return null;
        }

        private void OnEnable() => OptionState.OnOptionChanged += OnOptionChanged;

        private void Start()
        {
            foreach (var sound in sounds)
                if (sound.playOnStart)
                    Play(sound.id);
        }

        private void OnDisable()
        {
            OptionState.OnOptionChanged -= OnOptionChanged;
            foreach (var sound in sounds)
                StopSound(sound);
        }

        private void LateUpdate()
        {
            foreach (var sound in sounds)
            {
                if (!sound.source || !sound.source.isPlaying)
                    continue;

                sound.fadeElapsed += Time.unscaledDeltaTime;
                ApplyVolume(sound);
            }
        }

        public void Play(string id) => Play(id, null);

        public void Play(string id, AudioSource existingSource)
        {
            var sound = sounds.Find(entry => entry.id == id);
            if (!isActiveAndEnabled || sound == null || !sound.clip || (sound.loop && sound.requested))
                return;

            var unlocked = !OptionState.Instance || OptionState.Instance.IsUnlocked(sound.Option);
            if (existingSource)
                sound.source = existingSource;
            sound.requested = sound.loop || (sound.playOnStart && !unlocked);
            if (unlocked)
                StartSound(sound);
        }

        public void Stop(string id)
        {
            var sound = sounds.Find(entry => entry.id == id);
            if (sound != null)
                StopSound(sound);
        }

        public void StopAll()
        {
            foreach (var sound in sounds)
                StopSound(sound);
        }

        public float ClipDuration(string id)
        {
            var sound = sounds.Find(entry => entry.id == id);
            return sound != null && sound.clip &&
                (!OptionState.Instance || OptionState.Instance.IsUnlocked(sound.Option))
                ? sound.clip.length : 0f;
        }

        private static void StopSound(Sound sound)
        {
            sound.requested = false;
            if (sound.source)
                sound.source.Stop();
        }

        private void StartSound(Sound sound)
        {
            if (!sound.source)
                sound.source = gameObject.AddComponent<AudioSource>();

            sound.source.playOnAwake = false;
            sound.source.clip = sound.clip;
            sound.source.loop = sound.loop;
            sound.fadeElapsed = 0f;
            ApplyVolume(sound);
            if (sound.loop)
                sound.source.Play();
            else
                sound.source.PlayOneShot(sound.clip);
        }

        public void SetPaused(string id, bool paused)
        {
            var sound = sounds.Find(entry => entry.id == id);
            if (sound == null || !sound.source)
                return;

            if (paused)
                sound.source.Pause();
            else
                sound.source.UnPause();
        }

        private static void ApplyVolume(Sound sound)
        {
            var key = sound.category == Category.BackgroundMusic ? "Roulette.UI.Music" : "Roulette.UI.Effects";
            float fade = sound.fadeInSeconds > 0f
                ? Mathf.SmoothStep(0f, 1f, sound.fadeElapsed / sound.fadeInSeconds)
                : 1f;
            sound.source.volume = sound.volume * Mathf.Clamp01(PlayerPrefs.GetFloat(key, 1f)) * fade;
        }

        private void OnOptionChanged(OptionType option, bool unlocked)
        {
            foreach (var sound in sounds)
            {
                if (option != sound.Option)
                    continue;

                if (!unlocked && sound.source)
                    sound.source.Stop();
                if (unlocked && sound.requested && sound.clip)
                {
                    sound.requested = sound.loop;
                    StartSound(sound);
                }
            }
        }
    }
}
