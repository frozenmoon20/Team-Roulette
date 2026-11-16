using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [SerializeField] private GameObject mazeCellPrefab;
    [SerializeField] private int width = 8;
    [SerializeField] private int height = 8;
    [SerializeField] private float cellSize = 4f;

    private MazeCell[,] cells;
    private bool[,] visited;

    private void Start()
    {
        GenerateMaze();
    }

    private void GenerateMaze()
    {
        cells = new MazeCell[width, height];
        visited = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
               Vector3 position =
    transform.position +
    new Vector3(x * cellSize, 0f, z * cellSize);
                GameObject cellObject = Instantiate(
                    mazeCellPrefab,
                    position,
                    Quaternion.identity,
                    transform
                );

                cells[x, z] = cellObject.GetComponent<MazeCell>();
            }
        }

        GeneratePath(0, 0);
        OpenExit();
    }

    private void GeneratePath(int x, int z)
    {
        visited[x, z] = true;

        List<Vector2Int> directions = new List<Vector2Int>
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0)
        };

        Shuffle(directions);

        foreach (Vector2Int direction in directions)
        {
            int nextX = x + direction.x;
            int nextZ = z + direction.y;

            if (nextX < 0 || nextX >= width ||
                nextZ < 0 || nextZ >= height ||
                visited[nextX, nextZ])
            {
                continue;
            }

            RemoveWalls(x, z, nextX, nextZ, direction);

            GeneratePath(nextX, nextZ);
        }
    }

    private void RemoveWalls(
        int x,
        int z,
        int nextX,
        int nextZ,
        Vector2Int direction
    )
    {
        MazeCell current = cells[x, z];
        MazeCell next = cells[nextX, nextZ];

        if (direction == Vector2Int.up)
        {
            current.RemoveNorthWall();
            next.RemoveSouthWall();
        }
        else if (direction == Vector2Int.right)
        {
            current.RemoveEastWall();
            next.RemoveWestWall();
        }
        else if (direction == Vector2Int.down)
        {
            current.RemoveSouthWall();
            next.RemoveNorthWall();
        }
        else if (direction == Vector2Int.left)
        {
            current.RemoveWestWall();
            next.RemoveEastWall();
        }
    }

    private void Shuffle(List<Vector2Int> directions)
    {
        for (int i = directions.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            Vector2Int temp = directions[i];
            directions[i] = directions[randomIndex];
            directions[randomIndex] = temp;
        }
    }

    private void OpenExit()
{
    int exitX = width / 2;
    int exitZ = height - 1;

    cells[exitX, exitZ].RemoveNorthWall();
}
}