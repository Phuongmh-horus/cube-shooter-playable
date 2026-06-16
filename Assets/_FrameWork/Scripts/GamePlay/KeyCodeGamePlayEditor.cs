#if DEVELOPMENT_TOOL
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyCodeGamePlayEditor : MonoSingleton<KeyCodeGamePlayEditor>
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            LevelSystem.Instance.HandleWin();
        if (Input.GetKeyDown(KeyCode.F3))
            LevelSystem.Instance.HandleLose();
    }
}
#endif