using UnityEngine;

public class KeyRotate : MonoBehaviour
{
    public float rotateSpeed = 80f;

    void Update()
    {
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f);
    }
}