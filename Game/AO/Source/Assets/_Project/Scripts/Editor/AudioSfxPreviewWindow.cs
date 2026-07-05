using System;
using System.IO;
using System.Media;
using AO.Audio;
using UnityEditor;
using UnityEngine;

namespace AO.Editor
{
    public sealed class AudioSfxPreviewWindow : EditorWindow
    {
        private AudioManager _audioManager;
        private SerializedObject _serializedAudio;
        private Vector2 _scroll;
        private SoundPlayer _soundPlayer;
        private string _temporaryWavePath;
        private double _stopPreviewAt = -1d;
        private float _previewMasterVolume = 1f;

        [MenuItem("AO/Audio/SFX Preview")]
        public static void Open()
        {
            GetWindow<AudioSfxPreviewWindow>("AO SFX Preview");
        }

        private void OnEnable()
        {
            FindAudioManager();
            EditorApplication.update += UpdatePreviewStop;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreviewStop;
            StopPreview();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Open GamePlayScene, adjust AudioManager SFX entries, then preview the trimmed WAV segment with its entry volume. Runtime user SFX volume is not applied here.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _audioManager = (AudioManager)EditorGUILayout.ObjectField("Audio Manager", _audioManager, typeof(AudioManager), true);
                if (EditorGUI.EndChangeCheck())
                {
                    BindSerializedObject();
                }

                if (GUILayout.Button("Find", GUILayout.Width(64f)))
                {
                    FindAudioManager();
                }
            }

            _previewMasterVolume = EditorGUILayout.Slider("Preview Master", _previewMasterVolume, 0f, 1f);

            if (_audioManager == null)
            {
                EditorGUILayout.HelpBox("No AudioManager was found in the open scene.", MessageType.Warning);
                return;
            }

            if (_serializedAudio == null || _serializedAudio.targetObject != _audioManager)
            {
                BindSerializedObject();
            }

            _serializedAudio.Update();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Runtime Spacing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serializedAudio.FindProperty("_sfxMinInterval"));
            EditorGUILayout.PropertyField(_serializedAudio.FindProperty("_hitSfxMinInterval"));

