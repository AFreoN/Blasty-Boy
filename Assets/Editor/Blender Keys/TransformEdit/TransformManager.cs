using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace UnityMadeAwesome.BlenderInUnity
{
    [InitializeOnLoad]
    public class TransformManager
    {
        private static Translate translateEdit;
        private static Rotate rotateEdit;
        private static Scale scaleEdit;
        private static ModalEdit activeModal;
        private static ModalEdit delayStart;

        private static bool swallowMouse = false;
        private static int mouseButton;

        public static Vector3 position;

        static float windowWidth;
        static float windowHeight;

        // Use this for initialization
        static TransformManager()
        {
            SceneView.duringSceneGui += SceneGUI;
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyOnGUI;

            // Create our model edit singletons.
            translateEdit = new Translate();
            rotateEdit = new Rotate();
            scaleEdit = new Scale();
        }

        static void HierarchyOnGUI(int i, Rect r)
        {
            if (!Data.transformEditingEnabled)
            {
                return;
            }

            if (Event.current.alt == false && Event.current.shift == false && Event.current.control == false && Event.current.type == EventType.KeyDown)  //While Mouse is in hierarchy window or hierarchy in focus
            {
                if (Event.current.keyCode == Data.translateKey )
                {
                    SceneView.lastActiveSceneView.Focus();
                    Event.current.Use();

                    // Hey translate! We'll start it on next SceneGUI!
                    delayStart = translateEdit;
                }
                
                if (Event.current.keyCode == Data.rotateKey)
                {
                    SceneView.lastActiveSceneView.Focus();
                    Event.current.Use();
                    delayStart = rotateEdit;
                }

                if (Event.current.keyCode == Data.scaleKey)
                {
                    SceneView.lastActiveSceneView.Focus();
                    Event.current.Use();
                    delayStart = scaleEdit;
                }

            }
        }

        static void SceneGUI(SceneView sceneView)
        {
            if (!Data.transformEditingEnabled)
            {
                return;
            }

            if (activeModal != null)
            {
                activeModal.Update();

                if (EditorWindow.focusedWindow != sceneView)
                {
                    // SceneView lost focus but we're in a mode so we force it back.
                    sceneView.Focus();
                }

                // We force the scene to continue to update if we are in a mode.
                HandleUtility.Repaint();
            }

            if (delayStart != null)
            {
                // We got a message to start!
                if (activeModal != null)
                {
                    activeModal.Cancel();
                }

                activeModal = delayStart;
                delayStart = null;
                activeModal.Start();
            }

            //While Scene View on focus
            if (Event.current.isKey && Event.current.alt == false && Event.current.shift == false && Event.current.control == false && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == Data.translateKey)
                {
                    Event.current.Use();

                    if (activeModal != null)
                    {
                        activeModal.Cancel();
                    }

                    activeModal = translateEdit;
                    activeModal.Start();
                }
                else if (Event.current.keyCode == Data.rotateKey)
                {
                    Event.current.Use();

                    if (activeModal != null)
                    {
                        activeModal.Cancel();
                    }

                    activeModal = rotateEdit;
                    activeModal.Start();
                }
                if (Event.current.keyCode == Data.scaleKey)
                {
                    Event.current.Use();

                    if (activeModal != null)
                    {
                        activeModal.Cancel();
                    }

                    activeModal = scaleEdit;
                    activeModal.Start();
                }
            }

            if (swallowMouse)
            {
                if (Event.current.type == EventType.MouseMove && Event.current.button == mouseButton)
                {
                    if (Event.current.type == EventType.MouseUp)
                    {
                        swallowMouse = false;
                    }
                    
                    Event.current.Use();
                }
            }

            if(Selection.activeTransform != null)
                renderValues();
        }

        public static void ModalFinished()
        {
            activeModal = null;
        }

        public static void SwallowMouseUntilUp(int button)
        {
            swallowMouse = true;
            mouseButton = button;
        }


        #region Values Rendering OnSceneGUI
        static void renderValues()
        {
            if (activeModal == translateEdit)
            {
                Handles.BeginGUI();
                HandleTranslateValues();
                Handles.EndGUI();
            }

            if (activeModal == rotateEdit)
            {
                Handles.BeginGUI();
                HandleRotateValues();
                Handles.EndGUI();
            }

            if(activeModal == scaleEdit)
            {
                Handles.BeginGUI();
                HandleScaleValues();
                Handles.EndGUI();
            }
        }

        static readonly string xHtml = "   <color=#FF0000><b>X</b></color>   ";
        static readonly string yHtml = "   <color=#FFFF00><b>Y</b></color>   ";
        static readonly string zHtml = "   <color=#0000FF><b>Z</b></color>   ";

        static void HandleTranslateValues()
        {
            #region Label Skin
            var skin = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 20,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            skin.normal.textColor = Color.white;
            #endregion
            float width = 200;
            float height = 30;
            Rect r = new Rect(10, 10, width, height);

            if (translateEdit.state.myMode == TransformState.Mode.Free)
            {
                var values = allAxisTranslateValue();

                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, values.Item1, skin);

                r.y += (height + 10);
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, values.Item2, skin);

                r.y += (height + 10);
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, values.Item3, skin);

                //EditorGUI.Vector3Field(r, "Position", Selection.activeGameObject.transform.localPosition);
            }
            else if(translateEdit.state.myMode == TransformState.Mode.SingleAxis)
            {
                var v = singleAxisTranslateValue();

                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, v.Item1,skin);
            }
            else if(translateEdit.state.myMode == TransformState.Mode.DoubleAxis)
            {
                var v = doubleAxisTranslateValue();

                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, v.Item1, skin);

                r.y += height + 10;
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, v.Item2, skin);
            }
        }

        static (string,string,string) allAxisTranslateValue()
        {
            string valueX = "", valueY = "", valueZ = "";
            Vector3 pos = Selection.activeTransform.localPosition;
            valueX = "   <color=#FF0000><b>X</b></color>   " + pos.x;
            valueY = "   <color=#FFFF00><b>Y</b></color>   " + pos.y;
            valueZ = "   <color=#0000FF><b>Z</b></color>   " + pos.z;

            return (valueX, valueY, valueZ);
        }

        static (string,Color) singleAxisTranslateValue()
        {
            string value = "";
            Color col = Color.white;
            Vector3 pos = Selection.activeTransform.localPosition;

            if(translateEdit.state.myAxis == TransformState.Axis.X)
            {
                value = "   <color=#FF0000><b>X</b></color>   " + pos.x;
                col = Color.red;
            }
            if (translateEdit.state.myAxis == TransformState.Axis.Y)
            {
                value = "   <color=#FFFF00><b>Y</b></color>   " + pos.y;
                col = Color.yellow;
            }
            if (translateEdit.state.myAxis == TransformState.Axis.Z)
            {
                value = "   <color=#0000FF><b>Z</b></color>   " + pos.z;
                col = Color.blue;
            }
            return (value,Color.white);
        }

        static (string, string) doubleAxisTranslateValue()
        {
            string s1 = "", s2 = "";
            Vector3 pos = Selection.activeTransform.localPosition;

            if(translateEdit.state.myAxis == TransformState.Axis.X)
            {
                s1 = yHtml + pos.y;
                s2 = zHtml + pos.z;
            }
            if (translateEdit.state.myAxis == TransformState.Axis.Y)
            {
                s1 = xHtml + pos.x;
                s2 = zHtml + pos.z;
            }
            if (translateEdit.state.myAxis == TransformState.Axis.Z)
            {
                s1 = xHtml + pos.x;
                s2 = yHtml + pos.y;
            }

            return (s1, s2);
        }

        static void HandleRotateValues()
        {
            #region Label Skin
            var skin = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 20,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            skin.normal.textColor = Color.white;
            #endregion
            float width = 200;
            float height = 30;
            Rect r = new Rect(10, 10, width, height);

            Vector3 rot = Selection.activeTransform.localEulerAngles;

            if (rotateEdit.state.myMode == TransformState.Mode.Free)
            {
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, xHtml + rot.x, skin);

                r.y += (height + 10);
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, yHtml + rot.y, skin);

                r.y += (height + 10);
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, zHtml + rot.z, skin);
            }
            else
            {
                if (rotateEdit.state.myAxis == TransformState.Axis.X)
                {
                    GUI.color = new Color(.7f, .7f, .7f, .5f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    GUI.Label(r, xHtml + rot.x, skin);
                }
                else if (rotateEdit.state.myAxis == TransformState.Axis.Y)
                {
                    GUI.color = new Color(.7f, .7f, .7f, .5f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    GUI.Label(r, yHtml + rot.y, skin);
                }
                else if (rotateEdit.state.myAxis == TransformState.Axis.Z)
                {
                    GUI.color = new Color(.7f, .7f, .7f, .5f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    GUI.Label(r, zHtml + rot.z, skin);
                }
            }
        }

        static void HandleScaleValues()
        {
            #region Label Skin
            var skin = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 20,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            skin.normal.textColor = Color.white;
            #endregion
            float width = 200;
            float height = 30;
            Rect r = new Rect(10, 10, width, height);

            Vector3 scale = Selection.activeTransform.localScale;

            if(scaleEdit.state.myMode == TransformState.Mode.Free)
            {
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, xHtml + scale.x, skin);

                r.y += (height + 10);
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, yHtml + scale.y, skin);

                r.y += (height + 10);
                GUI.color = new Color(.7f, .7f, .7f, .5f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, zHtml + scale.z, skin);
            }
            else
            {
                if(scaleEdit.state.myAxis == TransformState.Axis.X)
                {
                    GUI.color = new Color(.7f, .7f, .7f, .5f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    GUI.Label(r, xHtml + scale.x, skin);
                }
                else if (scaleEdit.state.myAxis == TransformState.Axis.Y)
                {
                    GUI.color = new Color(.7f, .7f, .7f, .5f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    GUI.Label(r, yHtml + scale.y, skin);
                }
                else
                {
                    GUI.color = new Color(.7f, .7f, .7f, .5f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    GUI.Label(r, zHtml + scale.z, skin);
                }
            }
        }
        #endregion
    }
}
