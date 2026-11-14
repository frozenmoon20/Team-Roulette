
using System;
using UnityEngine;

public class MiniGame1ItemSounds : MonoBehaviour
{
    [Serializable]
    public class ItemSoundSettings
    {
        [Header("대상 물건")]
        public GrabbableItem item;

        [Header("효과음")]
        public AudioClip pickupSound;
        public AudioClip dropSound;

        [Range(0f, 1f)]
        public float volume = 0.8f;
    }

    [Header("인형 / 쿠션 등 물건별 효과음")]
    public ItemSoundSettings[] items;

    private GrabbableItem previousHeld;

    private void OnEnable()
    {
        PlayerHand hand = PlayerHand.Instance;

        previousHeld = hand != null
            ? hand.Held
            : null;
    }

    private void Update()
    {
        PlayerHand hand = PlayerHand.Instance;

        if (hand == null || items == null)
            return;

        GrabbableItem currentHeld = hand.Held;

        // 이전 프레임과 비교하여 집기 / 내려놓기 감지
        if (currentHeld != previousHeld)
        {
            foreach (ItemSoundSettings entry in items)
            {
                if (entry == null || entry.item == null)
                    continue;

                // 물건을 새로 집었을 때
                if (currentHeld == entry.item)
                {
                    PlaySound(
                        entry.pickupSound,
                        entry.volume
                    );
                }

                // 들고 있던 물건을 손에서 놓았을 때
                if (previousHeld == entry.item)
                {
                    PlaySound(
                        entry.dropSound,
                        entry.volume
                    );
                }
            }
        }

        previousHeld = currentHeld;
    }

    private void PlaySound(
        AudioClip clip,
        float volume)
    {
        if (clip == null)
            return;

        AudioManager.Play(clip, volume);
    }
}
