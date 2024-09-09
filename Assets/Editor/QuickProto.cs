using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class QuickProto : ScriptableObject
{
    [MenuItem("Tools/Assign Material &_M")]
    static void assignMaterial()
    {
        Object[] allObject = Selection.objects;

        List<GameObject> sceneGO = new List<GameObject>();
        List<MeshRenderer> allMR = new List<MeshRenderer>();
        Material mat = null;

        foreach (Object o in allObject)
        {
            GameObject g = o as GameObject;
            if (!AssetDatabase.Contains(o))
            {
                if (g.GetComponent<MeshRenderer>() != null || g.GetComponent<SkinnedMeshRenderer>() != null)
                    sceneGO.Add(o as GameObject);

                if (g.GetComponent<MeshRenderer>() != null)
                    allMR.Add(g.GetComponent<MeshRenderer>());
            }
            else if (o is Material)
            {
                mat = o as Material;
            }
        }

        if (sceneGO.Count > 0 && mat != null)
        {
            Undo.RecordObjects(allMR.ToArray(), "Changed materials");

            foreach (GameObject g in sceneGO)
            {
                if (g.GetComponent<MeshRenderer>() != null)
                    g.GetComponent<MeshRenderer>().sharedMaterial = mat;
                else
                    g.GetComponent<SkinnedMeshRenderer>().sharedMaterial = mat;
            }
        }
    }

    //[MenuItem("Tools/Clamp To Ground _G")]
    static void clampToGround()
    {
        if(!AssetDatabase.Contains(Selection.activeGameObject))
        {
            Transform transform = Selection.activeGameObject.transform;

            if(Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit))
            {
                float boundY = 0;

                float final = 0;

                if (transform.GetComponent<MeshRenderer>() != null)
                {
                    boundY = transform.GetComponent<MeshRenderer>().bounds.size.y;

                    final = boundY * .5f ;
                }

                Undo.RecordObject(transform, "Clamped Transform");
                float yPos = (hit.point + final * Vector3.up).y;
                transform.position = new Vector3(transform.position.x, yPos, transform.position.z);
            }
            else
            {
                Undo.RecordObject(transform, "Clamped Transform");
                transform.position = new Vector3(transform.position.x, 0, transform.position.z);
            }
        }
    }

    //[MenuItem("Tools/Clamp To Right &_PGDN")]
    static void clampToRight()
    {
        Transform transform = Selection.activeGameObject.transform;
        if (transform == null)
            return;

        if (Physics.Raycast(transform.position, Vector3.right, out RaycastHit hit))
        {
            float boundX = 0;

            float final = 0;

            if (transform.GetComponent<MeshRenderer>() != null)
            {
                boundX = transform.GetComponent<MeshRenderer>().bounds.size.x;

                final = boundX * .5f;
            }

            Undo.RecordObject(transform, "Clamped Transform");
            float xPos = (hit.point - final * Vector3.right).x;
            transform.position = new Vector3(xPos, transform.position.y, transform.position.z);
        }
        else
        {
            Undo.RecordObject(transform, "Clamped Transform");
            transform.position = new Vector3(0, transform.position.y, transform.position.z);
        }
    }

    //[MenuItem("Tools/Clamp To Left &_4")]
    static void clampToLeft()
    {
        Transform transform = Selection.activeGameObject.transform;
        if (transform == null)
            return;

        if (Physics.Raycast(transform.position, Vector3.left, out RaycastHit hit))
        {
            float boundX = 0;

            float final = 0;

            if (transform.GetComponent<MeshRenderer>() != null)
            {
                boundX = transform.GetComponent<MeshRenderer>().bounds.size.x;

                final = boundX * .5f;
            }

            Undo.RecordObject(transform, "Clamped Transform");
            float xPos = (hit.point + final * Vector3.right).x;
            transform.position = new Vector3(xPos, transform.position.y, transform.position.z);
        }
        else
        {
            Undo.RecordObject(transform, "Clamped Transform");
            transform.position = new Vector3(0, transform.position.y, transform.position.z);
        }
    }

    //[MenuItem("Tools/Reset Rotation &_R")]
    static void resetRotation()
    {
        List<Transform> allTrans = new List<Transform>();

        foreach(GameObject g in Selection.gameObjects)
        {
            if(!AssetDatabase.Contains(g))
            {
                allTrans.Add(g.transform);
            }
        }

        if (allTrans.Count == 0)
            return;

        Undo.RecordObjects(allTrans.ToArray(), "Reset Rotations");
        foreach(Transform t in allTrans)
        {
            t.rotation = Quaternion.identity;
        }
    }

    //[MenuItem("Tools/Reset Position &_G")]
    static void resetPosition()
    {
        List<Transform> allTrans = new List<Transform>();

        foreach (GameObject g in Selection.gameObjects)
        {
            if (!AssetDatabase.Contains(g))
            {
                allTrans.Add(g.transform);
            }
        }

        if (allTrans.Count == 0)
            return;

        Undo.RecordObjects(allTrans.ToArray(), "Reset Positions");
        foreach (Transform t in allTrans)
        {
            t.position = Vector3.zero;
        }
    }

    [MenuItem("Tools/Delete Gameobjects &_X")]
    static void deleteObjects()
    {
        List<GameObject> allTrans = new List<GameObject>();

        foreach (GameObject g in Selection.gameObjects)
        {
            if (!AssetDatabase.Contains(g))
            {
                allTrans.Add(g);
            }
        }

        if (allTrans.Count == 0)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Deleted Gameobjects");
        var undoGroupIndex = Undo.GetCurrentGroup();

        foreach(GameObject g in allTrans)
        {
            Undo.DestroyObjectImmediate(g);
        }
        Undo.CollapseUndoOperations(undoGroupIndex);
    }

    [MenuItem("Tools/Make Sphere with Parent %_B")]
    static void MakeSphereWithParent()
    {
        Object[] Tall = Selection.GetFiltered(typeof(Transform), SelectionMode.ExcludePrefab);

        foreach(Transform t in Tall)
        {
            Transform sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            sphere.localScale = Vector3.one * .2f;
            sphere.SetParent(t);
            sphere.localPosition = Vector3.zero;
            DestroyImmediate(sphere.GetComponent<SphereCollider>());

            Undo.RegisterCreatedObjectUndo(sphere.gameObject, "Debug Sphere created");
        }
    }
}
