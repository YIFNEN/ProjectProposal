using System;
using UnityEngine;

namespace AO.Character
{
    [Serializable]
    public struct DVariantBodyCalibrationProfile
    {
        public bool IsValid;
        public float HorizontalScale;
        public float VerticalScale;
        public float DepthScale;
        public Vector3 WorkspacePositiveLocal;
        public Vector3 WorkspaceNegativeLocal;

        public static DVariantBodyCalibrationProfile FromRigDefaults(DVariantRiderRig rig)
        {
            if (rig == null)
            {
                return new DVariantBodyCalibrationProfile
                {
                    IsValid = false,
                    HorizontalScale = 1f,
                    VerticalScale = 1f,
                    DepthScale = 1f,
                    WorkspacePositiveLocal = Vector3.one,
                    WorkspaceNegativeLocal = Vector3.one
                };
            }

            return new DVariantBodyCalibrationProfile
            {
                IsValid = true,
                HorizontalScale = rig.BaseHorizontalScale,
                VerticalScale = rig.BaseVerticalScale,
                DepthScale = rig.BaseDepthScale,
                WorkspacePositiveLocal = rig.BaseHandWorkspacePositiveLocal,
                WorkspaceNegativeLocal = rig.BaseHandWorkspaceNegativeLocal
            };
        }
    }
}
