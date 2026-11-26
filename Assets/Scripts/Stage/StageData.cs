using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Satu stage = layout map + daftar wave (sesuai tabel Stage Design di GDD).
///
/// Kode layout (1 karakter = 1 tile):
///   G = Grass Field (bisa pasang tower, bisa terbakar)
///   P = Bare Path (jalur musuh)
///   F = Surrounding Forest (tidak bisa diinteraksi)
///   W = The Wall
///   1..9 = titik spawn musuh (dihitung sebagai Bare Path). Jalurnya dicari otomatis ke Wall.
///
/// Layout ditulis PERSIS seperti gambar di GDD (portrait 17 kolom x 21 baris, Wall di atas).
/// Kalau rotateClockwise = true, map diputar 90 derajat searah jarum jam saat dimainkan
/// supaya jadi landscape 21 x 17 (Wall di kanan) dan pas di layar 16:9.
/// </summary>
[CreateAssetMenu(fileName = "NewStageData", menuName = "GIMERSIA/Stage Data")]
public class StageData : ScriptableObject
{
    [Header("Info")]
    public string stageName = "Stage";

    [Header("Map (baris paling atas = bagian atas gambar GDD)")]
    public string[] layoutRows = new string[0];
    public bool rotateClockwise = true;

    [Tooltip("Nama titik spawn, index 0 = karakter '1' di layout, index 1 = '2', dst.")]
    public string[] spawnPointNames = { "Kiri", "Kanan", "Tengah" };

    [Header("Cerita & Aturan Khusus")]
    [Tooltip("Visual novel sebelum stage dimulai (GDD: Opening sebelum Tutorial)")]
    public DialogueData introDialogue;
    [Tooltip("Visual novel setelah stage dimenangkan (GDD: Ending setelah Stage 3)")]
    public DialogueData outroDialogue;
    [Tooltip("GDD Tutorial: tower bisa dipasang & di-merge tanpa biaya")]
    public bool freeCost = false;
    [Tooltip("Tampilkan panduan Taduh langkah demi langkah")]
    public bool isTutorial = false;
    [Tooltip("Credit bergulir setelah Ending (hanya dipakai di stage terakhir). Boleh pakai rich text TMP")]
    [TextArea(6, 20)]
    public string credits =
        "<size=80><b>RIMBA CHROMIA</b></size>\n\nGimersia 2026\n\n\n" +
        "<color=#FFD24D>Game Design</color>\n(team name)\n\n" +
        "<color=#FFD24D>Programming</color>\n(team name)\n\n" +
        "<color=#FFD24D>Art</color>\n(team name)\n\n" +
        "<color=#FFD24D>Story</color>\n(team name)\n\n\n" +
        "Thank you for protecting the forest.";

    [Header("Timing (GDD: 15 detik persiapan & 15 detik jeda antar wave)")]
    public float prepTime = 15f;
    public float breakTime = 15f;

    [Header("Waves")]
    public WaveData[] waves = new WaveData[0];

    /// <summary>
    /// Layout final yang dipakai GridManager. Baris index 0 = bagian ATAS layar.
    /// </summary>
    public string[] GetPlayLayout()
    {
        if (layoutRows == null || layoutRows.Length == 0)
        {
            return new string[0];
        }

        string[] source = new string[layoutRows.Length];
        for (int i = 0; i < layoutRows.Length; i++)
        {
            source[i] = (layoutRows[i] ?? string.Empty).Trim();
        }

        if (!rotateClockwise)
        {
            return source;
        }

        int height = source.Length;
        int width = source[0].Length;
        string[] rotated = new string[width];
        char[] buffer = new char[height];

        // Putar 90 derajat searah jarum jam: baris baru i = kolom lama i dibaca dari bawah ke atas
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                string oldRow = source[height - 1 - j];
                buffer[j] = i < oldRow.Length ? oldRow[i] : 'F';
            }
            rotated[i] = new string(buffer);
        }

        return rotated;
    }

    public string GetSpawnPointName(int spawnPoint)
    {
        int index = spawnPoint - 1;
        if (spawnPointNames != null && index >= 0 && index < spawnPointNames.Length)
        {
            return spawnPointNames[index];
        }
        return $"Spawn {spawnPoint}";
    }
}

[Serializable]
public class WaveData
{
    [Tooltip("Grup yang titik spawn-nya sama keluar berurutan; titik spawn berbeda keluar bersamaan.")]
    public SpawnGroup[] groups = new SpawnGroup[0];

    public int TotalEnemies
    {
        get
        {
            int total = 0;
            if (groups != null)
            {
                foreach (SpawnGroup group in groups)
                {
                    if (group != null) total += Mathf.Max(0, group.count);
                }
            }
            return total;
        }
    }

    public IEnumerable<int> SpawnPoints()
    {
        HashSet<int> seen = new HashSet<int>();
        if (groups == null) yield break;

        foreach (SpawnGroup group in groups)
        {
            if (group != null && seen.Add(group.spawnPoint))
            {
                yield return group.spawnPoint;
            }
        }
    }
}

[Serializable]
public class SpawnGroup
{
    [Tooltip("Angka titik spawn di layout (1, 2, 3, ...)")]
    public int spawnPoint = 1;
    public EnemyData enemy;
    public int count = 1;
    [Tooltip("Jeda antar musuh di grup ini (detik)")]
    public float interval = 1.5f;
    [Tooltip("Jeda sebelum grup ini mulai keluar (detik)")]
    public float startDelay = 0f;
}
