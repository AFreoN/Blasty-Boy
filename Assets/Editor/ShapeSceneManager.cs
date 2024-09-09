using UnityEngine;
using UnityEditor;

public class ShapeSceneManager : Editor
{
    bool gotPos = false;
    Rect center;
    Vector2 scrollPosition = Vector2.zero;

    Texture2D circleTex;
    Vector2 startMousePosition = Vector2.zero;
    Material whiteMat;

    int gapPixel = 2;
    int buttonWidth = 60;
    int buttonHeight = 60;

    int totalButtons = 6;

    public Shapes parentWindow;

    public void OnSceneGUI(SceneView sceneView)
    {
        if(!gotPos)
        {
            buttonHeight -= gapPixel * 2;
            buttonWidth -= gapPixel * 2;
            center = getScreenPosition(200, ((float)totalButtons * buttonHeight + (((float)totalButtons+1) * gapPixel)));

            startMousePosition = Event.current.mousePosition;
            circleTex = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/Editor/Circle.png", typeof(Texture2D));
            whiteMat = (Material)AssetDatabase.LoadAssetAtPath("Assets/Editor/White.mat", typeof(Material));


            gotPos = true;
        }

        //HandleScrollBarView();
        HandleCircularView();

        if(Event.current.type == EventType.MouseDown)
        {
            parentWindow.Close();
        }

        SceneView.RepaintAll();
    }

    void HandleScrollBarView()
    {
        Handles.BeginGUI();

        GUI.Box(center, GUIContent.none);

        Rect scrollRect = new Rect(center.x, center.y, center.width, center.height);
        scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, scrollRect, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);

        Rect buttonRect = new Rect(center.x + gapPixel, center.y + gapPixel, buttonWidth, buttonHeight);

