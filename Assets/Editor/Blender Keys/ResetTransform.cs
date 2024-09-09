using UnityEngine;
using UnityEditor;
using System.Collections;

namespace UnityMadeAwesome.BlenderInUnity
{
    public class ResetTransform
    {
        [MenuItem("Edit/BlenderKeys/Reset Position &_G", priority = Data.RESET_TRANSFORM_PRIORITY)]
        static void ResetPosition()
        {
            Undo.RecordObjects(Selection.transforms, "Reset Position");

            foreach (Transform transform in Selection.transforms)
            {
                transform.localPosition = Vector3.zero;
            }
        }

        [MenuItem("Edit/BlenderKeys/Reset Position &_G", validate = true)]
        static bool ResetPositionCheck()
        {
            return Data.resetTransformsEnabled;
        }

        [MenuItem("Edit/BlenderKeys/Reset Rotation &_R", priority = Data.RESET_TRANSFORM_PRIORITY)]
        static void ResetRotation()
        {
            Undo.RecordObjects(Selection.transforms, "Reset Rotation");

            foreach (Transform transform in Selection.transforms)
            {
                transform.localRotation = Quaternion.identity;
            }

        }

        [MenuItem("Edit/BlenderKeys/Reset Rotation &_R", validate = true)]
        static bool ResetRotationCheck()
        {
            return Data.resetTransformsEnabled;
        }

        [MenuItem("Edit/BlenderKeys/Reset Scale &s", priority = Data.RESET_TRANSFORM_PRIORITY)]
        static void ResetScale()
        {
            Undo.RecordObjects(Selection.transforms, "Reset Scale");

            foreach (Transform transform in Selection.transforms)
            {
                transform.localScale = Vector3.one;
            }
        }

        [MenuItem("Edit/BlenderKeys/Reset Scale &s", validate = true)]
        static bool ResetScaleCheck()
        {
            return Data.resetTransformsEnabled;
        }

        [MenuItem("Edit/BlenderKeys/Create Empty Child %&n", validate = true)]
        static bool CreateEmptyChildCheck()  
        {
            return Data.resetTransformsEnabled;
        }
    }
}
