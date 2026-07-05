using UnityEngine;

namespace AO.Core
{
    [CreateAssetMenu(fileName = "FeverConfig", menuName = "AO/Configs/Fever Config", order = 3)]
    public class FeverConfig : ScriptableObject
    {
        [Header("Gauge")]
        [Tooltip("피버 게이지 최대값 (이 값에 도달하면 발동)")]
        [Range(50f, 200f)] public float GaugeMax = 100f;

        [Header("Gauge Increment Conditions")]
        [Tooltip("이 콤보 이상일 때만 노트 적중 시 게이지 증가")]
        public int ComboThreshold = 50;
        [Tooltip("콤보 임계 이상 + GOOD 이상 판정 1개당 게이지 증가량")]
        [Range(0f, 20f)] public float NoteIncrement = 2f;
        [Tooltip("물고기 쓰다듬 성공 1회당 게이지 증가량")]
        [Range(0f, 50f)] public float FishStrokeIncrement = 25f;
        [Tooltip("Tiny gauge gain per second while the combo threshold is being maintained. Set to 0 to disable.")]
        [Range(0f, 2f)] public float ComboHoldIncrementPerSecond = 0.12f;

        [Header("Gauge Decrement")]
        [Tooltip("MISS 시 게이지 감소량")]
        [Range(0f, 50f)] public float MissDecrement = 10f;

        [Header("Behavior")]
        [Tooltip("콤보가 임계 미만일 때 게이지를 동결할지 (true) 아니면 그래도 증가/감소할지")]
        public bool FreezeBelowThreshold = true;

        [Header("Activation")]
        [Tooltip("피버 지속 시간 (초)")]
        [Range(3f, 15f)] public float DurationSeconds = 7f;
        [Tooltip("피버 중 점수 추가 곱연산")]
        public float ScoreMultiplier = 1.2f;
        [Tooltip("피버 중 노트가 자동 PERFECT로 판정되는지")]
        public bool AutoPerfectDuringFever = true;
        [Tooltip("피버 중 산소 자연 감소를 멈출지")]
        public bool PauseOxygenDuringFever = true;

        [Header("Audio Crossfade")]
        [Tooltip("피버 진입·종료 오디오 크로스페이드 시간 (초)")]
        [Range(0.1f, 2f)] public float CrossfadeSeconds = 0.5f;
    }
}
