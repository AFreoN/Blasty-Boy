using UnityEngine;
using UnityEditor;

public class Shapes : EditorWindow
{
    public static Shapes instance;
    static bool isPositioned = false;

    [MenuItem("Tools/Create Shapes #_A")]
    public static void showWindow()
    {
        if(instance == null)
            GetWindow<Shapes>();
        else
            instance.Close();
    }

    private void OnFocus()
    {
        Repaint();
    }

    private void OnGUI()
    {
        if(!isPositioned)
        {
            //InitializeWindow();
        }
        GUILayout.Space(10);
        var style = new GUIStyle(GUI.skin.button);
        style.normal.textColor = Color.red;

        if(GUILayout.Button("Cube", style))
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.GetComponent<MeshRenderer>().sharedMaterial = (Material)AssetDatabase.LoadAssetAtPath("Assets/Editor/White", typeof(Material));
            Undo.RegisterCreatedObjectUndo(cube, "Cube created");
            Selection.activeGameObject = cube;
            this.Close();
        }

        if(GUILayout.Button("Capsule"))
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(capsule, "Capsule created");
            Selection.activeGameObject = capsule;
            this.Close();
        }

        if(GUILayout.Button("Sphere"))
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(sphere, "Sphere created");
            Selection.activeGameObject = sphere;
            this.Close();
        }

        if(GUILayout.Button("Cylinder"))
        {
            GameObject Cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(Cylinder, "Cylinder created");
            Selection.activeGameObject = Cylinder;
            this.Close();
        }

        if(GUILayout.Button("Plane"))
        {
            GameObject Plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(Plane, "Plane created");
            Selection.activeGameObject = Plane;
            this.Close();
        }

        if(GUILayout.Button("Quad"))
        {
            GameObject Quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Undo.RegisterCreatedObjectUndo(Quad, "Quad created");
            Selection.activeGameObject = Quad;
            this.Close();
        }
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    void InitializeWindow()
    {
        Repaint();
        Vector2 mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        position = getWindowPosition(mousePos, 300, 150);
        //Debug.Log("Mouse Position = " + mousePos);
        //Debug.Log("Screen  = " + Screen.width + "," + Screen.height);
        //position = new Rect(mousePos.x, mousePos.y, position.width, position.height);
        isPositioned = true;
    }

    Rect getWindowPosition(Vector2 mousePos, float width, float height)
    {
        float centerX = width / Screen.width;
        float centerY = height / Screen.height;

        centerX *= .5f;
        centerY *= .5f;

        return new Rect(mousePos.x + centerX, mousePos.y + centerY, width, height);
    }

    ShapeSceneManager _shapeSceneManager;

    private void OnEnable()
    {
        instance = this;

        Repaint();
        isPositioned = false;

        _shapeSceneManager = CreateInstance<ShapeSceneManager>();
        _shapeSceneManager.parentWindow = this;
        SceneView.duringSceneGui += _shapeSceneManager.OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= _shapeSceneManager.OnSceneGUI;
    }
}
