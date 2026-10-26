using UnityEngine;
using UnityEngine.UI;

namespace SousakuTeam8.MeterGame.Timing
{
    public sealed class TimingMeterPresenter : MonoBehaviour
    {
        [SerializeField] private MeterGameBarView bar;
        [SerializeField] private MeterGameInputPrompt prompt;
        [SerializeField] private Text timerLabel;

        public void Render(TimingSessionState session, TimingMeterPhase phase)
        {
            if (session == null) return;
            bool complete = session.IsFinished && phase != TimingMeterPhase.RoundFeedback && phase != TimingMeterPhase.FadingOut;
            bool judging = phase == TimingMeterPhase.Playing || phase == TimingMeterPhase.FadingIn && !complete;
            string score = $"{session.CurrentSetNumber}/{session.SetCount} セット  成功 {session.SuccessCount}/{session.RequiredSuccesses}";
            string message = complete
                ? $"ゲーム{(session.IsSuccessful ? "成功！" : "失敗…")}  成功 {session.SuccessCount}/{session.SetCount}"
                : judging ? $"範囲内でSpace！  {score}"
                : $"{(session.Round.Result == TimingRoundResult.Success ? "成功！" : "失敗…")}  {score}";
            if (prompt != null) prompt.SetPresentation(true, message);
            if (timerLabel != null) timerLabel.text = $"経過 {session.Round.ElapsedSeconds:F1} 秒";
            if (bar != null)
            {
                MeterGameResult result = complete
                    ? (session.IsSuccessful ? MeterGameResult.Success : MeterGameResult.Failure)
                    : (!judging && session.Round.Result == TimingRoundResult.Failure ? MeterGameResult.Failure : MeterGameResult.Playing);
                bar.Show(session.Round.Gauge, (float)session.SuccessCount / session.RequiredSuccesses,
                    session.Round.SuccessZoneStart, session.Round.SuccessZoneEnd, result);
            }
        }
    }
}
