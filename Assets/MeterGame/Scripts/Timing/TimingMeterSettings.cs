using System;
using UnityEngine;

namespace SousakuTeam8.MeterGame.Timing
{
    [Serializable]
    public sealed class TimingMeterSettings
    {
        [Header("セット構成（変更後は再スタート）")]
        [Min(2), InspectorName("セット数 n")] public int setCount = 5;
        [Min(1), InspectorName("必要成功数 m（n未満）")] public int requiredSuccesses = 3;

        [Header("ゲージと判定（次のセットから反映）")]
        [Min(0.01f), InspectorName("ゲージ増加量 / 秒"), Tooltip("0.4なら2.5秒で0からMAXへ到達")]
        public float fillPerSecond = 0.4f;
        [Range(0f, 1f), InspectorName("成功範囲の開始位置"), Tooltip("0.9なら90%〜100%で成功")]
        public float successZoneStart = 0.9f;
        [Min(0f), InspectorName("MAX到達後の猶予秒数")]
        public float maximumGraceSeconds = 0.35f;
        [InspectorName("セットごとの速度をランダム化")] public bool randomizeSpeed;
        [Min(0.01f), InspectorName("ランダム速度の最小倍率")] public float minimumSpeedMultiplier = 0.85f;
        [Min(0.01f), InspectorName("ランダム速度の最大倍率")] public float maximumSpeedMultiplier = 1.15f;
        [Min(0f), InspectorName("セットが進むごとの速度加算倍率"), Tooltip("0.1なら1、1.1、1.2…倍")]
        public float speedIncreasePerSet;
        [InspectorName("乱数シード（0で毎回ランダム）")] public int randomSeed;

        [Header("セット結果の演出")]
        [Min(0f), InspectorName("結果を表示する秒数")] public float resultDisplaySeconds = 1f;
        [InspectorName("結果アニメーションを有効化")] public bool animateResults = true;
        [Range(0f, 0.15f), InspectorName("結果アニメーションの拡縮量")] public float resultScaleAmount = 0.035f;
        [Min(0f), InspectorName("成功時の輝きの回数")] public float successFlashCount = 3f;

        [Header("セット間のフェード")]
        [InspectorName("フェードを有効化")] public bool useFade = true;
        [Min(0f), InspectorName("フェードアウト秒数")] public float fadeOutSeconds = 0.25f;
        [Min(0f), InspectorName("非表示で待つ秒数")] public float fadeHoldSeconds = 0.1f;
        [Min(0f), InspectorName("フェードイン秒数")] public float fadeInSeconds = 0.25f;

        public void Validate()
        {
            setCount = Mathf.Max(2, setCount);
            requiredSuccesses = Mathf.Clamp(requiredSuccesses, 1, setCount - 1);
            fillPerSecond = Mathf.Max(0.01f, fillPerSecond);
            successZoneStart = Mathf.Clamp01(successZoneStart);
            maximumGraceSeconds = Mathf.Max(0f, maximumGraceSeconds);
            minimumSpeedMultiplier = Mathf.Max(0.01f, minimumSpeedMultiplier);
            maximumSpeedMultiplier = Mathf.Max(minimumSpeedMultiplier, maximumSpeedMultiplier);
            speedIncreasePerSet = Mathf.Max(0f, speedIncreasePerSet);
            resultDisplaySeconds = Mathf.Max(0f, resultDisplaySeconds);
            resultScaleAmount = Mathf.Clamp(resultScaleAmount, 0f, 0.15f);
            successFlashCount = Mathf.Max(0f, successFlashCount);
            fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
            fadeHoldSeconds = Mathf.Max(0f, fadeHoldSeconds);
            fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
        }
    }
}
