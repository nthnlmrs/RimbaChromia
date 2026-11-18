using UnityEngine;

public class GameExit : MonoBehaviour
{
    public void ExitGame()
    {
        Application.Quit();
    }

    #if UNITY_EDITOR
    public void ExitGameInEditor()
    {
        UnityEditor.EditorApplication.isPlaying = false;
    }
    #endif
}
