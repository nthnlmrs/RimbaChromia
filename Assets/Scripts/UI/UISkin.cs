using System;
using UnityEngine;

/// <summary>
/// Semua sprite UI dari artist (Assets/Art/UI) dalam satu aset: Assets/Resources/UISkin.asset.
/// Kode UI membaca dari sini, jadi kalau artist mengganti gambar cukup drag sprite baru di Inspector.
/// Mockup susunan layar ada di Assets/Art/UI/_Reference.
/// </summary>
[CreateAssetMenu(fileName = "UISkin", menuName = "GIMERSIA/UI Skin")]
public class UISkin : ScriptableObject
{
    [Header("HUD")]
    public Sprite insightPlate;
    public Sprite insightIcon;
    public Sprite wallPlate;
    public Sprite wallFill;
    public Sprite pauseNormal;
    public Sprite pausePressed;

    [Header("Tombol (normal / ditekan)")]
    public Sprite exitNormal;
    public Sprite exitPressed;
    public Sprite nextNormal;
    public Sprite nextPressed;
    public Sprite quitNormal;
    public Sprite quitPressed;
    public Sprite restartNormal;
    public Sprite restartPressed;
    public Sprite startNow;

    [Header("Menu Pause")]
    public Sprite pausePanel;
    public Sprite volumeArrowLeft;
    public Sprite volumeArrowRight;
    public Sprite volumeLeafEmpty;
    [Tooltip("6 daun berwarna, urutan kiri ke kanan")]
    public Sprite[] volumeLeaves = new Sprite[6];

    [Header("Kartu tower & deskripsi (urutan: Red, Orange, Yellow, Green, Blue, Purple)")]
    public Sprite[] cards = new Sprite[6];
    public Sprite[] undeployedDescriptions = new Sprite[6];

    [Header("Panel info tower terpasang (per warna & tier)")]
    public TowerInfoArt[] towerInfo = new TowerInfoArt[0];

    [Header("Wave")]
    [Tooltip("Gambar 'WAVE N IS STARTING IN...', index 0 = wave 1")]
    public Sprite[] waveCountdown = new Sprite[3];
    public WavePlateArt[] wavePlates = new WavePlateArt[0];

    [Header("Layar akhir")]
    public Sprite victoryBanner;
    public Sprite defeatedBanner;

    private static UISkin cached;

    /// <summary>Dimuat dari Resources/UISkin. Null kalau aset belum ada (UI memakai gaya placeholder).</summary>
    public static UISkin Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<UISkin>("UISkin");
            return cached;
        }
    }

    public Sprite Card(TowerColor color) => Pick(cards, (int)color);
    public Sprite Description(TowerColor color) => Pick(undeployedDescriptions, (int)color);
    public Sprite Countdown(int waveNumber) => Pick(waveCountdown, waveNumber - 1);

    public TowerInfoArt InfoFor(TowerColor color, int tier)
    {
        foreach (TowerInfoArt art in towerInfo)
        {
            if (art != null && art.color == color && art.tier == tier) return art;
        }
        return null;
    }

    public Sprite WavePlate(int current, int total)
    {
        foreach (WavePlateArt plate in wavePlates)
        {
            if (plate != null && plate.current == current && plate.total == total) return plate.sprite;
        }
        return null;
    }

    private static Sprite Pick(Sprite[] list, int index)
    {
        return list != null && index >= 0 && index < list.Length ? list[index] : null;
    }
}

[Serializable]
public class TowerInfoArt
{
    public TowerColor color;
    [Range(1, 3)] public int tier = 1;
    public Sprite sprite;
    [Tooltip("Posisi label di gambar (pixel dari KIRI-ATAS): tengah baris 'ATK:' / 'HP:' dan ujung kanan labelnya")]
    public Vector2 atkLabel;
    public Vector2 hpLabel;
}

[Serializable]
public class WavePlateArt
{
    public int current = 1;
    public int total = 3;
    public Sprite sprite;
}
