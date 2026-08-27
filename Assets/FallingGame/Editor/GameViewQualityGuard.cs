#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Sousakusai8.MiniGame.Editor
{
    /// <summary>
    /// Prevents the Unity Game view from rendering at a reduced resolution and
    /// then enlarging that image with point filtering on high-DPI displays.
    /// This only affects the Editor preview and is not included in player builds.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameViewQualityGuard
    {
        private const string MenuPath = "yoogen/画質/Game View の低解像度表示を解除";

        static GameViewQualityGuard()
        {
            EditorApplication.delayCall += ApplyAutomatically;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        private static void ApplyFromMenu()
        {
            int changedViewCount = ApplyToOpenGameViews();
            Debug.Log(
                changedViewCount > 0
                    ? $"Game Viewの低解像度表示を解除しました（{changedViewCount}画面）。"
                    : "Game Viewはすでに高画質設定です。",
                null);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall += ApplyAutomatically;
            }
        }

        private static void ApplyAutomatically()
        {
            ApplyToOpenGameViews();
        }

        private static int ApplyToOpenGameViews()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
            {
                return 0;
            }

            int changedViewCount = 0;
            UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
            foreach (UnityEngine.Object gameView in gameViews)
            {
                SerializedObject serializedView = new(gameView);
                bool changed = DisableLowResolutionModes(serializedView);
                changed |= SetBilinearPreviewFiltering(serializedView);

                if (!changed)
                {
                    continue;
                }

                serializedView.ApplyModifiedPropertiesWithoutUndo();
                if (gameView is EditorWindow window)
                {
                    window.Repaint();
                }

                changedViewCount++;
            }

            return changedViewCount;
        }

        private static bool DisableLowResolutionModes(SerializedObject serializedView)
        {
            SerializedProperty lowResolution =
                serializedView.FindProperty("m_LowResolutionForAspectRatios");
            if (lowResolution == null)
            {
                return false;
            }

            bool changed = false;
            if (lowResolution.isArray)
            {
                for (int index = 0; index < lowResolution.arraySize; index++)
                {
                    SerializedProperty mode = lowResolution.GetArrayElementAtIndex(index);
                    if (mode.intValue == 0)
                    {
                        continue;
                    }

                    mode.intValue = 0;
                    changed = true;
                }
            }
            else if (lowResolution.boolValue)
            {
                lowResolution.boolValue = false;
                changed = true;
            }

            return changed;
        }

        private static bool SetBilinearPreviewFiltering(SerializedObject serializedView)
        {
            SerializedProperty filterMode = serializedView.FindProperty("m_TextureFilterMode");
            int bilinear = (int)FilterMode.Bilinear;
            if (filterMode == null || filterMode.intValue == bilinear)
            {
                return false;
            }

            filterMode.intValue = bilinear;
            return true;
        }
    }
}
#endif
