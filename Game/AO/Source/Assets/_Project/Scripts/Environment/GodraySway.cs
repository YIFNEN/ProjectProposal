using UnityEngine;
public class GodRaySway : MonoBehaviour
{
    [Header("회전 흔들림")]
    [Tooltip("좌우로 흔들리는 최대 각도 (도 단위)")]
    [SerializeField] private float swayAmount = 1.5f;
    [Tooltip("흔들림 속도 (낮을수록 천천히)")]
    [SerializeField] private float swaySpeed = 0.4f;
    [Header("위치 흔들림")]
    [Tooltip("좌우 위치 흔들림 (0이면 끄기)")]
    [SerializeField] private float positionSway = 0.05f;
    [Header("밝기 깜빡임")]
    [Tooltip("밝기가 미세하게 변동")]
    [SerializeField] private bool flickerEnabled = true;
    [Tooltip("깜빡임 속도")]
    [SerializeField] private float flickerSpeed = 0.6f;
    [Tooltip("깜빡임 강도 (0~0.3 추천)")]
    [SerializeField] private float flickerAmount = 0.15f;
    // 내부 변수
    private Quaternion baseRotation;
    private Vector3 basePosition;
    private float timeOffset;
    private Material matInstance;
    private float baseAlpha;
    void Start()
    {
        // 시작 시점의 위치/회전을 저장 (이걸 기준으로 흔들림)
        baseRotation = transform.localRotation;
        basePosition = transform.localPosition;
        // 각 빛줄기마다 다른 시작점 (모두 똑같이 흔들리지 않도록)
        timeOffset = Random.Range(0f, 100f);
        // 깜빡임용 머티리얼 인스턴스 생성
        if (flickerEnabled)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                // .material 호출 시 자동으로 인스턴스화 (각 빛줄기 독립)
                matInstance = rend.material;
                // 셰이더에 _AlphaMultiplier 속성이 있으면 기본값 저장
                if (matInstance.HasProperty("_AlphaMultiplier"))
                {
                    baseAlpha = matInstance.GetFloat("_AlphaMultiplier");
                }
            }
        }
    }
    void Update()
    {
        // 시간 + 오프셋 (각자 다른 위상)
        float t = Time.time + timeOffset;
        // === 회전 흔들림 (Z축 기준) ===
        float angle = Mathf.Sin(t * swaySpeed) * swayAmount;
        transform.localRotation = baseRotation * Quaternion.Euler(0, 0, angle);
        // === 위치 흔들림 (좌우) ===
        if (positionSway > 0f)
        {
            // 회전과 약간 다른 속도로 (더 자연스럽게)
            float xOffset = Mathf.Sin(t * swaySpeed * 0.7f) * positionSway;
            transform.localPosition = basePosition + new Vector3(xOffset, 0, 0);
        }
        // === 밝기 깜빡임 ===
        if (flickerEnabled && matInstance != null)
        {
            float flicker = baseAlpha + Mathf.Sin(t * flickerSpeed) * flickerAmount;
            // 0 미만으로 가지 않게 보호
            flicker = Mathf.Max(0f, flicker);
            matInstance.SetFloat("_AlphaMultiplier", flicker);
        }
    }
    void OnDestroy()
    {
        // 메모리 누수 방지: 인스턴스화한 머티리얼 정리
        if (matInstance != null)
        {
            Destroy(matInstance);
        }
    }
}