            SerializedProperty entries = _serializedAudio.FindProperty("_sfxEntries");
            if (entries == null)
            {
                EditorGUILayout.HelpBox("AudioManager does not expose SFX entries.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("SFX Entries", EditorStyles.boldLabel);
                if (GUILayout.Button("Stop", GUILayout.Width(64f)))
                {
                    StopPreview();
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < entries.arraySize; i++)
            {
                DrawEntry(entries.GetArrayElementAtIndex(i), i);
            }
            EditorGUILayout.EndScrollView();

            _serializedAudio.ApplyModifiedProperties();
        }

        private void DrawEntry(SerializedProperty entry, int index)
        {
            SerializedProperty id = entry.FindPropertyRelative("Id");
            SerializedProperty clip = entry.FindPropertyRelative("Clip");
            SerializedProperty volume = entry.FindPropertyRelative("Volume");
            SerializedProperty startTime = entry.FindPropertyRelative("StartTime");
            SerializedProperty maxDuration = entry.FindPropertyRelative("MaxDuration");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Entry {index}", EditorStyles.boldLabel, GUILayout.Width(72f));
                    EditorGUILayout.PropertyField(id, GUIContent.none);

                    SfxId sfxId = (SfxId)id.enumValueIndex;
                    string runtimeMode = IsRuntimeTrimmed(sfxId) ? "Runtime trims this SFX" : "Runtime plays full clip";
                    EditorGUILayout.LabelField(runtimeMode, EditorStyles.miniLabel, GUILayout.Width(140f));
                }

                EditorGUILayout.PropertyField(clip);
                EditorGUILayout.PropertyField(volume);
                EditorGUILayout.PropertyField(startTime);
                EditorGUILayout.PropertyField(maxDuration);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
                    {
                        if (GUILayout.Button("Preview Trim"))
                        {
                            PreviewEntry(clip.objectReferenceValue as AudioClip, startTime.floatValue, maxDuration.floatValue, volume.floatValue, useTrim: true);
                        }

                        if (GUILayout.Button("Preview Full"))
                        {
                            PreviewEntry(clip.objectReferenceValue as AudioClip, 0f, 0f, volume.floatValue, useTrim: false);
                        }
                    }
                }
            }
        }

        private void FindAudioManager()
        {
            _audioManager = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            BindSerializedObject();
        }

        private void BindSerializedObject()
        {
            _serializedAudio = _audioManager != null ? new SerializedObject(_audioManager) : null;
        }

        private void PreviewEntry(AudioClip source, float startTime, float maxDuration, float entryVolume, bool useTrim)
        {
            if (source == null) return;

            float start = useTrim ? startTime : 0f;
            float availableDuration = Mathf.Max(0.01f, source.length - Mathf.Clamp(start, 0f, Mathf.Max(0f, source.length - 0.01f)));
            float duration = useTrim && maxDuration > 0f ? Mathf.Min(maxDuration, availableDuration) : availableDuration;
            float volume = Mathf.Clamp01(entryVolume <= 0f ? 1f : entryVolume) * _previewMasterVolume;

            PlayClip(source, start, duration, volume);
        }

        private void PlayClip(AudioClip source, float startTime, float duration, float volume)
        {
            StopPreview();

            float clampedStart = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, source.length - 0.01f));
            float clampedDuration = Mathf.Clamp(duration, 0.01f, source.length - clampedStart);
            string sourcePath = AssetDatabase.GetAssetPath(source);

            if (!TryWriteTrimmedWave(sourcePath, clampedStart, clampedDuration, volume, out string previewPath))
            {
                Debug.LogWarning($"[AO SFX Preview] Could not preview '{source.name}'. This preview tool currently supports PCM 16-bit WAV assets.");
                return;
            }

            _temporaryWavePath = previewPath;
            _soundPlayer = new SoundPlayer(_temporaryWavePath);
            _soundPlayer.Play();
            _stopPreviewAt = EditorApplication.timeSinceStartup + clampedDuration;
        }

        private static bool TryWriteTrimmedWave(string sourcePath, float startTime, float duration, float volume, out string previewPath)
        {
            previewPath = null;
            if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return false;
            if (!File.Exists(sourcePath)) return false;

            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            if (!TryReadPcm16Wave(sourceBytes, out WaveInfo info)) return false;

            int bytesPerFrame = info.Channels * (info.BitsPerSample / 8);
            int totalFrames = info.DataSize / bytesPerFrame;
            int startFrame = Mathf.Clamp(Mathf.RoundToInt(startTime * info.SampleRate), 0, Mathf.Max(0, totalFrames - 1));
            int frameCount = Mathf.Clamp(Mathf.RoundToInt(duration * info.SampleRate), 1, Mathf.Max(1, totalFrames - startFrame));
            int outputDataSize = frameCount * bytesPerFrame;
            byte[] outputData = new byte[outputDataSize];
            Buffer.BlockCopy(sourceBytes, info.DataOffset + startFrame * bytesPerFrame, outputData, 0, outputDataSize);

            float clampedVolume = Mathf.Clamp01(volume);
            if (!Mathf.Approximately(clampedVolume, 1f))
            {
                for (int i = 0; i < outputData.Length; i += 2)
                {
                    short sample = BitConverter.ToInt16(outputData, i);
                    int scaled = Mathf.RoundToInt(sample * clampedVolume);
                    scaled = Mathf.Clamp(scaled, short.MinValue, short.MaxValue);
                    byte[] scaledBytes = BitConverter.GetBytes((short)scaled);
                    outputData[i] = scaledBytes[0];
                    outputData[i + 1] = scaledBytes[1];
                }
            }

            Directory.CreateDirectory("Temp");
            previewPath = Path.GetFullPath(Path.Combine("Temp", $"AO_SFXPreview_{Guid.NewGuid():N}.wav"));
            WritePcm16Wave(previewPath, outputData, info.Channels, info.SampleRate, info.BitsPerSample);
            return true;
        }

        private static bool TryReadPcm16Wave(byte[] bytes, out WaveInfo info)
        {
            info = default;
            if (bytes.Length < 44) return false;
            if (ReadFourCc(bytes, 0) != "RIFF" || ReadFourCc(bytes, 8) != "WAVE") return false;

            int offset = 12;
            bool hasFormat = false;
            bool hasData = false;

            while (offset + 8 <= bytes.Length)
            {
                string chunkId = ReadFourCc(bytes, offset);
                int chunkSize = BitConverter.ToInt32(bytes, offset + 4);
                int chunkDataOffset = offset + 8;
                if (chunkSize < 0 || chunkDataOffset + chunkSize > bytes.Length) return false;

                if (chunkId == "fmt ")
                {
                    if (chunkSize < 16) return false;
                    ushort format = BitConverter.ToUInt16(bytes, chunkDataOffset);
                    ushort channels = BitConverter.ToUInt16(bytes, chunkDataOffset + 2);
                    int sampleRate = BitConverter.ToInt32(bytes, chunkDataOffset + 4);
                    ushort bitsPerSample = BitConverter.ToUInt16(bytes, chunkDataOffset + 14);
                    if (format != 1 || channels == 0 || sampleRate <= 0 || bitsPerSample != 16) return false;

                    info.Channels = channels;
                    info.SampleRate = sampleRate;
                    info.BitsPerSample = bitsPerSample;
                    hasFormat = true;
                }
                else if (chunkId == "data")
                {
                    info.DataOffset = chunkDataOffset;
                    info.DataSize = chunkSize;
                    hasData = true;
                }

                offset = chunkDataOffset + chunkSize;
                if ((offset & 1) == 1) offset++;
            }

            return hasFormat && hasData && info.DataSize > 0;
        }

        private static void WritePcm16Wave(string path, byte[] data, int channels, int sampleRate, int bitsPerSample)
        {
            int blockAlign = channels * (bitsPerSample / 8);
            int byteRate = sampleRate * blockAlign;

            using FileStream file = File.Create(path);
            using BinaryWriter writer = new BinaryWriter(file);
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + data.Length);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((ushort)blockAlign);
            writer.Write((ushort)bitsPerSample);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(data.Length);
            writer.Write(data);
        }

        private static string ReadFourCc(byte[] bytes, int offset)
        {
            return new string(new[] { (char)bytes[offset], (char)bytes[offset + 1], (char)bytes[offset + 2], (char)bytes[offset + 3] });
        }

        private void UpdatePreviewStop()
        {
            if (_stopPreviewAt <= 0d || EditorApplication.timeSinceStartup < _stopPreviewAt) return;
            StopPreview();
        }

        private void StopPreview()
        {
            if (_soundPlayer != null)
            {
                _soundPlayer.Stop();
                _soundPlayer.Dispose();
                _soundPlayer = null;
            }

            _stopPreviewAt = -1d;

            if (!string.IsNullOrEmpty(_temporaryWavePath))
            {
                try
                {
                    if (File.Exists(_temporaryWavePath)) File.Delete(_temporaryWavePath);
                }
                catch (IOException)
                {
                }

                _temporaryWavePath = null;
            }
        }

        private static bool IsRuntimeTrimmed(SfxId id)
        {
            return id == SfxId.Perfect || id == SfxId.Good || id == SfxId.Miss || id == SfxId.FeverHit;
        }

        private struct WaveInfo
        {
            public int Channels;
            public int SampleRate;
            public int BitsPerSample;
            public int DataOffset;
            public int DataSize;
        }
    }
}
