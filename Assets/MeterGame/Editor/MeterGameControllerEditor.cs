using UnityEditor;
using UnityEngine;

namespace SousakuTeam8.MeterGame.Editor
{
    [CustomEditor(typeof(MeterGameController)), CanEditMultipleObjects]
    public sealed class MeterGameControllerEditor : UnityEditor.Editor
    {
        private readonly bool[] expanded = { true, true, true, false, true, false };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("割合は 0〜1（1 = 100%）、時間は秒です。変更はPlay中も反映されます。初期値・シードの反映は再スタートしてください。", MessageType.Info);
            Section(0, "操作バー・入力", new[] {
                "initialGauge", "gaugeSpeedMultiplier", "drainPerSecond", "inputMode", "gainPerPress", "gainPerSecondWhileHeld"
            }, new[] { "初期ゲージ量", "バー速度倍率", "自然減少量 / 秒", "入力方式", "1回押した時の加算量", "長押し中の加算量 / 秒" });
            if (expanded[0])
            {
                var mode = serializedObject.FindProperty("settings.inputMode");
                if (!mode.hasMultipleDifferentValues)
                    EditorGUILayout.HelpBox(mode.intValue == (int)MeterInputMode.Hold
                        ? "Spaceを押している間は時間に応じて滑らかに加算し、離すと自然減少します。長押し中も自然減少するため、加算量 / 秒を自然減少量より大きくするとゲージが増えます。"
                        : "Spaceを押した瞬間に1回分を加算します。押しっぱなしでは追加加算しません。", MessageType.Info);
            }
            Section(1, "目標範囲・スピード", new[] {
                "minimumCenter", "maximumCenter", "minimumWidth", "maximumWidth", "positionPeriod", "positionSpeedMultiplier", "widthPeriod", "widthSpeedMultiplier", "minimumTransitionSeconds"
            }, new[] { "中心位置の最小", "中心位置の最大", "範囲幅の最小", "範囲幅の最大", "位置変化の基準時間", "位置の速度倍率（0で停止）", "幅変化の基準時間", "幅の速度倍率（0で停止）", "最短変化時間（倍率適用前）" });
            Section(2, "ランダム性・急変・狭まり", new[] {
                "randomness", "positionRandomness", "widthRandomness", "timingRandomness", "suddenMoveChance", "suddenSpeedMultiplier", "narrowChance", "narrowWidthFraction", "randomSeed"
            }, new[] { "ランダム性全体（0で規則的）", "移動先のランダム性", "幅のランダム性", "変化時間のランダム性", "急変の発生確率", "急変時の速度倍率", "狭まりの発生確率", "狭まり時に選ぶ幅の割合", "乱数シード（0で毎回ランダム）" });
            Section(3, "初期の目標範囲", new[] {
                "useCustomInitialTarget", "initialTargetCenter", "initialTargetWidth"
            }, new[] { "初期範囲を指定する", "初期中心位置", "初期範囲幅" });
            Section(4, "ハート・猶予・制限時間", new[] {
                "initialHeart", "requiredHoldSeconds", "heartGainPerSecond", "outsideGraceSeconds", "heartLossPerSecond", "timeLimitSeconds"
            }, new[] { "初期ハート量", "必要な連続滞在時間", "ハート加算量 / 秒", "範囲外の猶予時間", "猶予後のハート減少量 / 秒", "制限時間" });
            expanded[5] = EditorGUILayout.Foldout(expanded[5], "表示参照・結果イベント", true);
            if (expanded[5])
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("barView"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onSuccess"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onFailure"));
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("難易度プリセット（設定値を置き換えます）", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("やさしい")) ApplyPreset(0);
                if (GUILayout.Button("標準")) ApplyPreset(1);
                if (GUILayout.Button("難しい")) ApplyPreset(2);
            }
            if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
            {
                var controller = (MeterGameController)target;
                if (controller.State != null)
                    EditorGUILayout.HelpBox($"{controller.State.Result}  残り {controller.State.RemainingSeconds:F1} 秒\nゲージ {controller.State.Gauge:P0} / ハート {controller.State.Heart:P0} / 連続滞在 {controller.State.HoldSeconds:F2} 秒", MessageType.None);
                if (GUILayout.Button("現在の設定で再スタート")) controller.RestartGame();
                Repaint();
            }
        }

        private void Section(int index, string title, string[] names, string[] labels)
        {
            expanded[index] = EditorGUILayout.Foldout(expanded[index], title, true);
            if (!expanded[index]) return;
            EditorGUI.indentLevel++;
            var settings = serializedObject.FindProperty("settings");
            var mode = settings.FindPropertyRelative("inputMode");
            for (int i = 0; i < names.Length; i++)
            {
                var property = settings.FindPropertyRelative(names[i]);
                bool inactiveInput = !mode.hasMultipleDifferentValues &&
                    ((names[i] == "gainPerPress" && mode.intValue != (int)MeterInputMode.Press) ||
                     (names[i] == "gainPerSecondWhileHeld" && mode.intValue != (int)MeterInputMode.Hold));
                using (new EditorGUI.DisabledScope(inactiveInput))
                    EditorGUILayout.PropertyField(property, new GUIContent(labels[i], property.tooltip));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void ApplyPreset(int difficulty)
        {
            var preset = new MeterGameSettings();
            if (difficulty == 0)
            {
                preset.drainPerSecond = 0.1f;
                preset.minimumWidth = 0.28f;
                preset.maximumWidth = 0.4f;
                preset.positionSpeedMultiplier = preset.widthSpeedMultiplier = 0.7f;
                preset.randomness = 0.3f;
                preset.suddenMoveChance = 0.1f;
                preset.narrowChance = 0.1f;
                preset.requiredHoldSeconds = 0.3f;
                preset.heartGainPerSecond = 0.18f;
                preset.outsideGraceSeconds = 0.2f;
                preset.timeLimitSeconds = 40f;
            }
            else if (difficulty == 2)
            {
                preset.drainPerSecond = 0.18f;
                preset.minimumWidth = 0.1f;
                preset.maximumWidth = 0.22f;
                preset.positionSpeedMultiplier = preset.widthSpeedMultiplier = 1.3f;
                preset.suddenMoveChance = 0.4f;
                preset.narrowChance = 0.55f;
                preset.requiredHoldSeconds = 0.8f;
                preset.heartGainPerSecond = 0.1f;
                preset.heartLossPerSecond = 0.025f;
                preset.timeLimitSeconds = 25f;
            }
            preset.Validate();
            Undo.SetCurrentGroupName("Apply MeterGame difficulty preset");
            foreach (var obj in targets)
            {
                // SerializedProperty handles private fields, Undo, scene persistence and
                // prefab overrides together; only the settings subtree is changed.
                var data = new SerializedObject(obj);
                var settings = data.FindProperty("settings");
                foreach (var field in typeof(MeterGameSettings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    var property = settings.FindPropertyRelative(field.Name);
                    object value = field.GetValue(preset);
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Float: property.floatValue = (float)value; break;
                        case SerializedPropertyType.Integer: property.intValue = (int)value; break;
                        case SerializedPropertyType.Boolean: property.boolValue = (bool)value; break;
                        case SerializedPropertyType.Enum: property.intValue = System.Convert.ToInt32(value); break;
                    }
                }
                data.ApplyModifiedProperties();
            }
            serializedObject.Update();
        }
    }
}