        if (GUI.Button(buttonRect, "Cube"))
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Cube created");
            Selection.activeGameObject = cube;
            parentWindow.Close();
        }

        buttonRect.y += buttonHeight + gapPixel;
        if (GUI.Button(buttonRect, "Sphere"))
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(sphere, "Sphere created");
            Selection.activeGameObject = sphere;
            parentWindow.Close();
        }

        buttonRect.y += buttonHeight + gapPixel;
        if (GUI.Button(buttonRect, "Capsule"))
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(capsule, "Capsule created");
            Selection.activeGameObject = capsule;
            parentWindow.Close();
        }

        buttonRect.y += buttonHeight + gapPixel;
        if(GUI.Button(buttonRect, "Cylinder"))
        {
            GameObject Cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(Cylinder, "Cylinder created");
            Selection.activeGameObject = Cylinder;
            parentWindow.Close();
        }

        buttonRect.y += buttonHeight + gapPixel;
        if(GUI.Button(buttonRect, "Plane"))
        {
            GameObject Plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(Plane, "Plane created");
            Selection.activeGameObject = Plane;
            parentWindow.Close();
        }

        buttonRect.y += buttonHeight + gapPixel;
        if(GUI.Button(buttonRect, "Quad"))
        {
            GameObject Quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Undo.RegisterCreatedObjectUndo(Quad, "Quad created");
            Selection.activeGameObject = Quad;
            parentWindow.Close();
        }

        GUI.EndScrollView();


        Handles.EndGUI();
    }

    void HandleCircularView()
    {
        Handles.BeginGUI();

        Vector3 direction = Vector3.down;
        float distance = 100;

        if (circleTex != null)
        {
            GUI.color = new Color(0,.9f,.6f,1);
            GUI.DrawTexture(new Rect(startMousePosition.x - 150, startMousePosition.y - 150 + buttonHeight * .5f, 300, 300), circleTex);
        }
        else
            Debug.LogWarning("No Texture Found");

        GUI.color = Color.white;
        var style = new GUIStyle(GUI.skin.button);
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;
        //style.fontSize = 12;
        GUI.backgroundColor = new Color(1,1,0,1);

        direction *= distance;
        Rect buttonRect = new Rect(startMousePosition.x + direction.x - buttonWidth * .5f, startMousePosition.y + direction.y, buttonWidth, buttonHeight);
        Handles.DrawLine(startMousePosition + Vector2.up * buttonHeight * .5f, new Vector2(buttonRect.x + buttonWidth * .5f, buttonRect.y + buttonHeight * .5f));

        if(GUI.Button(buttonRect, "Cube", style))
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
            Undo.RegisterCreatedObjectUndo(cube, "Cube created");
            Selection.activeGameObject = cube;
            parentWindow.Close();
        }

        direction = Quaternion.AngleAxis(-(360f/totalButtons), Vector3.forward) * direction;
        buttonRect = new Rect(startMousePosition.x + direction.x - buttonWidth * .5f, startMousePosition.y + direction.y, buttonWidth, buttonHeight);
        Handles.DrawLine(startMousePosition + Vector2.up * buttonHeight * .5f, new Vector2(buttonRect.x + buttonWidth * .5f, buttonRect.y + buttonHeight * .5f));
        if (GUI.Button(buttonRect, "Sphere", style))
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
            Undo.RegisterCreatedObjectUndo(sphere, "Sphere created");
            Selection.activeGameObject = sphere;
            parentWindow.Close();
        }

        direction = Quaternion.AngleAxis(-(360f/totalButtons), Vector3.forward) * direction;
        buttonRect = new Rect(startMousePosition.x + direction.x - buttonWidth * .5f, startMousePosition.y + direction.y, buttonWidth, buttonHeight);
        Handles.DrawLine(startMousePosition + Vector2.up * buttonHeight * .5f, new Vector2(buttonRect.x + buttonWidth * .5f, buttonRect.y + buttonHeight * .5f));
        if (GUI.Button(buttonRect, "Cylinder", style))
        {
            GameObject Cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Cylinder.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
            Undo.RegisterCreatedObjectUndo(Cylinder, "Cylinder created");
            Selection.activeGameObject = Cylinder;
            parentWindow.Close();
        }

        direction = Quaternion.AngleAxis(-(360f / totalButtons), Vector3.forward) * direction;
        buttonRect = new Rect(startMousePosition.x + direction.x - buttonWidth * .5f, startMousePosition.y + direction.y, buttonWidth, buttonHeight);
        Handles.DrawLine(startMousePosition + Vector2.up * buttonHeight * .5f, new Vector2(buttonRect.x + buttonWidth * .5f, buttonRect.y + buttonHeight * .5f));
        if (GUI.Button(buttonRect, "Capsule", style))
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
            Undo.RegisterCreatedObjectUndo(capsule, "Capsule created");
            Selection.activeGameObject = capsule;
            parentWindow.Close();
        }

        direction = Quaternion.AngleAxis(-(360f / totalButtons), Vector3.forward) * direction;
        buttonRect = new Rect(startMousePosition.x + direction.x - buttonWidth * .5f, startMousePosition.y + direction.y, buttonWidth, buttonHeight);
        Handles.DrawLine(startMousePosition + Vector2.up * buttonHeight * .5f, new Vector2(buttonRect.x + buttonWidth * .5f, buttonRect.y + buttonHeight * .5f));
        if (GUI.Button(buttonRect, "Plane", style))
        {
            GameObject Plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Plane.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
            Undo.RegisterCreatedObjectUndo(Plane, "Plane created");
            Selection.activeGameObject = Plane;
            parentWindow.Close();
        }

        direction = Quaternion.AngleAxis(-(360f / totalButtons), Vector3.forward) * direction;
        buttonRect = new Rect(startMousePosition.x + direction.x - buttonWidth * .5f, startMousePosition.y + direction.y, buttonWidth, buttonHeight);
        Handles.DrawLine(startMousePosition + Vector2.up * buttonHeight * .5f, new Vector2(buttonRect.x + buttonWidth * .5f, buttonRect.y + buttonHeight * .5f));
        if (GUI.Button(buttonRect, "Quad", style))
        {
            GameObject Quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Quad.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
            Undo.RegisterCreatedObjectUndo(Quad, "Quad created");
            Selection.activeGameObject = Quad;
            parentWindow.Close();
        }

        GUI.color = new Color(1, 1, 1, 1);
        GUI.DrawTexture(new Rect(startMousePosition.x - 25, startMousePosition.y - 25 + buttonHeight * .5f, 50, 50), circleTex);

        Handles.EndGUI();
    }

    Rect getScreenPosition(float width, float height)
    {
        float startX = Event.current.mousePosition.x - width * .5f;
        float startY = Event.current.mousePosition.y- height * .5f;
        float endX = width;
        float endY = height;

        
        return new Rect(startX, startY, endX, endY);
    }
}
