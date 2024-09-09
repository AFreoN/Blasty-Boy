using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EnemyController))]
public class EnemyInspector : Editor
{
    EnemyController ec = null;
    SerializedObject so;

    private void OnEnable()
    {
        so = new SerializedObject(target);
    }

    public override void OnInspectorGUI()
    {
        ec = (EnemyController)target;
        Undo.RecordObject(ec, "E control ");
        PrefabUtility.RecordPrefabInstancePropertyModifications(ec);

        ec.enemyType = (EnemyController.ENEMYTYPE)EditorGUILayout.EnumPopup("Enemy Type", ec.enemyType);
        EditorGUILayout.Space(5);

        switch(ec.enemyType)
        {
            case EnemyController.ENEMYTYPE.Standing:
                break;

            case EnemyController.ENEMYTYPE.PathWalker:
                ShowProperty("walking");
                break;

            case EnemyController.ENEMYTYPE.Chaser:
                ShowProperty("chasing");
                break;

            case EnemyController.ENEMYTYPE.Bully:
                ShowProperty("bullying");
                break;
        }
    }

    void ShowProperty(string s)
    {
        Undo.RecordObject(target, "Record enemy changes");
        SerializedProperty sp = so.FindProperty(s);
        EditorGUILayout.PropertyField(sp);
        so.ApplyModifiedProperties();
    }
}
