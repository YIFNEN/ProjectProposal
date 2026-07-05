using AO.Judgement;
using UnityEngine;

namespace AO.Player
{
    /// <summary>
    /// 양손 IHandSource 참조 매니저. 씬에 1개만 존재 (싱글톤 패턴).
    /// XR Origin 하위 LeftHand / RightHand GO 에 HandTracker가 붙어있어야 함.
    ///
    /// 셋업 (Unity 에디터):
    ///   XR Origin
    ///     └ Camera Offset
    ///         ├ Main Camera (HMD)
    ///         ├ LeftHand Controller (TrackedPoseDriver) ─┐
    ///         │   └ HandCollider                          │ <- 둘 중 하나에
    ///         │       (SphereCollider Trigger r=0.1125)   │    HandTracker 부착
    ///         │       (Rigidbody Kinematic)               │    "Hand" 태그
    ///         │       (HandTracker component)            ─┘
    ///         │       (Tag = "Hand")
    ///         └ RightHand Controller (동일 구조)
    ///
    ///   이 PlayerHands는 별도 GO에 두고 _leftHand/_rightHand에 위 HandTracker들을 드래그.
    /// </summary>
    public class PlayerHands : MonoBehaviour
    {
        public static PlayerHands Instance { get; private set; }

        [SerializeField, Tooltip("LeftHand의 HandTracker (또는 IHandSource 구현체)")]
        private HandTracker _leftHand;
        [SerializeField] private HandTracker _rightHand;

        public IHandSource LeftHand => _leftHand;
        public IHandSource RightHand => _rightHand;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerHands] Duplicate instance, destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_leftHand == null || _rightHand == null)
            {
                Debug.LogWarning("[PlayerHands] LeftHand or RightHand HandTracker not assigned.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
