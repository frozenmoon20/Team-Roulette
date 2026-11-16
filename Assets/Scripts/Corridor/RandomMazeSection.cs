using System.Collections.Generic;
using UnityEngine;

public class RandomMazeSection : MonoBehaviour
{
    [SerializeField] private GameObject mazeCellPrefab;
    [SerializeField] private int width = 5;
    [SerializeField] private int height = 5;
    [SerializeField] private float cellSize = 4f;

    private MazeCell[,] cells;
    private bool[,] visited;

    private class MazeEdge
    {
        public int x;
        public int z;
        public int nextX;
        public int nextZ;

        public MazeEdge(int x, int z, int nextX, int nextZ)
        {
            this.x = x;
            this.z = z;
            this.nextX = nextX;
            this.nextZ = nextZ;
        }
    }

    private void Start()
    {
        CreateCells();
        GeneratePaths();
    }

    private void CreateCells()
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
    }

    private void GeneratePaths()
    {
        List<MazeEdge> edges = new List<MazeEdge>();

        visited[0, 0] = true;
        AddEdges(0, 0, edges);

        while (edges.Count > 0)
        {
            int randomIndex = Random.Range(0, edges.Count);
            MazeEdge edge = edges[randomIndex];

            edges.RemoveAt(randomIndex);

            if (visited[edge.nextX, edge.nextZ])
                continue;

            RemoveWallBetween(
                edge.x,
                edge.z,
                edge.nextX,
                edge.nextZ
            );

            visited[edge.nextX, edge.nextZ] = true;

            AddEdges(
                edge.nextX,
                edge.nextZ,
                edges
            );
        }
    }

    private void AddEdges(int x, int z, List<MazeEdge> edges)
    {
        TryAddEdge(x, z, x + 1, z, edges);
        TryAddEdge(x, z, x - 1, z, edges);
        TryAddEdge(x, z, x, z + 1, edges);
        TryAddEdge(x, z, x, z - 1, edges);
    }

    private void TryAddEdge(
        int x,
        int z,
        int nextX,
        int nextZ,
        List<MazeEdge> edges
    )
    {
        if (nextX < 0 || nextX >= width ||
            nextZ < 0 || nextZ >= height)
            return;

        if (visited[nextX, nextZ])
            return;

        edges.Add(new MazeEdge(
            x,
            z,
            nextX,
            nextZ
        ));
    }

    private void RemoveWallBetween(
        int x,
        int z,
        int nextX,
        int nextZ
    )
    {
        if (nextX > x)
        {
            cells[x, z].RemoveEastWall();
            cells[nextX, nextZ].RemoveWestWall();
        }
        else if (nextX < x)
        {
            cells[x, z].RemoveWestWall();
            cells[nextX, nextZ].RemoveEastWall();
        }
        else if (nextZ > z)
        {
            cells[x, z].RemoveNorthWall();
            cells[nextX, nextZ].RemoveSouthWall();
        }
        else if (nextZ < z)
        {
            cells[x, z].RemoveSouthWall();
            cells[nextX, nextZ].RemoveNorthWall();
        }
    }
}