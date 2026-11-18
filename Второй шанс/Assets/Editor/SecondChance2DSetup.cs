#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

// Настройки редактора, а НЕ трёхмерная система игры.
// Никаких 3D-объектов или физики; геймплей отображается на плоском UI Canvas.
[InitializeOnLoad]
public static class SecondChance2DSetup
{
    static SecondChance2DSetup()
    {
        EditorApplication.delayCall += Apply2DMode;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Второй шанс/Включить 2D и открыть Game")]
    public static void Apply2DAndOpenGame()
    {
        Apply2DMode();
        FocusGameView();
    }

    private static void Apply2DMode()
    {
        if (EditorSettings.defaultBehaviorMode != EditorBehaviorMode.Mode2D)
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

        foreach (SceneView scene in SceneView.sceneViews)
        {
            if (scene == null) continue;
            scene.in2DMode = true;
            scene.showGrid = false;
            scene.Repaint();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += FocusGameView;
    }

    private static void FocusGameView()
    {
        // GameView недоступен как публичный тип, но это штатное окно Unity.
        Type type = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (type == null) return;
        EditorWindow window = EditorWindow.GetWindow(type);
        if (window != null) window.Focus();
    }
}
#endif
