using System;
using UnityEngine;

namespace SousakuTeam8.MeterGame
{
    public enum MeterInputMode
    {
        [InspectorName("押すたびに増加")]
        Press = 0,
        [InspectorName("長押しで連続増加")]
        Hold = 1
    }

    [Serializable]
    public sealed class MeterGameSettings
    {
        [Header("Gauge (0–1 = 0–100%)")]
        [Range(0f, 1f), Tooltip("開始時のゲージ量")]
        public float initialGauge = 0.8f;
        [Min(0f), Tooltip("毎秒自然に減る量")]
        public float drainPerSecond = 0.25f;
        [Min(0f), Tooltip("Spaceを1回押すごとに増える量。押しっぱなしでは増えません")]
        public float gainPerPress = 0.085f;
        [Min(0f), Tooltip("バーの自然減少と入力による加算の両方に掛ける速度倍率。0で停止")]
        public float gaugeSpeedMultiplier = 1f;
        [Tooltip("Pressは1回押すごとに加算、Holdは押している間に連続加算")]
        public MeterInputMode inputMode = MeterInputMode.Hold;
        [Min(0f), Tooltip("HoldモードでSpaceを押している間、毎秒加算する量。自然減少も同時に適用")]
        public float gainPerSecondWhileHeld = 0.5f;

        [Header("Moving target range")]
        [Range(0f, 1f)] public float minimumCenter = 0.25f;
        [Range(0f, 1f)] public float maximumCenter = 0.8f;
        [Range(0.01f, 1f)] public float minimumWidth = 0.18f;
        [Range(0.01f, 1f)] public float maximumWidth = 0.3f;
        [Min(0.1f), Tooltip("位置の移動時間の基準。通常はこの値の30〜60%秒で次の位置へ移動")]
        public float positionPeriod = 7f;
        [Min(0.1f), Tooltip("幅の変化時間の基準。通常はこの値の30〜60%秒で次の幅へ変化")]
        public float widthPeriod = 5f;
        [Min(0f), Tooltip("範囲の移動速度倍率。0で位置を固定、2で2倍速。Play中の変更も滑らかに反映")]
        public float positionSpeedMultiplier = 1f;
        [Min(0f), Tooltip("範囲の伸縮速度倍率。0で幅を固定、2で2倍速")]
        public float widthSpeedMultiplier = 1f;
        [Min(0.01f), Tooltip("1区間の最短変化時間（速度倍率を掛ける前の秒数）")]
        public float minimumTransitionSeconds = 0.12f;
        public bool useCustomInitialTarget;
        [Range(0f, 1f), Tooltip("Custom Initial Target有効時の初期中心。移動可能区間内に補正")]
        public float initialTargetCenter = 0.8f;
        [Range(0.01f, 1f), Tooltip("Custom Initial Target有効時の初期幅。幅の最小・最大内に補正")]
        public float initialTargetWidth = 0.3f;

        [Header("Random target motion")]
        [Range(0f, 1f), Tooltip("ランダム性全体の強さ。0なら規則的に往復し、急変・狭まりイベントも無効")]
        public float randomness = 1f;
        [Range(0f, 1f), Tooltip("移動先のランダム性。0なら最小・最大中心の往復、1なら自由な移動先")]
        public float positionRandomness = 1f;
        [Range(0f, 1f), Tooltip("幅のランダム性。0なら最小・最大幅の往復、1なら自由な幅")]
        public float widthRandomness = 1f;
        [Range(0f, 1f), Tooltip("変化時間のばらつき。0ならPeriodの45%秒で一定、1なら30〜60%秒")]
        public float timingRandomness = 1f;
        [Range(0f, 1f), Tooltip("移動先の更新時、ときどき速い変動になる確率")]
        public float suddenMoveChance = 0.25f;
        [Min(1f), Tooltip("速い変動時の速度倍率。位置が飛ぶことはありません")]
        public float suddenSpeedMultiplier = 3f;
        [Range(0f, 1f), Tooltip("幅の更新時、最小幅に近い狭い範囲を選ぶ確率")]
        public float narrowChance = 0.35f;
        [Range(0f, 1f), Tooltip("狭まり時に幅の区間の下側何割から選ぶか。0なら常に最小幅、1なら幅区間全体")]
        public float narrowWidthFraction = 0.15f;
        [Tooltip("0はゲーム開始ごとにランダム。0以外で同じ動きを再現")]
        public int randomSeed;

