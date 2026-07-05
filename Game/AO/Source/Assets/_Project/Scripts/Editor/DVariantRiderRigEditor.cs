using AO.Character;
using UnityEditor;
using UnityEngine;

namespace AO.Editor
{
    [CustomEditor(typeof(DVariantRiderRig))]
    public sealed class DVariantRiderRigEditor : UnityEditor.Editor
    {
        private static readonly Color LeftColor = new Color(0.1f, 0.9f, 1f, 0.95f);
        private static readonly Color RightColor = new Color(1f, 0.35f, 0.9f, 0.95f);
        private static readonly string[] AxisLabels = { "X", "Y", "Z" };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DVariantRiderRig rig = (DVariantRiderRig)target;
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Gameplay hand reach is normally governed by the mapping scale values. " +
                "Enable Limit Hand Workspace only as a debug hard clamp, then drag the colored Scene view handles to inspect or tune the clamp box.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture Hand Rest"))
                {
                    Undo.RecordObject(rig, "Capture D-Variant Hand Rest");
                    rig.CaptureSceneAuthoredHandRest();
                    EditorUtility.SetDirty(rig);
                }

                if (GUILayout.Button("Recenter Calibration"))
                {
                    rig.RecenterControllerCalibration();
                }
            }
        }

        private void OnSceneGUI()
        {
            DVariantRiderRig rig = (DVariantRiderRig)target;
            if (rig == null) return;

            serializedObject.Update();

            SerializedProperty show = serializedObject.FindProperty("_showHandWorkspaceGizmos");
            SerializedProperty limit = serializedObject.FindProperty("_limitHandWorkspace");
            if (show != null && !show.boolValue) return;
            if (limit != null && !limit.boolValue) return;

            SerializedProperty positiveProp = serializedObject.FindProperty("_handWorkspacePositiveLocal");
            SerializedProperty negativeProp = serializedObject.FindProperty("_handWorkspaceNegativeLocal");
            SerializedProperty leftRestProp = serializedObject.FindProperty("_leftHandRestLocal");
            SerializedProperty rightRestProp = serializedObject.FindProperty("_rightHandRestLocal");
            if (positiveProp == null || negativeProp == null || leftRestProp == null || rightRestProp == null) return;

            Vector3 positive = AbsVector(positiveProp.vector3Value);
            Vector3 negative = AbsVector(negativeProp.vector3Value);

            EditorGUI.BeginChangeCheck();
            DrawHandWorkspaceControls(rig.transform, leftRestProp.vector3Value, LeftColor, "Left", ref positive, ref negative);
            DrawHandWorkspaceControls(rig.transform, rightRestProp.vector3Value, RightColor, "Right", ref positive, ref negative);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rig, "Adjust D-Variant Hand Workspace");
                positiveProp.vector3Value = AbsVector(positive);
                negativeProp.vector3Value = AbsVector(negative);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(rig);
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private static void DrawHandWorkspaceControls(
            Transform rigTransform,
            Vector3 restLocal,
            Color color,
            string label,
            ref Vector3 positive,
            ref Vector3 negative)
        {
            Matrix4x4 localToWorld = rigTransform.localToWorldMatrix;
            Matrix4x4 worldToLocal = rigTransform.worldToLocalMatrix;
            Vector3 restWorld = localToWorld.MultiplyPoint3x4(restLocal);

            Handles.color = color;
            Handles.Label(restWorld + rigTransform.up * 0.08f, $"{label} rest");

            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 axisLocal = AxisVector(axis);
                Vector3 axisWorld = localToWorld.MultiplyVector(axisLocal).normalized;

                Vector3 positiveLocal = restLocal + axisLocal * GetAxis(positive, axis);
                Vector3 negativeLocal = restLocal - axisLocal * GetAxis(negative, axis);

                Vector3 positiveWorld = localToWorld.MultiplyPoint3x4(positiveLocal);
                Vector3 negativeWorld = localToWorld.MultiplyPoint3x4(negativeLocal);
                float positiveSize = HandleUtility.GetHandleSize(positiveWorld) * 0.075f;
                float negativeSize = HandleUtility.GetHandleSize(negativeWorld) * 0.075f;

                Handles.color = color;
                Vector3 newPositiveWorld = Handles.Slider(
                    positiveWorld,
                    axisWorld,
                    positiveSize,
                    Handles.CubeHandleCap,
                    0f);
                Vector3 newNegativeWorld = Handles.Slider(
                    negativeWorld,
                    -axisWorld,
                    negativeSize,
                    Handles.CubeHandleCap,
                    0f);

                Vector3 newPositiveLocal = worldToLocal.MultiplyPoint3x4(newPositiveWorld);
                Vector3 newNegativeLocal = worldToLocal.MultiplyPoint3x4(newNegativeWorld);

                SetAxis(ref positive, axis, Mathf.Max(0.01f, GetAxis(newPositiveLocal - restLocal, axis)));
                SetAxis(ref negative, axis, Mathf.Max(0.01f, GetAxis(restLocal - newNegativeLocal, axis)));

                Handles.Label(positiveWorld + axisWorld * (positiveSize * 1.5f), $"+{AxisLabels[axis]}");
                Handles.Label(negativeWorld - axisWorld * (negativeSize * 1.5f), $"-{AxisLabels[axis]}");
            }
        }

        private static Vector3 AxisVector(int axis)
        {
            return axis switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward,
            };
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            return axis switch
            {
                0 => value.x,
                1 => value.y,
                _ => value.z,
            };
        }

        private static void SetAxis(ref Vector3 value, int axis, float axisValue)
        {
            if (axis == 0) value.x = axisValue;
            else if (axis == 1) value.y = axisValue;
            else value.z = axisValue;
        }

        private static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
