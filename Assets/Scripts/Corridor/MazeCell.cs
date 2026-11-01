using UnityEngine;

public class MazeCell : MonoBehaviour
{
    [SerializeField] private GameObject northWall;
    [SerializeField] private GameObject southWall;
    [SerializeField] private GameObject westWall;
    [SerializeField] private GameObject eastWall;

    public void RemoveNorthWall()
    {
        northWall.SetActive(false);
    }

    public void RemoveSouthWall()
    {
        southWall.SetActive(false);
    }

    public void RemoveWestWall()
    {
        westWall.SetActive(false);
    }

    public void RemoveEastWall()
    {
        eastWall.SetActive(false);
    }
}