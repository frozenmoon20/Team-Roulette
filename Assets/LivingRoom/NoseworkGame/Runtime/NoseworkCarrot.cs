using System.Collections;
using UnityEngine;

namespace TeamRoulette.Room3.Nosework
{
    public sealed class NoseworkCarrot : MonoBehaviour, IInteractable, IHasName
    {
        public int index;
        public NoseworkGame game;
        public bool IsPulled { get; private set; }
        public bool Selectable => game && game.AcceptingInput && !IsPulled;
        public Vector3 OriginalPosition { get; private set; }
        public Quaternion OriginalRotation { get; private set; }
        Vector3 originalScale;
        bool captured;
        public string DisplayName => game && game.Running ? "당근 선택" : "겨울이와 노즈워크 놀이";
        public void Capture()
        {
            if (captured) return;
            captured = true;
            OriginalPosition = transform.localPosition;
            OriginalRotation = transform.localRotation;
            originalScale = transform.localScale;
        }
        void Awake() { Capture(); }
        public void Interact() { if (game) { if (game.Running) game.Select(this); else game.Begin(); } }
        public void Restore()
        {
            Capture(); IsPulled = false; gameObject.SetActive(true);
            transform.localPosition = OriginalPosition; transform.localRotation = OriginalRotation;
            transform.localScale = originalScale;
        }
        public IEnumerator Cue(float duration)
        {
            Capture(); Vector3 start = transform.position;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration));
                transform.position = start + Vector3.up * (.075f * pulse);
                transform.localRotation = OriginalRotation * Quaternion.Euler(0, 0, Mathf.Sin(t * 23) * 10 * pulse);
                yield return null;
            }
            transform.localPosition = OriginalPosition; transform.localRotation = OriginalRotation;
        }
        public IEnumerator Pull(float height, float duration)
        {
            Vector3 start = transform.position;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                transform.position = start + Vector3.up * (height * Mathf.SmoothStep(0, 1, t / duration));
                yield return null;
            }
            IsPulled = true; gameObject.SetActive(false);
        }
    }
}
