using UnityEditor;
using UnityEngine;
using SousakuTeam8.MeterGame.Timing;

namespace SousakuTeam8.MeterGame.Editor
{
    [CustomEditor(typeof(TimingMeterController)), CanEditMultipleObjects]
    public sealed class TimingMeterControllerEditor : UnityEditor.Editor
    {
        private bool showRandomness;
        private bool showResultEffects;
        private bool showFade;
        private bool showEvents;
        private bool showReferences;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("成功範囲は幅だけを指定し、位置は各セット開始時に自動抽選します。幅・速度・最小移動割合は次セットから、セット数・必要成功数・シードは再スタートで反映します。", MessageType.Info);
            var settings = serializedObject.FindProperty("settings");

            EditorGUILayout.LabelField("ゲージ速度・成功判定", EditorStyles.boldLabel);
            var speed = settings.FindPropertyRelative("fillPerSecond");
            EditorGUI.BeginChangeCheck();
            float percentPerSecond = EditorGUILayout.FloatField(new GUIContent("往復速度（% / 秒）", "40なら片道2.5秒、往復5秒。増加・減少は同じ速度です。"), speed.floatValue * 100f);
            if (EditorGUI.EndChangeCheck()) speed.floatValue = Mathf.Max(1f, percentPerSecond) / 100f;

            var zoneWidth = settings.FindPropertyRelative("successZoneWidth");
            EditorGUI.BeginChangeCheck();
            float widthPercent = EditorGUILayout.Slider(new GUIContent("成功範囲の幅（%）", "10ならバー全体の10%の幅。位置は指定できません。"), zoneWidth.floatValue * 100f, 1f, 100f);
            if (EditorGUI.EndChangeCheck()) zoneWidth.floatValue = widthPercent / 100f;
            var minimumChange = settings.FindPropertyRelative("minimumPositionChange");
            EditorGUI.BeginChangeCheck();
            float changePercent = EditorGUILayout.Slider(new GUIContent("位置の最小移動割合（%）", "配置可能な移動幅に対する割合。幅10%・移動割合10%ならバー全体の9%以上離して抽選。"), minimumChange.floatValue * 100f, 0f, 50f);
            if (EditorGUI.EndChangeCheck()) minimumChange.floatValue = changePercent / 100f;
            DrawField(settings, "randomSeed", "乱数シード（0で毎回ランダム）");
            float rate = Mathf.Max(0.01f, speed.floatValue);
            EditorGUILayout.HelpBox($"成功範囲の幅：{zoneWidth.floatValue:P0}（セット内では位置固定）\n片道：約 {1f / rate:F2} 秒 ／ 往復：約 {2f / rate:F2} 秒\n片道の範囲内通過時間：約 {zoneWidth.floatValue / rate:F2} 秒\n幅100%ではバー全体が成功範囲になり、位置は変化しません。\n未入力なら同速度で往復し続けます。範囲外でSpaceを押すとセット失敗です。\n※ ランダム速度・セットごとの加速を適用する前の目安です。", MessageType.None);

            DrawField(settings, "setCount", "セット数 n");
            DrawField(settings, "requiredSuccesses", "必要成功数 m（n未満）");

            showRandomness = EditorGUILayout.Foldout(showRandomness, "速度のランダム性・セットごとの加速", true);
            if (showRandomness)
            {
                DrawField(settings, "randomizeSpeed", "セットごとに速度をランダム化");
                using (new EditorGUI.DisabledScope(!settings.FindPropertyRelative("randomizeSpeed").boolValue && !settings.FindPropertyRelative("randomizeSpeed").hasMultipleDifferentValues))
                {
                    DrawField(settings, "minimumSpeedMultiplier", "ランダム速度の最小倍率");
                    DrawField(settings, "maximumSpeedMultiplier", "ランダム速度の最大倍率");
                }
                DrawField(settings, "speedIncreasePerSet", "セットごとの速度加算倍率");
            }
            showResultEffects = EditorGUILayout.Foldout(showResultEffects, "成功・失敗の結果演出", true);
            if (showResultEffects)
            {
                DrawField(settings, "resultDisplaySeconds", "結果表示時間（秒）");
                DrawField(settings, "animateResults", "結果アニメーションを有効化");
                DrawField(settings, "resultScaleAmount", "拡縮の強さ");
                DrawField(settings, "successFlashCount", "成功時の強調回数");
            }
            showFade = EditorGUILayout.Foldout(showFade, "セット間のフェード", true);
            if (showFade)
            {
                DrawField(settings, "useFade", "フェードを有効化");
                DrawField(settings, "fadeOutSeconds", "フェードアウト時間（秒）");
                DrawField(settings, "fadeHoldSeconds", "非表示で待つ時間（秒）");
                DrawField(settings, "fadeInSeconds", "フェードイン時間（秒）");
            }
            showEvents = EditorGUILayout.Foldout(showEvents, "結果イベント", true);
            if (showEvents)
            {
                EditorGUILayout.HelpBox("範囲外のSpaceで On Set Failure が発火します。全セット終了時に必要成功数に届かなければ On Failure が発火します。", MessageType.Info);
                foreach (string name in new[] { "onSetSuccess", "onSetFailure", "onSuccess", "onFailure" })
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            }
            showReferences = EditorGUILayout.Foldout(showReferences, "コンポーネント参照", true);
            if (showReferences)
                foreach (string name in new[] { "input", "presenter", "feedback", "transition" })
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(name));

            serializedObject.ApplyModifiedProperties();
            if (!Application.isPlaying || serializedObject.isEditingMultipleObjects) return;
            var controller = (TimingMeterController)target;
            if (controller.Session != null)
                EditorGUILayout.HelpBox($"{controller.Phase}\nセット {controller.Session.CurrentSetNumber}/{controller.Session.SetCount}　成功 {controller.Session.SuccessCount}/{controller.Session.RequiredSuccesses}\n今回の成功範囲：{controller.Session.Round.SuccessZoneStart:P1}〜{controller.Session.Round.SuccessZoneEnd:P1}（表示のみ）\n使用中の乱数シード：{controller.Session.RandomSeed}\nゲージ {controller.Session.Round.Gauge:P0}　経過 {controller.Session.Round.ElapsedSeconds:F2} 秒", MessageType.None);
            if (GUILayout.Button("現在の設定で再スタート")) controller.RestartGame();
            Repaint();
        }

        private static void DrawField(SerializedProperty settings, string name, string label)
        {
            var property = settings.FindPropertyRelative(name);
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }
    }
}
