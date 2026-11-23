using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pola area berbasis jumlah tile, mengikuti istilah di GDD:
/// "5 grid"  = Plus5     (tile itu sendiri + 4 tetangga atas/bawah/kiri/kanan)
/// "9 grid"  = Square9   (kotak 3x3)
/// "13 grid" = Diamond13 (belah ketupat, jarak Manhattan <= 2)
/// </summary>
public enum AreaPattern
{
    None,
    Single,
    Plus5,
    Square9,
    Diamond13
}

public static class GridArea
{
    private static readonly Dictionary<AreaPattern, Vector2Int[]> cache =
        new Dictionary<AreaPattern, Vector2Int[]>();

    /// <summary>Offset (row, column) relatif dari tile tengah, termasuk (0,0).</summary>
    public static Vector2Int[] GetOffsets(AreaPattern pattern)
    {
        if (cache.TryGetValue(pattern, out Vector2Int[] cached))
        {
            return cached;
        }

        List<Vector2Int> offsets = new List<Vector2Int>();

        switch (pattern)
        {
            case AreaPattern.Single:
                offsets.Add(Vector2Int.zero);
                break;

            case AreaPattern.Plus5:
                AddManhattan(offsets, 1);
                break;

            case AreaPattern.Square9:
                for (int r = -1; r <= 1; r++)
                {
                    for (int c = -1; c <= 1; c++)
                    {
                        offsets.Add(new Vector2Int(r, c));
                    }
                }
                break;

            case AreaPattern.Diamond13:
                AddManhattan(offsets, 2);
                break;
        }

        Vector2Int[] result = offsets.ToArray();
        cache[pattern] = result;
        return result;
    }

    private static void AddManhattan(List<Vector2Int> offsets, int radius)
    {
        for (int r = -radius; r <= radius; r++)
        {
            for (int c = -radius; c <= radius; c++)
            {
                if (Mathf.Abs(r) + Mathf.Abs(c) <= radius)
                {
                    offsets.Add(new Vector2Int(r, c));
                }
            }
        }
    }
}
