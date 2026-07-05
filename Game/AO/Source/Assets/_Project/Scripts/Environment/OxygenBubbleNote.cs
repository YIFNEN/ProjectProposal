using System.Collections;
using UnityEngine;

/// <summary>
/// OxygenBubbleNote  [어비스리움 에디션]
/// ─────────────────────────────────────────────────────────────────────
/// 바닷속 리듬게임의 "산소방울 노트" 런타임 제어 컴포넌트.
/// OxygenBubble.shader (어비스리움 에디션) 와 함께 사용합니다.
///
/// 주요 변경점 (어비스리움 에디션)
///   • 노트 타입별 프리셋 색상 전면 개편 (영롱한 보석 계열)
///   • 히트 플래시 → 시안 파란빛으로 변경
///   • 판정선 접근 시 Pulse가 더 크고 빠르게 반응
///   • BottomGlowColor, FresnelColor2 도 타입별로 변경 가능
/// ─────────────────────────────────────────────────────────────────────
/// [요구사항]
///   - Unity 6 (6000.x)
///   - Universal Render Pipeline (URP) 17+
///   - Compatibility Mode ON
///   - Meta Quest 2 / Quest 3 (Android, OpenXR)
/// </summary>
[RequireComponent(typeof(Renderer))]
public class OxygenBubbleNote : MonoBehaviour
{
    // ── 셰이더 프로퍼티 ID 캐싱 ──────────────────────────────────────
    static readonly int ID_HitFlashColor      = Shader.PropertyToID("_HitFlashColor");
    static readonly int ID_HitFlashIntensity  = Shader.PropertyToID("_HitFlashIntensity");
    static readonly int ID_FresnelColor       = Shader.PropertyToID("_FresnelColor");
    static readonly int ID_FresnelIntensity   = Shader.PropertyToID("_FresnelIntensity");
    static readonly int ID_FresnelColor2      = Shader.PropertyToID("_FresnelColor2");
    static readonly int ID_FresnelIntensity2  = Shader.PropertyToID("_FresnelIntensity2");
    static readonly int ID_PulseSpeed         = Shader.PropertyToID("_PulseSpeed");
    static readonly int ID_PulseAmplitude     = Shader.PropertyToID("_PulseAmplitude");
    static readonly int ID_BaseColor          = Shader.PropertyToID("_BaseColor");
    static readonly int ID_InnerGlowColor     = Shader.PropertyToID("_InnerGlowColor");
    static readonly int ID_BottomGlowColor    = Shader.PropertyToID("_BottomGlowColor");
    static readonly int ID_SpecularIntensity  = Shader.PropertyToID("_SpecularIntensity");
    static readonly int ID_SpecularIntensity2 = Shader.PropertyToID("_SpecularIntensity2");

    // ── 노트 타입 ─────────────────────────────────────────────────────
    public enum NoteType { Normal, Long, Special }

    [System.Serializable]
    public struct NoteColorPreset
    {
        [Header("테두리 빛 (Fresnel)")]
        public Color fresnelColor;          // 바깥 림 색
        public Color fresnelColor2;         // 안쪽 림 색 (보라~파랑 계열 권장)
        [Range(0f, 3f)] public float fresnelIntensity;
        [Range(0f, 2f)] public float fresnelIntensity2;

        [Header("베이스 & 내부")]
        public Color baseColor;             // 전체 색조 (Alpha = 투명도)
        public Color innerGlowColor;        // 안쪽 글로우 (Alpha 중요!)
        public Color bottomGlowColor;       // 하단 보라빛 반사 (Alpha 중요!)
    }

    // ─────────────────────────────────────────────────────────────────
    // 노트 타입별 프리셋 — 어비스리움 에디션
    // ─────────────────────────────────────────────────────────────────

    [Header("노트 타입 프리셋")]

    // Normal: 맑고 영롱한 하늘색 보석
    public NoteColorPreset normalPreset = new NoteColorPreset
    {
        fresnelColor      = new Color(0.55f, 0.95f, 1.00f, 1f),   // 밝은 시안
        fresnelColor2     = new Color(0.40f, 0.50f, 1.00f, 1f),   // 보라빛 안쪽 림
        fresnelIntensity  = 2.2f,
        fresnelIntensity2 = 0.6f,
        baseColor         = new Color(0.30f, 0.55f, 1.00f, 0.12f), // 짙은 바닷속 파랑
        innerGlowColor    = new Color(0.15f, 0.60f, 1.00f, 0.35f), // 안쪽 파란 글로우
        bottomGlowColor   = new Color(0.35f, 0.20f, 1.00f, 0.18f)  // 하단 보라 반사
    };

    // Long: 더 깊고 짙은 코발트 보석
    public NoteColorPreset longPreset = new NoteColorPreset
    {
        fresnelColor      = new Color(0.30f, 0.70f, 1.00f, 1f),   // 진한 코발트 블루
        fresnelColor2     = new Color(0.20f, 0.30f, 0.90f, 1f),   // 딥 블루 안쪽
        fresnelIntensity  = 2.5f,
        fresnelIntensity2 = 0.8f,
        baseColor         = new Color(0.10f, 0.30f, 0.80f, 0.16f), // 더 진한 바닷속
        innerGlowColor    = new Color(0.10f, 0.40f, 1.00f, 0.45f), // 강한 내부 글로우
        bottomGlowColor   = new Color(0.20f, 0.10f, 0.90f, 0.25f)  // 강한 보라 반사
    };

