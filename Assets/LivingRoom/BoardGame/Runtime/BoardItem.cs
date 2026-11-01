using UnityEngine;

public enum BoardItemState { Floor, Board, Held, Trash }

public sealed class BoardItem : MonoBehaviour
{
    public string id;
    public Vector2 size;
    public BoardItemState state;
    public Vector2 boardPosition;
    public float angle;
    public int order;
    public LineRenderer outline;
    public Collider hitbox;

    public void SetOutline(bool visible)
    {
        if (outline) outline.enabled = visible;
    }
}
