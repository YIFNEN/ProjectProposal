using AO.Core;
using AO.Rhythm;
using UnityEngine;

namespace AO.Notes
{
    /// <summary>
    /// 모든 노트의 공통 베이스. 시간 기반 위치 보간(프레임 드랍에 안전)과 수명 관리.
    ///
    /// 위치 보간:
    ///   t = (SongTime - SpawnTime) / (HitTime - SpawnTime)
    ///   position = Lerp(SpawnPos, HitPos, t)
    /// t가 1을 넘으면 hitPos를 지나치므로 BubbleNote는 MISS 처리, FishNote는 자체 로직.
    /// </summary>
    public abstract class NoteObject : MonoBehaviour
    {
        protected RhythmEngine _engine;
        protected JudgementConfig _judgementConfig;
        protected NoteData _data;
        protected Vector3 _spawnPos;
        protected Vector3 _hitPos;
        protected double _spawnTime;
        protected double _hitTime;
        protected bool _resolved; // 이미 판정/소멸 처리되었는가

        public NoteData Data => _data;
        public Lane Lane => _data?.Lane ?? Lane.None;
        public NoteType Type => _data?.Type ?? NoteType.Bubble;

        public virtual void Initialize(NoteData data, RhythmEngine engine, JudgementConfig cfg,
            Vector3 spawnPos, Vector3 hitPos)
        {
            _data = data;
            _engine = engine;
            _judgementConfig = cfg;
            _spawnPos = spawnPos;
            _hitPos = hitPos;
            _hitTime = data.HitTime;
            float leadTime = engine != null ? engine.EffectiveNoteLeadTimeSeconds : cfg.NoteLeadTimeSeconds;
            _spawnTime = data.HitTime - leadTime;
            _resolved = false;
            SetPathPosition(spawnPos);
            gameObject.SetActive(true);
            OnSpawned();
        }

        protected virtual void OnSpawned() { }

        protected virtual void Update()
        {
            if (_engine == null || _resolved || _engine.IsPaused) return;

            double now = _engine.SongTime;
            double duration = _hitTime - _spawnTime;
            if (duration <= 0) return;

            float t = (float)((now - _spawnTime) / duration);
            SetPathPosition(Vector3.LerpUnclamped(_spawnPos, _hitPos, t));

            UpdateNote(now, t);
        }

        protected virtual void SetPathPosition(Vector3 pathPosition)
        {
            transform.position = pathPosition;
        }

        /// <summary>
        /// BubbleNote / FishNote 별 추가 로직 (판정·수명·이펙트 트리거).
        /// </summary>
        protected abstract void UpdateNote(double songTime, float t);

        /// <summary>
        /// 풀로 반환. NotePool이 호출하지만 자식 클래스가 직접 호출해도 무방.
        /// </summary>
        public void Despawn()
        {
            _resolved = true;
            gameObject.SetActive(false);
            NotePool.Instance?.Return(this);
        }
    }
}
