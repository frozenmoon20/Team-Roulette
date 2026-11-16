using System.Collections;
using UnityEngine;
using TMPro;
using System;

// 기능이 해금되면 화면 위에 "OO 기능이 해금되었습니다." 알림을 띄우고, 해금 효과음을 낸다.
// 효과음은 AudioManager로 보내므로 효과음(SFX) 해금 전에는 안 나고, 설정창 효과음 볼륨을 따른다.
// (효과음 자체가 해금되는 순간에는 이미 해금된 뒤라 소리가 난다 → "효과음이 돌아왔다"는 첫 소리가 됨)
public class UnlockNotice : MonoBehaviour
{
    public TMP_Text noticeText;
    public CanvasGroup canvasGroup;
    public float fadeTime = 0.5f;
    public float showTime = 2f;

    [Header("해금 효과음 (비워 두면 소리 없음)")]
    public AudioClip unlockClip;
    [Range(0f, 1f)] public float unlockVolume = 0.8f;

    Coroutine running;

    void Awake()
    {
        canvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        OptionState.OnOptionChanged += HandleOptionChanged;
    }

    void OnDisable()
    {
        OptionState.OnOptionChanged -= HandleOptionChanged;
    }

    void HandleOptionChanged(OptionType type, bool isUnlocked)
    {
        if (!isUnlocked)
        {
            return;
        }

        string name = GetDisplayName(type);
        if (name == null) return;

        Show(name + " 기능이 해금되었습니다.");
        AudioManager.Play(unlockClip, unlockVolume);


    }

    string GetDisplayName(OptionType type)
    {
        switch (type)
        {
            case OptionType.Move: return "이동";
            case OptionType.RotateCamera: return "카메라 회전";
            case OptionType.Grab: return "잡기";
            case OptionType.Push: return "밀기";
            case OptionType.Pull: return "당기기";
            case OptionType.Hand: return "손";
            case OptionType.SFX: return "효과음";
            case OptionType.BGM: return "배경음";
            case OptionType.Jump: return "점프";
            case OptionType.Run: return "달리기";
            default: return null;   // MessageSent 같은 진행 기록은 알림을 띄우지 않음


        }
    }

    public void Show(string message)
    {
        noticeText.text = message;
        if (running != null)
        {
            StopCoroutine(running);
        }
        running = StartCoroutine(ShowRoutine());


    }

    IEnumerator ShowRoutine()
    {
        yield return Fade(canvasGroup.alpha, 1f);
        yield return new WaitForSeconds(showTime);
        yield return Fade(1f, 0f);
        running = null;
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}