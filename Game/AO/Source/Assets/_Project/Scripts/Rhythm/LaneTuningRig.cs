using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace AO.Rhythm
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LaneTuningRig : MonoBehaviour
    {
        private const float PositionEpsilonSqr = 0.0000001f;

        [Header("Targets")]
        [SerializeField] private Transform _hitAnchor;
        [SerializeField] private Transform _spawnAnchor;
        [SerializeField] private NoteSpawner _noteSpawner;
        [SerializeField] private JudgementFrame _judgementFrame;
        [SerializeField] private LanePathGuide _lanePathGuide;
        [SerializeField] private Transform _debugHitPadsRoot;

        [Header("Handles")]
        [SerializeField] private Transform _laneUp;
        [SerializeField] private Transform _laneDown;
        [SerializeField] private Transform _laneLeft;
        [SerializeField] private Transform _laneRight;
        [SerializeField] private Transform _laneCenter;
        [SerializeField] private Transform _sharedSpawnPoint;

        [Header("Edit Mode")]
        [SerializeField] private bool _autoApplyInEditMode = true;
        [SerializeField] private bool _syncDebugHitPads = true;

        public void ConfigureTargets(
            Transform hitAnchor,
            Transform spawnAnchor,
            NoteSpawner noteSpawner,
            JudgementFrame judgementFrame,
            LanePathGuide lanePathGuide,
            Transform debugHitPadsRoot,
            Transform laneUp,
            Transform laneDown,
            Transform laneLeft,
            Transform laneRight,
            Transform laneCenter,
            Transform sharedSpawnPoint)
        {
            _hitAnchor = hitAnchor;
            _spawnAnchor = spawnAnchor;
            _noteSpawner = noteSpawner;
            _judgementFrame = judgementFrame;
            _lanePathGuide = lanePathGuide;
            _debugHitPadsRoot = debugHitPadsRoot;
            _laneUp = laneUp;
            _laneDown = laneDown;
            _laneLeft = laneLeft;
            _laneRight = laneRight;
            _laneCenter = laneCenter;
            _sharedSpawnPoint = sharedSpawnPoint;
        }

        public void PullTargetsToHandles()
        {
            Vector3 hitBase = HitBasePosition;
            SetHandleWorldPosition(_laneUp, hitBase + GetSourceLaneOffset(Lane.Up));
            SetHandleWorldPosition(_laneDown, hitBase + GetSourceLaneOffset(Lane.Down));
            SetHandleWorldPosition(_laneLeft, hitBase + GetSourceLaneOffset(Lane.Left));
            SetHandleWorldPosition(_laneRight, hitBase + GetSourceLaneOffset(Lane.Right));
            SetHandleWorldPosition(_laneCenter, hitBase + GetSourceLaneOffset(Lane.Center));

            if (_sharedSpawnPoint != null)
            {
                SetHandleWorldPosition(_sharedSpawnPoint, SpawnBasePosition + GetSourceSharedSpawnOffset());
            }
        }

        public void ApplyHandlesToTargets()
        {
            if (!HasLaneHandles) return;

            Vector3 up = GetHandleLaneOffset(_laneUp, Lane.Up);
            Vector3 down = GetHandleLaneOffset(_laneDown, Lane.Down);
            Vector3 left = GetHandleLaneOffset(_laneLeft, Lane.Left);
            Vector3 right = GetHandleLaneOffset(_laneRight, Lane.Right);
            Vector3 center = GetHandleLaneOffset(_laneCenter, Lane.Center);
            Vector3 sharedSpawn = _sharedSpawnPoint != null
                ? _sharedSpawnPoint.position - SpawnBasePosition
                : GetSourceSharedSpawnOffset();

            bool changed = false;
            changed |= ApplyToNoteSpawner(up, down, left, right, center, sharedSpawn);
            changed |= ApplyToJudgementFrame(up, down, left, right, center);
            changed |= ApplyToLanePathGuide(up, down, left, right, center, sharedSpawn);
            changed |= ApplyToDebugHitPads(up, down, left, right, center);

#if UNITY_EDITOR
            if (changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            if (!Application.isPlaying && _autoApplyInEditMode)
            {
                ApplyHandlesToTargets();
            }
        }

        private void Update()
        {
            if (Application.isPlaying || !_autoApplyInEditMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            ApplyHandlesToTargets();
        }
#endif

        private bool ApplyToNoteSpawner(Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center, Vector3 sharedSpawn)
        {
            if (_noteSpawner == null) return false;
            if (LaneOffsetsMatch(_noteSpawner, up, down, left, right, center)
                && Approximately(_noteSpawner.SharedSpawnOffset, sharedSpawn))
            {
                return false;
            }

            _noteSpawner.ConfigureLaneOffsets(up, down, left, right, center);
            _noteSpawner.ConfigureSharedSpawnOffset(sharedSpawn);
            MarkDirty(_noteSpawner);
            return true;
        }

        private bool ApplyToJudgementFrame(Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center)
        {
            if (_judgementFrame == null) return false;
            if (LaneOffsetsMatch(_judgementFrame, up, down, left, right, center)) return false;

            _judgementFrame.ConfigureLaneOffsets(up, down, left, right, center);
            MarkDirty(_judgementFrame);
            return true;
        }

        private bool ApplyToLanePathGuide(Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center, Vector3 sharedSpawn)
        {
            if (_lanePathGuide == null) return false;
            if (LaneOffsetsMatch(_lanePathGuide, up, down, left, right, center)
                && Approximately(_lanePathGuide.SharedSpawnOffset, sharedSpawn))
            {
                return false;
            }

            _lanePathGuide.ConfigureLaneOffsets(up, down, left, right, center);
            _lanePathGuide.ConfigureSharedSpawnOffset(sharedSpawn);
            MarkDirty(_lanePathGuide);
            return true;
        }

        private bool ApplyToDebugHitPads(Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center)
        {
            if (!_syncDebugHitPads || _debugHitPadsRoot == null) return false;

            bool changed = false;
            changed |= SetDebugHitPadPosition("DebugHitPad_Up", up);
            changed |= SetDebugHitPadPosition("DebugHitPad_Down", down);
            changed |= SetDebugHitPadPosition("DebugHitPad_Left", left);
            changed |= SetDebugHitPadPosition("DebugHitPad_Right", right);
            changed |= SetDebugHitPadPosition("DebugHitPad_Center", center);
            return changed;
        }

        private bool SetDebugHitPadPosition(string childName, Vector3 offset)
        {
            Transform pad = _debugHitPadsRoot.Find(childName);
            if (pad == null) return false;

            Vector3 position = HitBasePosition + offset;
            if (Approximately(pad.position, position)) return false;

            pad.position = position;
            MarkDirty(pad);
            return true;
        }

        private Vector3 GetHandleLaneOffset(Transform handle, Lane lane)
        {
            return handle != null ? handle.position - HitBasePosition : GetSourceLaneOffset(lane);
        }

        private Vector3 GetSourceLaneOffset(Lane lane)
        {
            if (_noteSpawner != null) return _noteSpawner.GetConfiguredLaneOffset(lane);
            if (_judgementFrame != null) return _judgementFrame.GetConfiguredLaneOffset(lane);
            if (_lanePathGuide != null) return _lanePathGuide.GetConfiguredLaneOffset(lane);
            return LaneLayout.GetDefaultOffset(lane);
        }

        private Vector3 GetSourceSharedSpawnOffset()
        {
            if (_noteSpawner != null) return _noteSpawner.SharedSpawnOffset;
            if (_lanePathGuide != null) return _lanePathGuide.SharedSpawnOffset;
            return LaneLayout.TopMidSpawnOffset;
        }

        private Vector3 HitBasePosition => _hitAnchor != null ? _hitAnchor.position : transform.position;

        private Vector3 SpawnBasePosition => _spawnAnchor != null ? _spawnAnchor.position : transform.position;

        private bool HasLaneHandles =>
            _laneUp != null
            && _laneDown != null
            && _laneLeft != null
            && _laneRight != null
            && _laneCenter != null;

        private static void SetHandleWorldPosition(Transform handle, Vector3 position)
        {
            if (handle == null) return;
            handle.position = position;
            handle.localRotation = Quaternion.identity;
            handle.localScale = Vector3.one;
            MarkDirty(handle);
        }

        private static bool LaneOffsetsMatch(NoteSpawner spawner, Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center)
        {
            return Approximately(spawner.GetConfiguredLaneOffset(Lane.Up), up)
                && Approximately(spawner.GetConfiguredLaneOffset(Lane.Down), down)
                && Approximately(spawner.GetConfiguredLaneOffset(Lane.Left), left)
                && Approximately(spawner.GetConfiguredLaneOffset(Lane.Right), right)
                && Approximately(spawner.GetConfiguredLaneOffset(Lane.Center), center);
        }

        private static bool LaneOffsetsMatch(JudgementFrame frame, Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center)
        {
            return Approximately(frame.GetConfiguredLaneOffset(Lane.Up), up)
                && Approximately(frame.GetConfiguredLaneOffset(Lane.Down), down)
                && Approximately(frame.GetConfiguredLaneOffset(Lane.Left), left)
                && Approximately(frame.GetConfiguredLaneOffset(Lane.Right), right)
                && Approximately(frame.GetConfiguredLaneOffset(Lane.Center), center);
        }

        private static bool LaneOffsetsMatch(LanePathGuide guide, Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center)
        {
            return Approximately(guide.GetConfiguredLaneOffset(Lane.Up), up)
                && Approximately(guide.GetConfiguredLaneOffset(Lane.Down), down)
                && Approximately(guide.GetConfiguredLaneOffset(Lane.Left), left)
                && Approximately(guide.GetConfiguredLaneOffset(Lane.Right), right)
                && Approximately(guide.GetConfiguredLaneOffset(Lane.Center), center);
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= PositionEpsilonSqr;
        }

        private static void MarkDirty(Object target)
        {
#if UNITY_EDITOR
            if (target != null && !Application.isPlaying)
            {
                EditorUtility.SetDirty(target);
            }
#endif
        }
    }
}
