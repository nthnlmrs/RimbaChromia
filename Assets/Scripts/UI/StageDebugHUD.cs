using UnityEngine;

/// <summary>
/// HUD SEMENTARA (IMGUI) untuk testing alur stage sebelum UI final dibuat (Prioritas 4).
/// Otomatis ditambahkan oleh WaveManager kalau "Show Debug Hud" dicentang.
/// Hapus / matikan setelah HUD asli (Canvas) jadi.
/// </summary>
public class StageDebugHUD : MonoBehaviour
{
    private WaveManager waves;
    private WallHealth wall;
    private InsightManager insight;

    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;

    private void Start()
    {
        waves = WaveManager.Instance;
        wall = FindFirstObjectByType<WallHealth>();
        insight = FindFirstObjectByType<InsightManager>();
    }

    private void BuildStyles()
    {
        if (labelStyle != null) return;

        float scale = Screen.height / 1080f;

        boxStyle = new GUIStyle(GUI.skin.box);
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(20 * scale),
            richText = true
        };
        titleStyle = new GUIStyle(labelStyle)
        {
            fontSize = Mathf.RoundToInt(44 * scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(22 * scale)
        };
    }

    private void OnGUI()
    {
        if (waves == null) return;
        BuildStyles();

        float scale = Screen.height / 1080f;
        float width = 420f * scale;
        float line = 28f * scale;

        GUILayout.BeginArea(new Rect(16f * scale, 16f * scale, width, line * 11), boxStyle);

        StageData stage = waves.CurrentStage;
        GUILayout.Label($"<b>{(stage != null ? stage.stageName : "No Stage")}</b>", labelStyle);
        GUILayout.Label($"Wave: {Mathf.Max(0, waves.CurrentWaveNumber)}/{waves.TotalWaves}   State: <b>{StateText()}</b>", labelStyle);

        if (waves.State == StageState.Preparing || waves.State == StageState.Break)
        {
            GUILayout.Label($"Wave berikutnya dalam: <b>{Mathf.CeilToInt(waves.StateTimeRemaining)}s</b>  (N = skip)", labelStyle);
        }
        else if (waves.State == StageState.WaveRunning)
        {
            GUILayout.Label($"Musuh tersisa: <b>{waves.EnemiesLeftThisWave}</b>", labelStyle);
        }
        else
        {
            GUILayout.Label(" ", labelStyle);
        }

        if (wall != null)
        {
            GUILayout.Label($"Wall HP: <b>{Mathf.CeilToInt(wall.CurrentHP)}/{wall.MaxHP}</b>", labelStyle);
        }

        if (insight != null)
        {
            GUILayout.Label($"Insight: <b>{insight.CurrentInsight}/{insight.MaxInsight}</b>", labelStyle);
        }

        if (FireManager.Instance != null)
        {
            string paused = FireManager.Instance.IsSimulationPaused ? " (jeda)" : "";
            GUILayout.Label($"Tile terbakar: <b>{FireManager.Instance.BurningCount}</b>{paused}", labelStyle);
        }

        GUILayout.Label("<i>1 Red  2 Blue  3 Green  |  F api  X padam  N skip</i>", labelStyle);
        GUILayout.EndArea();

        if (waves.State == StageState.Victory || waves.State == StageState.Defeat)
        {
            DrawResultPanel(scale);
        }
    }

    private void DrawResultPanel(float scale)
    {
        bool victory = waves.State == StageState.Victory;
        float w = 520f * scale;
        float h = 260f * scale;
        Rect rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        GUI.Box(rect, GUIContent.none, boxStyle);
        GUILayout.BeginArea(new Rect(rect.x + 20f * scale, rect.y + 20f * scale, w - 40f * scale, h - 40f * scale));

        GUILayout.Label(victory ? "VICTORY!" : "DEFEAT", titleStyle);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Restart Stage", buttonStyle, GUILayout.Height(50f * scale)))
        {
            waves.RestartStage();
        }

        if (victory && waves.HasNextStage &&
            GUILayout.Button("Next Stage", buttonStyle, GUILayout.Height(50f * scale)))
        {
            waves.LoadNextStage();
        }

        GUILayout.EndArea();
    }

    private string StateText()
    {
        switch (waves.State)
        {
            case StageState.Preparing: return "Persiapan";
            case StageState.WaveRunning: return "Wave berjalan";
            case StageState.Break: return "Jeda";
            case StageState.Victory: return "Menang";
            case StageState.Defeat: return "Kalah";
            default: return "Loading";
        }
    }
}
