using UnityEditor;
using UnityEngine;
using SousakuTeam8.MeterGame.Timing;

namespace SousakuTeam8.MeterGame.Editor
{
    [CustomEditor(typeof(TimingMeterController)), CanEditMultipleObjects]
    public sealed class TimingMeterControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("ゲージが成功範囲に入ってからSpaceを1回押します。セット数・必要成功数・シードの変更は再スタート、速度・判定の変更は次セットから反映します。", MessageType.Info);
            DrawDefaultInspector();
            if (!Application.isPlaying || serializedObject.isEditingMultipleObjects) return;
            var controller = (TimingMeterController)target;
            if (controller.Session != null)
                EditorGUILayout.HelpBox($"{controller.Phase}\nセット {controller.Session.CurrentSetNumber}/{controller.Session.SetCount}　成功 {controller.Session.SuccessCount}/{controller.Session.RequiredSuccesses}\nゲージ {controller.Session.Round.Gauge:P0}　残り {controller.Session.Round.RemainingSeconds:F2} 秒", MessageType.None);
            if (GUILayout.Button("現在の設定で再スタート")) controller.RestartGame();
            Repaint();
        }
    }
}
