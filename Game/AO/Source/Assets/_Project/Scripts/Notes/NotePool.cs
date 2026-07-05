using System.Collections.Generic;
using AO.Rhythm;
using UnityEngine;
using UnityEngine.Serialization;

namespace AO.Notes
{
    /// <summary>
    /// Pools rhythm note wrapper prefabs. Fish color/shape variants are visual
    /// candidates inside FishNote, so the pool only needs one FishNote wrapper.
    /// </summary>
    public class NotePool : MonoBehaviour
    {
        public static NotePool Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private BubbleNote _bubblePrefab;
        [FormerlySerializedAs("_fishPrefab")]
        [SerializeField] private FishNote _fishWrapperPrefab;

        [Header("Initial Pool Size")]
        [SerializeField] private int _bubblePrewarm = 16;
        [SerializeField] private int _fishPrewarm = 8;

        private readonly Queue<BubbleNote> _bubblePool = new Queue<BubbleNote>();
        private readonly Queue<FishNote> _fishPool = new Queue<FishNote>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Prewarm();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Prewarm()
        {
            for (int i = 0; i < _bubblePrewarm; i++)
            {
                if (_bubblePrefab == null) break;

                BubbleNote bubble = Instantiate(_bubblePrefab, transform);
                bubble.gameObject.SetActive(false);
                _bubblePool.Enqueue(bubble);
            }

            for (int i = 0; i < _fishPrewarm; i++)
            {
                if (_fishWrapperPrefab == null) break;

                FishNote fish = Instantiate(_fishWrapperPrefab, transform);
                fish.gameObject.SetActive(false);
                _fishPool.Enqueue(fish);
            }
        }

        public NoteObject GetForData(NoteData data)
        {
            if (data == null) return null;

            return data.Type switch
            {
                NoteType.Bubble => GetBubble(),
                NoteType.Fish => GetFish(),
                _ => null,
            };
        }

        public NoteObject GetForType(NoteType type)
        {
            return type switch
            {
                NoteType.Bubble => GetBubble(),
                NoteType.Fish => GetFish(),
                _ => null,
            };
        }

        public void Return(NoteObject note)
        {
            if (note == null) return;

            note.gameObject.SetActive(false);
            note.transform.SetParent(transform, worldPositionStays: false);

            switch (note)
            {
                case BubbleNote bubble:
                    _bubblePool.Enqueue(bubble);
                    break;
                case FishNote fish:
                    _fishPool.Enqueue(fish);
                    break;
            }
        }

        private BubbleNote GetBubble()
        {
            if (_bubblePool.Count > 0) return _bubblePool.Dequeue();
            return _bubblePrefab != null ? Instantiate(_bubblePrefab, transform) : null;
        }

        private FishNote GetFish()
        {
            if (_fishPool.Count > 0) return _fishPool.Dequeue();
            return _fishWrapperPrefab != null ? Instantiate(_fishWrapperPrefab, transform) : null;
        }
    }
}
