using System.Collections;
using UnityEngine;
using TMPro;
using System;

public class UnlockNotice : MonoBehaviour
{
    public TMP_Text noticeText;
    public CanvasGroup canvasGroup;
    public float fadeTime = 0.5f;
    public float showTime = 2f;

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
        if(!isUnlocked)
        {
            return;
        }

        string name = GetDisplayName(type);
        if (name == null) return;

        Show(name + " 기능이 해금되었습니다.");


    }

    string GetDisplayName(OptionType type)
    {
        switch (type) {
            case OptionType.Move:   return "이동";
            case OptionType.RotateCamera: return "카메라 회전";
            case OptionType.Grab: return "잡기";
            case OptionType.Push: return "밀기";
            case OptionType.Pull: return "당기기";
            case OptionType.Hand: return "손";
            case OptionType.SFX: return "효과음";
            case OptionType.BGM: return "배경음";
            default: return null;


        }
    }

    public void Show(string message)
    {
        noticeText.text = message;
        if(running != null)
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