        [Header("Heart / result")]
        [Range(0f, 1f)] public float initialHeart;
        [Min(0f), Tooltip("範囲内に連続で何秒とどまるとハートが増え始めるか")]
        public float requiredHoldSeconds = 1f;
        [Min(0f), Tooltip("条件達成後のハートの毎秒加算量。0.12なら毎秒12%、0で加算停止")]
        public float heartGainPerSecond = 0.05f;
        [Min(0f), Tooltip("範囲外に出ても連続滞在時間を維持する猶予秒数。範囲外では加算しない")]
        public float outsideGraceSeconds;
        [Min(0f), Tooltip("範囲外で猶予を超えたときの毎秒ハート減少量。0なら獲得済みの量を保持")]
        public float heartLossPerSecond;
        [Min(0.1f), Tooltip("ゲーム全体の制限時間（秒）")]
        public float timeLimitSeconds = 60f;

        public void Validate()
        {
            initialGauge = Mathf.Clamp01(initialGauge);
            drainPerSecond = Mathf.Max(0f, drainPerSecond);
            gainPerPress = Mathf.Max(0f, gainPerPress);
            gaugeSpeedMultiplier = Mathf.Max(0f, gaugeSpeedMultiplier);
            gainPerSecondWhileHeld = Mathf.Max(0f, gainPerSecondWhileHeld);
            minimumCenter = Mathf.Clamp01(minimumCenter);
            maximumCenter = Mathf.Clamp(maximumCenter, minimumCenter, 1f);
            minimumWidth = Mathf.Clamp(minimumWidth, 0.01f, 1f);
            maximumWidth = Mathf.Clamp(maximumWidth, minimumWidth, 1f);
            positionPeriod = Mathf.Max(0.1f, positionPeriod);
            widthPeriod = Mathf.Max(0.1f, widthPeriod);
            positionSpeedMultiplier = Mathf.Max(0f, positionSpeedMultiplier);
            widthSpeedMultiplier = Mathf.Max(0f, widthSpeedMultiplier);
            minimumTransitionSeconds = Mathf.Max(0.01f, minimumTransitionSeconds);
            initialTargetCenter = Mathf.Clamp(initialTargetCenter, minimumCenter, maximumCenter);
            initialTargetWidth = Mathf.Clamp(initialTargetWidth, minimumWidth, maximumWidth);
            randomness = Mathf.Clamp01(randomness);
            positionRandomness = Mathf.Clamp01(positionRandomness);
            widthRandomness = Mathf.Clamp01(widthRandomness);
            timingRandomness = Mathf.Clamp01(timingRandomness);
            suddenMoveChance = Mathf.Clamp01(suddenMoveChance);
            suddenSpeedMultiplier = Mathf.Max(1f, suddenSpeedMultiplier);
            narrowChance = Mathf.Clamp01(narrowChance);
            narrowWidthFraction = Mathf.Clamp01(narrowWidthFraction);
            initialHeart = Mathf.Clamp01(initialHeart);
            outsideGraceSeconds = Mathf.Max(0f, outsideGraceSeconds);
            heartLossPerSecond = Mathf.Max(0f, heartLossPerSecond);
            requiredHoldSeconds = Mathf.Max(0f, requiredHoldSeconds);
            heartGainPerSecond = Mathf.Max(0f, heartGainPerSecond);
            timeLimitSeconds = Mathf.Max(0.1f, timeLimitSeconds);
        }
    }
}
