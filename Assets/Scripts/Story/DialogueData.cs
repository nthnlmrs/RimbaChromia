using System;
using UnityEngine;

/// <summary>
/// Satu adegan visual novel (GDD: "Visual novel - played in the start of the game and the end of the game").
/// Tim penulis cukup mengedit aset ini: nama pembicara, teks, dan CG (gambar) per baris.
/// Kalau CG kosong, latar memakai warna backgroundColor (placeholder sebelum CG dari artist jadi).
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "GIMERSIA/Dialogue")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines = new DialogueLine[0];
}

[Serializable]
public class DialogueLine
{
    [Tooltip("Kosongkan untuk narasi")]
    public string speaker = "";
    [TextArea(2, 5)] public string text = "";
    [Tooltip("Gambar CG adegan (GDD Asset List: Opening / Closing CG). Kosong = pakai warna latar")]
    public Sprite cg;
    public Color backgroundColor = new Color(0.08f, 0.12f, 0.09f);
    [Tooltip("Layar bergetar saat baris ini muncul (misal suara ledakan)")]
    public bool shake = false;
}
