using UnityEngine;

namespace AO.Core
{
    [CreateAssetMenu(fileName = "JudgementConfig", menuName = "AO/Configs/Judgement Config", order = 2)]
    public class JudgementConfig : ScriptableObject
    {
        [Header("Bubble Note Timing Windows (초)")]
        [Tooltip("PERFECT 판정 윈도우 (도착 시점 ± 이 값)")]
        [Range(0.01f, 0.2f)] public float PerfectWindow = 0.05f;
        [Tooltip("GOOD 판정 윈도우 (도착 시점 ± 이 값). PerfectWindow 바깥부터 GoodWindow까지가 GOOD")]
        [Range(0.05f, 0.4f)] public float GoodWindow = 0.10f;

        [Header("Note Visibility")]
        [Tooltip("스폰 → 캐릭터 손 도착까지 시간 (초)")]
        [Range(0.5f, 3.0f)] public float NoteLeadTimeSeconds = 1.5f;

        [Header("Sync Calibration")]
        [Tooltip("영상-오디오 지연 차이 보정 (초). 환경마다 다르므로 0에서 시작해 ±0.02씩 튜닝.\n" +
                 "  양수(+0.05 등): 노트가 음악보다 일찍 도착할 때. 노트 도착을 미룸. (PC 스피커·블루투스 헤드폰 등 오디오가 늦은 환경)\n" +
                 "  음수(-0.05 등): 노트가 음악보다 늦게 도착할 때. 노트를 일찍 도착시킴. (Quest Link 등 영상 스트림이 늦은 환경)\n" +
                 "추후 게임 내 캘리브레이션 메뉴 (W4)에서 사용자가 직접 조정.")]
        [Range(-0.3f, 0.3f)] public float AudioOffsetSeconds = 0f;

        [Header("Fish Note (쓰다듬)")]
        [Tooltip("쓰다듬 성공으로 인정되는 최소 저속 이동 시간 (초)")]
        [Range(0.3f, 2.0f)] public float FishStrokeMinDuration = 0.8f;
        [Tooltip("쓰다듬으로 인정되는 손 속도 상한 (m/s) — 이보다 빠르면 타격으로 간주, 실패")]
        [Range(0.05f, 1.5f)] public float FishStrokeMaxSpeed = 1.5f;

        [Header("Score Base (배율 적용 전)")]
        public int PerfectBaseScore = 100;
        public int GoodBaseScore = 50;
        public int FishStrokeBaseScore = 200;
        [Tooltip("물고기 성공 시 추가 곱연산")]
        public float FishStrokeBonusMultiplier = 1.5f;

        [Header("Haptic Feedback (Quest Touch)")]
        [Tooltip("Bubble PERFECT 시 햅틱 강도 0~1")]
        [Range(0f, 1f)] public float HapticPerfectAmplitude = 0.7f;
        [Tooltip("Bubble PERFECT 햅틱 지속 시간 (초)")]
        [Range(0f, 0.5f)] public float HapticPerfectDuration = 0.06f;
        [Tooltip("Bubble GOOD 시 햅틱 강도")]
        [Range(0f, 1f)] public float HapticGoodAmplitude = 0.45f;
        [Tooltip("Bubble GOOD 햅틱 지속 시간 (초)")]
        [Range(0f, 0.5f)] public float HapticGoodDuration = 0.04f;
        [Tooltip("Fish 쓰다듬 성공 햅틱 강도 — 부드럽고 길게")]
        [Range(0f, 1f)] public float HapticFishSuccessAmplitude = 0.55f;
        [Tooltip("Fish 쓰다듬 성공 햅틱 지속 시간 (초)")]
        [Range(0f, 1f)] public float HapticFishSuccessDuration = 0.35f;
        [Tooltip("Fish 쓰다듬 실패 (빠른 이동) 햅틱 강도 — 짧고 강하게 경고")]
        [Range(0f, 1f)] public float HapticFishFailAmplitude = 0.85f;
        [Tooltip("Fish 쓰다듬 실패 햅틱 지속 시간 (초)")]
        [Range(0f, 0.5f)] public float HapticFishFailDuration = 0.08f;
        [Tooltip("Fish 쓰다듬 진행 중 (저속 누적 시) 미세 햅틱 — 0이면 비활성")]
        [Range(0f, 1f)] public float HapticFishStrokingAmplitude = 0.15f;
    }
}
