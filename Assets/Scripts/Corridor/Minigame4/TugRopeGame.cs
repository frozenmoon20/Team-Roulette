using System.Collections;
using UnityEngine;
using TeamRoulette.UI;

public class TugRopeGame : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "로프 장난감";

    public int requiredPulls = 3;

    public Vector3 tugOffset =
        new Vector3(0f, 0f, -0.35f);

    public float tugAngle = 12f;
    public float tugDuration = 0.35f;

    public GameObject nextPath;

    int pullCount;

    bool moving;
    bool completed;

    Vector3 startLocalPosition;
    Quaternion startLocalRotation;

    public string DisplayName => displayName;

    void Start()
    {
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;

        if (nextPath != null)
            nextPath.SetActive(false);
    }

    public void Interact()
    {
        if (completed)
            return;

        if (moving)
            return;

        StartCoroutine(TugRoutine());
    }

    IEnumerator TugRoutine()
    {
        moving = true;

        Vector3 targetPosition =
            startLocalPosition + tugOffset;

        Quaternion targetRotation =
            startLocalRotation *
            Quaternion.Euler(tugAngle, 0f, 0f);

        float halfDuration =
            tugDuration * 0.5f;

        float time = 0f;

        while (time < halfDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(time / halfDuration);

            transform.localPosition =
                Vector3.Lerp(
                    startLocalPosition,
                    targetPosition,
                    t
                );

            transform.localRotation =
                Quaternion.Slerp(
                    startLocalRotation,
                    targetRotation,
                    t
                );

            yield return null;
        }

        time = 0f;

        while (time < halfDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(time / halfDuration);

            transform.localPosition =
                Vector3.Lerp(
                    targetPosition,
                    startLocalPosition,
                    t
                );

            transform.localRotation =
                Quaternion.Slerp(
                    targetRotation,
                    startLocalRotation,
                    t
                );

            yield return null;
        }

        transform.localPosition =
            startLocalPosition;

        transform.localRotation =
            startLocalRotation;

        pullCount++;

        moving = false;

        if (pullCount >= requiredPulls)
        {
            CompleteGame();
        }
        else
        {
            PlayerInteractor.ShowHint(
                "조금 더 당겨보자. "
                + pullCount
                + " / "
                + requiredPulls
            );
        }
    }

    void CompleteGame()
{
    completed = true;

    if (OptionState.Instance != null)
    {
        OptionState.Instance.Unlock(
            OptionType.Pull
        );
    }

    if (nextPath != null)
        nextPath.SetActive(true);

    SceneUIContent ui =
        FindFirstObjectByType<SceneUIContent>();

    if (ui != null)
    {
        ui.CompleteObjective(
            "tug_game"
        );
    }

    PlayerInteractor.ShowHint(
        "당기기 기능이 해금되었다."
    );

    Debug.Log(
        "터그놀이 클리어 - Pull 해금!"
    );
}
}