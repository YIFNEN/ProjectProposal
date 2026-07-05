using UnityEngine;

namespace AO.Judgement
{
    /// <summary>
    /// 손 위치·속도 + 햅틱 출력 인터페이스. 구현체:
    ///   - HandTracker: Transform 기반 + XRNode 기반 햅틱
    ///   - 추후 HandTrackingSource (com.unity.xr.hands) — 손 트래킹은 햅틱 미지원 (no-op)
    ///
    /// FishNote / BubbleNote 가 상호작용 시 손 속도 측정 + 햅틱 반응에 사용.
    /// </summary>
    public interface IHandSource
    {
        /// <summary>월드 공간 손 위치.</summary>
        Vector3 Position { get; }

        /// <summary>현재 손 이동 속도 (m/s, 월드 공간).</summary>
        Vector3 Velocity { get; }

        /// <summary>현재 트래킹 활성 여부.</summary>
        bool IsTracked { get; }

        /// <summary>
        /// 컨트롤러 햅틱 진동 발생. 손 트래킹 구현체는 no-op.
        /// </summary>
        /// <param name="amplitude">진동 강도 0~1</param>
        /// <param name="duration">지속 시간 (초)</param>
        void PlayHaptic(float amplitude, float duration);
    }

}
