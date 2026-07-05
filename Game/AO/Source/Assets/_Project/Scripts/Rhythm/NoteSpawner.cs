using AO.Core;
using AO.Notes;
using UnityEngine;

namespace AO.Rhythm
{
    /// <summary>
    /// RhythmEngine.OnNoteShouldSpawn 이벤트를 구독해 풀에서 노트를 꺼내 Initialize.
    ///
    /// 위치 결정:
    ///   - Spawn Anchor: 노트가 시작되는 위치(노트가 +Z에서 -Z로 흐른다고 가정 시 캐릭터 정면 멀리)
    ///   - Hit Anchor: 노트가 도착해 충돌해야 하는 위치 (D-Variant 컨셉상 캐릭터 손 위치)
    ///   - 레인 오프셋: JSON 호환을 위해 enum 이름은 Up/Down/Left/Right를 유지하되, 물리 배치는 대각선 X자 레인
    ///
    /// W1에서는 SpawnAnchor / HitAnchor를 씬에 Empty GameObject로 배치 후 인스펙터로 연결.
    /// W4의 D-Variant 작업에서 Hit Anchor를 캐릭터 양손 위치에 동적으로 매핑.
    /// </summary>
    public class NoteSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RhythmEngine _engine;
        [SerializeField] private JudgementConfig _judgementConfig;
        [SerializeField] private NotePool _pool;

        [Header("Anchors")]
        [Tooltip("노트가 스폰되는 기준 위치 (월드). 보통 캐릭터 정면 멀리 +Z 쪽")]
        [SerializeField] private Transform _spawnAnchor;
        [Tooltip("노트가 도착하는 기준 위치 (월드). D-Variant에서는 캐릭터 손/몸 위치")]
        [SerializeField] private Transform _hitAnchor;

        [Header("Lane Offsets (Hit Anchor 기준)")]
        [SerializeField] private Vector3 _laneUpOffset = LaneLayout.UpperLeftOffset;
        [SerializeField] private Vector3 _laneDownOffset = LaneLayout.LowerRightOffset;
        [SerializeField] private Vector3 _laneLeftOffset = LaneLayout.LowerLeftOffset;
        [SerializeField] private Vector3 _laneRightOffset = LaneLayout.UpperRightOffset;
        [SerializeField] private Vector3 _laneCenterOffset = LaneLayout.CenterOffset;

        [Header("Spawn Shape")]
        [Tooltip("When enabled, all notes start from a shared top-middle point and fan out to their lane hit positions.")]
        [SerializeField] private bool _useSharedFanSpawnPoint = true;
        [SerializeField] private Vector3 _sharedSpawnOffset = LaneLayout.TopMidSpawnOffset;

        private void OnEnable()
        {
            if (_engine != null)
            {
                _engine.OnNoteShouldSpawn += HandleNoteShouldSpawn;
            }
        }

        private void OnDisable()
        {
            if (_engine != null)
            {
                _engine.OnNoteShouldSpawn -= HandleNoteShouldSpawn;
            }
        }

        private void HandleNoteShouldSpawn(NoteData data)
        {
            if (_pool == null || _judgementConfig == null) return;

            NoteObject note = _pool.GetForData(data);
            if (note == null)
            {
                Debug.LogWarning($"[NoteSpawner] Pool returned null for {data.Type}.");
                return;
            }

            Vector3 hitPos = ComputeHitPosition(data);
            Vector3 spawnPos = ComputeSpawnPosition(data, hitPos);
            note.transform.SetParent(_pool.transform, worldPositionStays: false);
            note.Initialize(data, _engine, _judgementConfig, spawnPos, hitPos);
        }

        public Vector3 GetConfiguredLaneOffset(Lane lane)
        {
            return GetLaneOffset(lane);
        }

        public Vector3 SharedSpawnOffset => _sharedSpawnOffset;

        public bool UseSharedFanSpawnPoint => _useSharedFanSpawnPoint;

        public void ConfigureLaneOffsets(Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center)
        {
            _laneUpOffset = up;
            _laneDownOffset = down;
            _laneLeftOffset = left;
            _laneRightOffset = right;
            _laneCenterOffset = center;
        }

        public void ConfigureSharedSpawnOffset(Vector3 sharedSpawnOffset)
        {
            _sharedSpawnOffset = sharedSpawnOffset;
        }

        private Vector3 ComputeHitPosition(NoteData data)
        {
            Vector3 baseHit = _hitAnchor != null ? _hitAnchor.position : Vector3.zero;
            return baseHit + GetLaneOffset(data.Lane);
        }

        private Vector3 ComputeSpawnPosition(NoteData data, Vector3 hitPos)
        {
            // Spawn Anchor가 노트의 "시작점"을 정의. 레인 오프셋은 Hit Anchor 기준이므로
            // Spawn 도 동일한 오프셋을 적용해 직선 이동이 되도록.
            Vector3 baseSpawn = _spawnAnchor != null ? _spawnAnchor.position : Vector3.forward * 5f;
            if (_useSharedFanSpawnPoint)
            {
                return baseSpawn + _sharedSpawnOffset;
            }

            return baseSpawn + GetLaneOffset(data.Lane);
        }

        private Vector3 GetLaneOffset(Lane lane)
        {
            return lane switch
            {
                Lane.Up    => _laneUpOffset,
                Lane.Down  => _laneDownOffset,
                Lane.Left  => _laneLeftOffset,
                Lane.Right => _laneRightOffset,
                Lane.Center => _laneCenterOffset,
                _          => Vector3.zero,
            };
        }
    }
}
