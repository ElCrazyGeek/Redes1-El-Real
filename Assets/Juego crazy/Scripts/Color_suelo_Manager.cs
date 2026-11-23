using UnityEngine;
using System.Collections.Generic;
using Mirror;

public class FloorGridManager : NetworkBehaviour
{
    public static FloorGridManager Instance;

    [Header("Configuración de Cuadrícula")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private int columns = 14;
    [SerializeField] private int rows = 8;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector2 origin = new Vector2(-6.5f, -3.5f);

    [Header("Colores de Equipo")]
    [SerializeField] private Color teamAColor = new Color(0.2f, 0.8f, 1f, 0.65f);   // Cian / Azul
    [SerializeField] private Color teamBColor = new Color(1f, 0.35f, 0.35f, 0.65f); // Rojo / Coral
    [SerializeField] private Color neutralColor = new Color(0.25f, 0.25f, 0.25f, 0.4f);

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
                    tile.Paint(neutralColor, 0);
                    tiles[new Vector2Int(x, y)] = tile;
                }
            }
        }
    }

    [Server]
    public void PaintTileForTeam(Vector3 position, int teamId)
    {
        Color targetColor = teamId == 1 ? teamAColor : teamBColor;
        RpcPaintTile(position, targetColor, teamId);
    }

    [ClientRpc]
    public void RpcPaintTile(Vector3 position, Color color, int teamId)
    {
        int x = Mathf.RoundToInt((position.x - origin.x) / cellSize);
        int y = Mathf.RoundToInt((position.y - origin.y) / cellSize);
        Vector2Int key = new Vector2Int(x, y);

        if (tiles.TryGetValue(key, out FloorTile tile))
        {
            tile.Paint(color, teamId);
        }
    }

    // --- UTILIDAD PARA LA IA (Segmento 2) ---
    // Busca la casilla más cercana que NO esté pintada por el equipo de la IA
    public Vector2 GetNearestUnpaintedTile(int myTeamId, Vector2 currentPos)
    {
        Vector2 bestPos = currentPos;
        float closestDist = float.MaxValue;

        foreach (var kvp in tiles)
        {
            FloorTile tile = kvp.Value;
            if (tile != null && tile.paintedByTeam != myTeamId)
            {
                float dist = Vector2.Distance(currentPos, tile.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestPos = tile.transform.position;
                }
            }
        }

        return bestPos;
    }

    // Opcional: Reiniciar toda la cancha a color neutro
    [Server]
    public void ResetAllTiles()
    {
        RpcResetAllTiles();
    }

    [ClientRpc]
    private void RpcResetAllTiles()
    {
        foreach (var tile in tiles.Values)
        {
            if (tile != null)
            {
                tile.Paint(neutralColor, 0);
            }
        }
    }
}