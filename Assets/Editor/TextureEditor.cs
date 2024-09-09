using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class TextureEditor : EditorWindow
{
    Texture2D mainTex = null;
    int tolerance = 0;
    List<ColorMatch> colorMatch = new List<ColorMatch>();
    const int initColorCount = 2;

    const string path = "Assets/Textures/";

    [MenuItem("Tools/Texture Editor")]
    public static void showWindow()
    {
        GetWindow<TextureEditor>();
    }

    private void OnGUI()
    {
        mainTex = (Texture2D)EditorGUILayout.ObjectField("Main Texture",mainTex, typeof(Texture2D), false);
        tolerance = EditorGUILayout.IntField("Color Tolerance", tolerance);

        if(colorMatch.Count == 0)
        {
            for(int i = 0; i < initColorCount; i++)
            {
                ColorMatch c = new ColorMatch(Color.white, Color.black);
                colorMatch.Add(c);
            }
        }

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Add Color"))
        {
            AddColor();
        }
        if(GUILayout.Button("Remove Color"))
        {
            RemoveColor();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        for(int i = 0; i < colorMatch.Count; i++)
        {
            GUILayout.BeginHorizontal();
            colorMatch[i].checkColor = EditorGUILayout.ColorField("Check Color " + (i+1), colorMatch[i].checkColor);
            colorMatch[i].targetColor = EditorGUILayout.ColorField("Target Color" + (i + 1), colorMatch[i].targetColor);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(15);
        if(mainTex != null)
        {
            if(GUILayout.Button("Create Custom"))
            {
                if(AssetDatabase.Contains(mainTex))
                {
                    int width = mainTex.width, height = mainTex.height;
                    Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);

                    for (int i = 0; i < width; i++)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            foreach(ColorMatch c in colorMatch)
                            {
                                if (CheckColor(mainTex.GetPixel(i, j), c.checkColor, tolerance))
                                {
                                    texture.SetPixel(i, j, c.targetColor);
                                    break;
                                }
                                else
                                    texture.SetPixel(i, j, mainTex.GetPixel(i, j));
                            }
                        }
                    }
                    texture.Apply();

                    SaveTextureToFile(texture, "AAA.png");
                    //AssetDatabase.CreateAsset(texture, "Assets/Textures/_Copy.png");
                    //if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(mainTex), $"Assets/Textures/"+filename))
                    //    Debug.LogWarning($"Failed to copy {path}");
                    //else
                    //{
                    //    Debug.Log("pixel 1 color = " + mainTex.GetPixel(0, 0));
                    //    mainTex = (Texture2D)AssetDatabase.LoadAssetAtPath(path + filename, typeof(Texture2D));
                    //    //ChangeColors(mainTex);
                    //}
                }
            }
        }
    }

    bool CheckColor(Color c1, Color c2, int t = 0)
    {
        if(t == 0)
        {
            if (c1.r == c2.r && c1.g == c2.g && c1.b == c2.b)
                return true;
            else
                return false;
        }
        else
        {
            if (Mathf.Abs(c1.r - c2.r) <= t * .01f && Mathf.Abs(c1.g - c2.g) <= t * .01f && Mathf.Abs(c1.b - c2.b) <= t * .01f)
                return true;
            else
                return false;
        }
    }

    bool CheckColor2(Color c1, Color c2)
    {
        Debug.Log(c1.r);
        if (c1.r == c2.r && c1.g == c2.g && c1.b == c2.b)
            return true;
        else
            return false;
    }

    void AddColor()
    {
        ColorMatch c = new ColorMatch(Color.white, Color.black);
        colorMatch.Add(c);
    }
    void RemoveColor()
    {
        if (colorMatch.Count <= 0)
            return;

        colorMatch.RemoveAt(colorMatch.Count - 1);
    }
    void SaveTextureToFile(Texture2D texture, string filename)
    {
        byte[] bytes;
        bytes = texture.EncodeToPNG();

        FileStream fileSave;
        fileSave = new FileStream(Application.dataPath + "/Textures/" + filename, FileMode.Create);
        Debug.Log("File Created");
        BinaryWriter binary;
        binary = new BinaryWriter(fileSave);
        binary.Write(bytes);
        fileSave.Close();
    }

    [System.Serializable]
    public class ColorMatch
    {
        public Color checkColor = Color.white;
        public Color targetColor = Color.white;

        public ColorMatch(Color _checkColor, Color _targetColor)
        {
            checkColor = _checkColor;
            targetColor = _targetColor;
        }
    }
}
