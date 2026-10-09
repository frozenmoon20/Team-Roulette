using UnityEngine;

namespace TeamRoulette.UI
{
    // Only attached in UI_Workbench, never included in the reusable prefab.
    public class UIPreview : MonoBehaviour
    {
        public GameUI ui;
        public Transform clockMarker;
        public AudioSource music, effect;
        AudioClip musicClip, effectClip;
        void Start()
        {
            // Quiet generated tones allow both volume sliders to be checked without external assets.
            musicClip = Tone("UI preview music", 220, 1);
            effectClip = Tone("UI preview effect", 440, .15f);
            if (music) { music.clip = musicClip; music.loop = true; music.Play(); }
        }
        AudioClip Tone(string name, float hz, float duration)
        {
            const int rate = 22050;
            float[] samples = new float[Mathf.RoundToInt(rate * duration)];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(2 * Mathf.PI * hz * i / rate) * .025f;
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        void Update()
        {
            if (clockMarker) clockMarker.Rotate(0, 35 * Time.deltaTime, 0);
            if (ui.IsPaused) return;
            if (Input.GetKeyDown(KeyCode.F1)) ui.GetComponent<SceneUIContent>().PlayDialogue();
            if (Input.GetKeyDown(KeyCode.F2))
            {
                var content = ui.GetComponent<SceneUIContent>();
                var next = content.objectives.Find(x => x.visible && !x.completed);
                if (next != null) content.CompleteObjective(next.id);
                if (effect && effectClip) effect.PlayOneShot(effectClip);
            }
            if (Input.GetKeyDown(KeyCode.F3) && OptionState.Instance)
                foreach (OptionType option in System.Enum.GetValues(typeof(OptionType))) OptionState.Instance.Unlock(option);
        }
        void OnDestroy()
        {
            if (musicClip) Destroy(musicClip);
            if (effectClip) Destroy(effectClip);
        }
    }
}
