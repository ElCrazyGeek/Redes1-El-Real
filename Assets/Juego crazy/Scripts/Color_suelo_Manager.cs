using UnityEngine;
using System.Collections.Generic;
using Mirror;

public class FloorGridManager : NetworkBehaviour
{
    public static FloorGridManager Instance;

    [Header("Configuración")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private int columns = 14;
    [SerializeField] private int rows = 8;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector2 origin = new Vector2(-6.5f, -3.5f);

    private Dictionary<Vector2Int, FloorTile> tiles = new Dictionary<Vector2Int, FloorTile>();

    private void Awake()
    {
        Instance = this;
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector3 worldPos = new Vector3(origin.x + (x * cellSize), origin.y + (y * cellSize), 1f);
                GameObject tileObj = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);

                if (tileObj.TryGetComponent<FloorTile>(out FloorTile tile))
                {
                    tiles[new Vector2Int(x, y)] = tile;
                }
            }
        }
    }

    [ClientRpc]
    public void RpcPaintTile(Vector3 position, Color color)
    {
        int x = Mathf.RoundToInt((position.x - origin.x) / cellSize);
        int y = Mathf.RoundToInt((position.y - origin.y) / cellSize);
        Vector2Int key = new Vector2Int(x, y);

        if (tiles.TryGetValue(key, out FloorTile tile))
        {
            tile.Paint(color);
        }
    }
}