    // Special: 황금빛이 섞인 신비로운 보석 (아이템 노트 등)
    public NoteColorPreset specialPreset = new NoteColorPreset
    {
        fresnelColor      = new Color(0.60f, 0.98f, 1.00f, 1f),   // 밝은 민트~흰빛
        fresnelColor2     = new Color(0.80f, 0.60f, 1.00f, 1f),   // 라벤더 안쪽 림
        fresnelIntensity  = 3.0f,
        fresnelIntensity2 = 1.0f,
        baseColor         = new Color(0.50f, 0.60f, 1.00f, 0.14f), // 밝은 퍼플~블루
        innerGlowColor    = new Color(0.40f, 0.70f, 1.00f, 0.40f), // 밝은 내부 글로우
        bottomGlowColor   = new Color(0.60f, 0.30f, 1.00f, 0.22f)  // 라벤더 반사
    };

    // ─────────────────────────────────────────────────────────────────

    [Header("히트 플래시 — 어비스리움 시안 계열")]
    [SerializeField] Color hitFlashColor    = new Color(0.50f, 0.95f, 1.0f, 1f);
    [SerializeField, Range(0f, 5f)]   float hitFlashPeak     = 3.5f;
    [SerializeField, Range(0.05f, 0.5f)] float hitFlashDuration = 0.20f;

    [Header("접근 펄스 (판정선 거리 기반)")]
    [SerializeField] Transform judgementLine;
    [SerializeField] float     pulseStartDistance = 5f;
    [SerializeField, Range(0f, 4f)]   float maxPulseSpeed     = 4.0f;
    [SerializeField, Range(0f, 0.5f)] float maxPulseAmplitude = 0.18f;

    // ── 내부 변수 ─────────────────────────────────────────────────────
    Renderer              _renderer;
    MaterialPropertyBlock _block;
    Coroutine             _flashCoroutine;
    NoteType              _currentType = NoteType.Normal;

    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _block    = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        ApplyPreset(_currentType);
    }

    void Update()
    {
        UpdateApproachPulse();
    }

    // ── 공개 API ─────────────────────────────────────────────────────

    /// <summary>노트 타입 설정 → 색상 프리셋 즉시 적용</summary>
    public void SetNoteType(NoteType type)
    {
        _currentType = type;
        ApplyPreset(type);
    }

    /// <summary>히트 판정 시 호출 → 파란 플래시 재생</summary>
    public void PlayHitFlash()
    {
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    /// <summary>오브젝트 풀 반환 시 상태 초기화</summary>
    public void Reset()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(ID_HitFlashIntensity, 0f);
        _renderer.SetPropertyBlock(_block);
        ApplyPreset(_currentType);
    }

    // ── 내부 메서드 ──────────────────────────────────────────────────

    void ApplyPreset(NoteType type)
    {
        NoteColorPreset p = type switch
        {
            NoteType.Long    => longPreset,
            NoteType.Special => specialPreset,
            _                => normalPreset
        };

        _renderer.GetPropertyBlock(_block);

        // Fresnel 이중 레이어
        _block.SetColor(ID_FresnelColor,       p.fresnelColor);
        _block.SetFloat(ID_FresnelIntensity,   p.fresnelIntensity);
        _block.SetColor(ID_FresnelColor2,      p.fresnelColor2);
        _block.SetFloat(ID_FresnelIntensity2,  p.fresnelIntensity2);

        // 베이스 & 글로우
        _block.SetColor(ID_BaseColor,          p.baseColor);
        _block.SetColor(ID_InnerGlowColor,     p.innerGlowColor);
        _block.SetColor(ID_BottomGlowColor,    p.bottomGlowColor);

        _renderer.SetPropertyBlock(_block);
    }

    void UpdateApproachPulse()
    {
        if (judgementLine == null) return;

        float dist = Vector3.Distance(transform.position, judgementLine.position);
        float t    = 1f - Mathf.Clamp01(dist / pulseStartDistance);
        float tSq  = t * t;  // t^2 커브: 가까워질수록 급격하게 강해짐

        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(ID_PulseSpeed,     Mathf.Lerp(1.8f, maxPulseSpeed,     tSq));
        _block.SetFloat(ID_PulseAmplitude, Mathf.Lerp(0.10f, maxPulseAmplitude, tSq));
        _renderer.SetPropertyBlock(_block);
    }

    IEnumerator HitFlashRoutine()
    {
        float elapsed = 0f;
        while (elapsed < hitFlashDuration)
        {
            float t         = elapsed / hitFlashDuration;
            float intensity = Mathf.Sin(t * Mathf.PI) * hitFlashPeak;

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ID_HitFlashColor,     hitFlashColor);
            _block.SetFloat(ID_HitFlashIntensity, intensity);
            _renderer.SetPropertyBlock(_block);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(ID_HitFlashIntensity, 0f);
        _renderer.SetPropertyBlock(_block);
        _flashCoroutine = null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, pulseStartDistance);
    }
#endif
}
