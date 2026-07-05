using UnityEngine;

namespace AO.Core
{
    [CreateAssetMenu(fileName = "OxygenConfig", menuName = "AO/Configs/Oxygen Config", order = 0)]
    public class OxygenConfig : ScriptableObject
    {
        [Header("Initial / Bounds")]
        [Range(0f, 100f)] public float InitialOxygen = 100f;
        [Range(0f, 100f)] public float MaxOxygen = 100f;
        [Range(0f, 100f)] public float CriticalThreshold = 20f;

        [Header("Drain (per second)")]
        [Tooltip("자연 감소율 — 일반 모드에서 1초당 차감")]
        [Range(0f, 10f)] public float NaturalDrainPerSecond = 2.5f;

        [Header("Penalties / Recoveries")]
        [Range(0f, 20f)] public float MissPenalty = 5f;
        [Range(0f, 20f)] public float PerfectRecovery = 4f;
        [Range(0f, 20f)] public float GoodRecovery = 2f;
        [Range(0f, 20f)] public float FishStrokeRecovery = 3f;

        [Header("Behavior Flags")]
        [Tooltip("피버 모드 동안 자연 감소 정지")]
        public bool PauseDuringFever = true;
    }
}
