using UnityEngine;
using TeamRoulette.UI;

public class TimedLeverSection : MonoBehaviour
{
    public Transform door;
    public Transform playerFailResetPoint;

    public float timeLimit = 5f;
    public float doorOpenHeight = 3f;
    public float doorSpeed = 4f;

    bool attemptRunning;
    bool completed;

    float remainingTime;

    Vector3 closedPosition;
    Vector3 openPosition;

    void Start()
    {
        if (door != null)
        {
            closedPosition =
                door.position;

            openPosition =
                closedPosition +
                Vector3.up *
                doorOpenHeight;
        }
    }

    void Update()
    {
        if (door == null)
            return;

        if (
            attemptRunning ||
            completed
        )
        {
            door.position =
                Vector3.MoveTowards(
                    door.position,
                    openPosition,
                    doorSpeed *
                    Time.deltaTime
                );
        }
        else
        {
            door.position =
                Vector3.MoveTowards(
                    door.position,
                    closedPosition,
                    doorSpeed *
                    Time.deltaTime
                );
        }

        if (
            !attemptRunning ||
            completed
        )
            return;

        remainingTime -=
            Time.deltaTime;

        if (remainingTime <= 0f)
        {
            FailAttempt();
        }
    }

    public void StartAttempt()
    {
        if (completed)
            return;

        if (attemptRunning)
            return;

        attemptRunning = true;

        remainingTime =
            timeLimit;

        PlayerInteractor.ShowHint(
            "문이 열렸다. 제한 시간 안에 통과해야 한다."
        );
    }

    public void CompleteSection()
    {
        if (!attemptRunning)
            return;

        completed = true;

        attemptRunning = false;

        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null)
        {
            ui.CompleteObjective(
                "timed_door"
            );

        }

        PlayerInteractor.ShowHint(
            "제한 시간 안에 통과했다."
        );

        Debug.Log(
            "시간제한 레버 구간 클리어!"
        );
    }

    void FailAttempt()
    {
        attemptRunning = false;

        if (door != null)
        {
            door.position =
                closedPosition;
        }

        FirstPersonPlayer player =
            FindFirstObjectByType<FirstPersonPlayer>();

        if (
            player != null &&
            playerFailResetPoint != null
        )
        {
            CharacterController controller =
                player.GetComponent<CharacterController>();

            if (controller != null)
                controller.enabled = false;

            player.transform.position =
                playerFailResetPoint.position;

            player.transform.rotation =
                playerFailResetPoint.rotation;

            if (controller != null)
                controller.enabled = true;
        }

        PlayerInteractor.ShowHint(
            "시간 초과. 다시 시도한다."
        );

        Debug.Log(
            "시간제한 레버 실패"
        );
    }
}