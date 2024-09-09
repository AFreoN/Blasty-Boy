using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace CustomExtensions
{
    public static partial class Extension
    {
        #region Vectors
        public static Vector2 xy(this Vector3 v)
        {
            return new Vector2(v.x, v.y);
        }

        public static Vector3 ReplaceX(this Vector3 v, float x)
        {
            return new Vector3(x, v.y, v.z);
        }

        public static Vector3 ReplaceY(this Vector3 v, float y)
        {
            return new Vector3(v.x, y, v.z);
        }

        public static Vector3 ReplaceZ(this Vector3 v, float z)
        {
            return new Vector3(v.x, v.y, z);
        }

        public static Vector3 GetClosestPoint(this Vector3 v, Vector3[] allPoints)
        {
            float dis = float.MaxValue;
            Vector3 result = Vector3.zero;
            for (int i = 0; i < allPoints.Length; i++)
            {
                if (Vector3.Distance(v, allPoints[i]) < dis)
                {
                    dis = Vector3.Distance(v, allPoints[i]);
                    result = allPoints[i];
                }
            }

            return result;
        }

        public static Vector3 RotateInXAxis(this Vector3 v, float angle)
        {
            return Quaternion.Euler(new Vector3(angle, 0, 0)) * v;
        }

        public static Vector3 RotateInYAxis(this Vector3 v, float angle)
        {
            return Quaternion.Euler(new Vector3(0, angle, 0)) * v;
        }

        public static Vector3 RotateInZAxis(this Vector3 v, float angle)
        {
            return Quaternion.Euler(new Vector3(0, 0, angle)) * v;
        }

        public static Vector3 MultiplyBy(this Vector3 v, Vector3 multiplyVector)
        {
            return new Vector3(v.x * multiplyVector.x, v.y * multiplyVector.y, v.z * multiplyVector.z);
        }
        #endregion

        #region Math
        public static Vector3 ToVector(this float v)
        {
            return Vector3.one * v;
        }

        public static Vector3 ToVector(this int i)
        {
            return Vector3.one * (float)i;
        }

        public static float Clamp(this float fl, float min, float max)
        {
            float result = fl;
            if (fl < min)
                result = min;
            else if (fl > max)
                result = max;

            return result;
        }

        public static int Clamp(this int i, int min, int max)
        {
            int result = i;
            if (i < min)
                result = min;
            else if (i > max)
                result = max;

            return result;
        }

        public static int ClampToMin(this int i, int min, int max)
        {
            int result = i;
            if (i < min || i > max)
                result = min;

            return result;
        }

        public static int ClampToMax(this int i, int min, int max)
        {
            int result = i;
            if (i < min || i > max)
                result = max;

            return result;
        }
        #endregion

        #region Gameobject
        //For getting component from gameobject (unity default thing)
        public static T GetComponentRequired<T>(this GameObject self) where T : Component
        {
            T component = self.GetComponent<T>();

            if (component == null) Debug.LogError("Could not find " + typeof(T) + " on " + self.name);

            return component;
        }

        public static T[] ToOtherComponentArray<T>(this GameObject[] g) where T : Component
        {
            T[] t = new T[g.Length];
            for (int i = 0; i < g.Length; i++)
            {
                t[i] = g[i].GetComponent<T>();
            }
            return t;
        }

        //public static List<T> ToOtherComponentList<T>(this List<GameObject> g) where T : Component
        //{
        //    List<T> result = new List<T>();
        //    for(int i = 0; i < g.Count; i++)
        //    {
        //        result.Add(g[i].GetComponent<T>());
        //    }
        //    return result;
        //}

        //If component exists in gameobject, call the action
        public static T GetComponent<T>(this GameObject self, System.Action<T> callback) where T : Component
        {
            var component = self.GetComponent<T>();

            if (component != null)
            {
                callback.Invoke(component);
            }

            return component;
        }

        //Is component exists call the success action, otherwise call failure action
        public static T GetComponent<T>(this GameObject self, System.Action<T> success, System.Action failure) where T : Component
        {
            var component = self.GetComponent<T>();

            if (component != null)
            {
                success.Invoke(component);
                return component;
            }
            else
            {
                failure.Invoke();
                return null;
            }
        }

        public static bool HasComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() != null;
        }
        #endregion

        #region Component
        //Array of any components can be used by this method and calls action thrown to this with single component parameter
        public static void ForEachComponent<T>(this T[] array, System.Action<T> callback) where T : Component
        {
            for (var i = 0; i < array.Length; i++)
            {
                //callback.Invoke(array[i]);
                callback(array[i]);
            }
        }

        //Same as ForEachComponent function, but it doesn't require any parameter
        public static void ForEachComponent<T>(this T[] array, System.Action callback) where T : Component
        {
            for (var i = 0; i < array.Length; i++)
            {
                callback();
            }
        }

        //Add component to any component's gameobject
        public static T AddComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.AddComponent<T>();
        }

        //Get component from any component's gameobject
        public static T GetComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.GetComponent<T>();
        }

        //Get the bool of whether the required component is present in the gameobject or noe
        public static bool HasComponent<T>(this Component component) where T : Component
        {
            return component.GetComponent<T>() != null;
        }

        //Get the bool of true if component is present and calls the given action, else returns false
        public static bool HasComponent<T>(this Component component, System.Action<T> callback) where T : Component
        {
            T c = component.gameObject.GetComponent<T>();
            if (c != null)
            {
                callback(c);
                return true;
            }
            else
                return false;
        }

        public static bool HasComponent<T>(this Component component, System.Action<T> success, System.Action fail) where T : Component
        {
            T c = component.gameObject.GetComponent<T>();
            if (c != null)
            {
                success(c);
                return true;
            }
            else
            {
                fail();
                return false;
            }
        }

        public static T[] ToOtherComponentArray<T>(this Component[] c) where T : Component
        {
            T[] t = new T[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                t[i] = c[i].gameObject.GetComponent<T>();
            }
            return t;
        }

        public static List<T> ToOtherComponentList<T>(this Component[] c) where T : Component
        {
            List<T> local = new List<T>();
            for (int i = 0; i < c.Length; i++)
            {
                local.Add(c[i].gameObject.GetComponent<T>());
            }
            return local;
        }

        public static T[] ArrayAdd<T>(this T[] arr, T add)
        {
            T[] ret = new T[arr.Length + 1];
            for (int i = 0; i < arr.Length; i++)
            {
                ret[i] = arr[i];
            }
            ret[arr.Length] = add;
            //arr = ret;
            return ret;
        }

        public static T[] ArrayAdd<T>(this T[] arr, T[] add)
        {
            T[] ret = new T[arr.Length + add.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                ret[i] = arr[i];
            }
            for (int j = 0; j < add.Length; j++)
            {
                ret[j + arr.Length] = add[j];
            }

            //arr = ret;
            return ret;
        }

        public static bool CompareTag(this Collision c, string tag)
        {
            return c.gameObject.CompareTag(tag);
        }
        #endregion

        #region Transform
        public static Quaternion GetLookAtRotation(this Transform self, Vector3 target)
        {
            return Quaternion.LookRotation(target - self.position);
        }

        public static Quaternion GetLookAtRotation(this Transform self, Transform target)
        {
            return Quaternion.LookRotation(target.position - self.position);
        }

        public static Quaternion GetLookAtRotationIgnoreX(this Transform self, Vector3 target)
        {
            Vector3 dif = (target - self.position).ReplaceX(self.position.x);
            return Quaternion.LookRotation(dif);
        }

        public static Quaternion GetLookAtRotationIgnoreX(this Transform self, Transform target)
        {
            return self.GetLookAtRotationIgnoreX(target.position);
        }

        public static Quaternion GetLookAtRotationIgnoreY(this Transform self, Vector3 target)
        {
            Vector3 dif = (target - self.position).ReplaceY(self.position.y);
            return Quaternion.LookRotation(dif);
        }

        public static Quaternion GetLookAtRotationIgnoreY(this Transform self, Transform target)
        {
            return self.GetLookAtRotationIgnoreY(target.position);
        }

        public static Quaternion GetLookAtRotationIgnoreZ(this Transform self, Vector3 target)
        {
            Vector3 dif = (target - self.position).ReplaceZ(self.position.z);
            return Quaternion.LookRotation(dif);
        }

        public static Quaternion GetLookAtRotationIgnoreZ(this Transform self, Transform target)
        {
            return self.GetLookAtRotationIgnoreZ(target.position);
        }

        public static Transform GetClosestTransform(this Transform t, Transform[] allTargets)
        {
            float dis = float.MaxValue;
            Transform result = null;
            for (int i = 0; i < allTargets.Length; i++)
            {
                if (Vector3.Distance(t.position, allTargets[i].position) < dis)
                {
                    dis = Vector3.Distance(t.position, allTargets[i].position);
                    result = allTargets[i];
                }
            }
            return result;
        }

        public static void SetX(this Transform t, float x, Space s = Space.World)
        {
            if (s == Space.World)
                t.position = t.position.ReplaceX(x);
            else
                t.localPosition = t.localPosition.ReplaceX(x);
        }

        public static void SetY(this Transform t, float y, Space s = Space.World)
        {
            if (s == Space.World)
                t.position = t.position.ReplaceY(y);
            else
                t.localPosition = t.localPosition.ReplaceY(y);
        }

        public static void SetZ(this Transform t, float z, Space s = Space.World)
        {
            if (s == Space.World)
                t.position = t.position.ReplaceZ(z);
            else
                t.localPosition = t.localPosition.ReplaceZ(z);
        }

        public static Transform[] GetChildsAsArray(this Transform t)
        {
            Transform[] childs = new Transform[t.childCount];
            for (int i = 0; i < childs.Length; i++)
            {
                childs[i] = t.GetChild(i);
            }
            return childs;
        }

        public static List<Transform> GetChildsAsList(this Transform t)
        {
            List<Transform> result = new List<Transform>();
            for (int i = 0; i < t.childCount; i++)
            {
                result.Add(t.GetChild(i));
            }
            return result;
        }

        public static void DestroyChilds(this Transform t)
        {
            if (t.childCount == 0)
                return;

            Transform[] childs = t.GetChildsAsArray();
            foreach (Transform trans in childs)
                GameObject.Destroy(trans.gameObject);
        }

        public static bool IsInView(this Transform self, Transform target, float fov)
        {
            bool result = false;

            float angle = Vector3.Angle(self.forward, (target.position - self.position).ReplaceY(0).normalized);
            result = angle <= fov * .5f ? true : false;

            return result;
        }

        public static Transform Instantiate(this Transform prefab, Vector3 position, Quaternion rotation)
        {
            return Object.Instantiate(prefab, position, rotation);
        }

        public static Transform Instantiate(this Transform prefab)
        {
            return Object.Instantiate(prefab);
        }

        public static Transform Instantiate(this Transform prefab, Vector3 position)
        {
            return Object.Instantiate(prefab, position, prefab.rotation);
        }

        public static Transform Instantiate(this Transform prefab, Quaternion rotation)
        {
            return Object.Instantiate(prefab, Vector3.zero, rotation);
        }

        public static Transform GetRootParent(this Transform t)
        {
            if (t == null)
                return null;

            Transform parent = t;
            while (parent.parent != null)
                parent = parent.parent;
            return parent;
        }
        #endregion

        #region Physics
        public static Vector3 CalculateVelocity(Vector3 targetPosition, Vector3 startPosition, float time)
        {
            Vector3 distance = targetPosition - startPosition;
            Vector3 distanceXZ = distance;
            distanceXZ.y = 0;

            //Calculate the total x and y distances of the projectile
            float Sy = distance.y;
            float Sxz = distanceXZ.magnitude;

            //Calculating Initial Velocity
            float Vxz = Sxz / time;
            float Vy = Sy / time + 0.5f * Mathf.Abs(Physics.gravity.y) * time;

            Vector3 result = distanceXZ.normalized;
            result *= Vxz;
            result.y = Vy;

            return result;
        }

        public static Vector3 CalculateVelocity(this Transform self, Vector3 targetPosition, float time)
        {
            return CalculateVelocity(targetPosition, self.position, time);
        }

        public static Vector3 CalculateVelocity(this Transform self, Transform target, float time)
        {
            return CalculateVelocity(target.position, self.position, time);
        }

        public static void ShowTrajectory(Vector3 finalVelocity, Vector3 startPoint, float throwingTime, LineRenderer ProjectionLine)
        {
            int maxIterations = Mathf.RoundToInt(throwingTime / Time.fixedDeltaTime);
            ProjectionLine.positionCount = maxIterations;
            Vector3 pos = startPoint;
            Vector3 vel = finalVelocity;

            float elapsedTime = 0.0f;

            for (int i = 0; i < maxIterations; i++)
            {
                vel = vel + (new Vector3(0, Physics.gravity.y, 0) * Time.fixedDeltaTime);
                pos += vel * Time.fixedDeltaTime;
                elapsedTime += Time.fixedDeltaTime;
                ProjectionLine.SetPosition(i, pos);
            }
        }

        public static Vector3 CalcVelocityAndShowTrajectory(Vector3 targetPosition, Vector3 startPosition, float time, LineRenderer lineRenderer)
        {
            Vector3 v = CalculateVelocity(targetPosition, startPosition, time);
            ShowTrajectory(v, startPosition, time, lineRenderer);
            return v;
        }
        #endregion

        #region RigidBody
        public static void ChangeDirection(this Rigidbody rigidbody, Vector3 direction)
        {
            rigidbody.velocity = direction * rigidbody.velocity.magnitude;
        }

        public static void SetVelocityIgnoringY(this Rigidbody rigidbody, Vector3 velocity)
        {
            rigidbody.velocity = Vector3.up * rigidbody.velocity.y + velocity;
        }
        #endregion

        #region Camera
        public static bool IsInCameraView(this Vector3 position, Camera cam = null)
        {
            Camera c = cam == null ? Camera.main : cam;
            Vector2 screenPos = c.WorldToScreenPoint(position);
            bool result = true;
            if (screenPos.x > Screen.width || screenPos.x < 0 || screenPos.y > Screen.height || screenPos.y < 0)
                result = false;

            return result;
        }

        public static bool IsInCameraView(this Transform t, Camera cam = null)
        {
            return IsInCameraView(t.position, cam);
        }

        public static bool IsInCameraView(this MeshFilter meshFilter, Camera cam = null)
        {
            Camera c = cam == null ? Camera.main : cam;
            Vector3 position = meshFilter.transform.position;
            Transform transform = meshFilter.transform;
            Mesh mesh = meshFilter.mesh;
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 pos = transform.TransformPoint(v);
                Vector2 screenPos = cam.WorldToScreenPoint(pos);

                if (screenPos.x > 0 && screenPos.x < Screen.width)
                    if (screenPos.y > 0 && screenPos.y < Screen.height)
                        return true;
            }
            return false;
        }

        public static bool IsRayHitting(this Collider col, Camera cam = null)
        {
            Camera c = cam == null ? Camera.main : cam;
            Transform transform = col.transform;
            if (Physics.Raycast(cam.ScreenPointToRay(cam.WorldToScreenPoint(transform.position)), out RaycastHit hit, Mathf.Infinity))
            {
                if (hit.collider == col)
                    return true;
                else
                    return false;
            }
            return false;
        }
        #endregion

        #region Color
        public static Color ChangeAlpha(this Color c, float alpha)
        {
            alpha = Mathf.Clamp(alpha, 0, 1);
            return new Color(c.r, c.g, c.b, alpha);
        }

        public static Color TintColor(this Color c, Color tintColor, bool changeAlpha = false)
        {
            float a = changeAlpha ? tintColor.a : 1;
            return new Color(c.r * tintColor.r, c.g * tintColor.g, c.b * tintColor.b, c.a * a);
        }
        #endregion

        #region Touch
        public static bool Began(this Touch touch)
        {
            if (touch.phase == TouchPhase.Began)
                return true;
            else
                return false;
        }

        public static bool Hold(this Touch touch)
        {
            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                return true;
            else
                return false;
        }

        public static bool Ended(this Touch touch)
        {
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                return true;
            else
                return false;
        }
        #endregion
    }
}

public static class Methods
{
    public static void ClearLogConsole()
    {
#if UNITY_EDITOR
        //Debug.Log("################# DISABLED BECAUSE OF BUILD!");
        Assembly assembly = Assembly.GetAssembly(typeof(UnityEditor.SceneView));
        System.Type logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
        MethodInfo clearConsoleMethod = logEntries.GetMethod("Clear");
        clearConsoleMethod.Invoke(new object(), null);
#endif
    }

    //Default values for up direction is zero, +90 offset sets vector3.right as 0
    public static float GetAngleFromVectorXY(Vector3 dir, DIRECTION direction = DIRECTION.Up)
    {
        float offset = 0;
        switch(direction)
        {
            case DIRECTION.Right:
                offset = 0;
                break;
            case DIRECTION.Left:
                offset = 180;
                break;
            case DIRECTION.Forward:
                offset = 270;
                break;
            case DIRECTION.Back:
                offset = 90;
                break;
            case DIRECTION.Up:
                offset = 270;
                break;
            case DIRECTION.Down:
                offset = 90;
                break;
        }
        dir = dir.normalized;
        float n = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + offset;
        if (n < 0) n += 360;
        n %= 360;
        return n;
    }

    public static float GetAngleFromVectorXZ(Vector3 dir, DIRECTION direction = DIRECTION.Forward)
    {
        float offset = 0;
        switch (direction)
        {
            case DIRECTION.Right:
                offset = 0;
                break;
            case DIRECTION.Left:
                offset = 180;
                break;
            case DIRECTION.Forward:
                offset = 270;
                break;
            case DIRECTION.Back:
                offset = 90;
                break;
            case DIRECTION.Up:
                offset = 270;
                break;
            case DIRECTION.Down:
                offset = 90;
                break;
        }
        dir = dir.normalized;
        float n = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg + offset;
        if (n < 0) n += 360;
        n %= 360;
        return n;
    }

    public static Vector3 GetVectorFromAngle(float angle, DIRECTION direction = DIRECTION.Up)
    {
        // angle = 0 -> 360
        float offset = 0;
        switch(direction)
        {
            case DIRECTION.Right:
                offset = 0;
                break;
            case DIRECTION.Left:
                offset = 180;
                break;
            case DIRECTION.Down:
                offset = 270;
                break;
            default:
                offset = 90;
                break;
        }
        angle = (angle + offset) % 360;
        float angleRad = angle * (Mathf.PI / 180f);
        return new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
    }

    public static Vector3 GetVectorFromAngleXZ(float angle, DIRECTION direction = DIRECTION.Forward)
    {
        // angle = 0 -> 360
        float offset = 0;
        switch (direction)
        {
            case DIRECTION.Right:
                offset = 0;
                break;
            case DIRECTION.Left:
                offset = 180;
                break;
            case DIRECTION.Back:
                offset = 270;
                break;
            default:
                offset = 90;
                break;
        }
        angle = (angle + offset) % 360;
        float angleRad = angle * (Mathf.PI / 180f);
        return new Vector3(Mathf.Cos(angleRad),0, Mathf.Sin(angleRad));
    }

    public static float ReMap(float value, float inMin, float inMax, float outMin, float outMax)
    {
        return (value - inMin) / (outMin - inMin) * (outMax - inMax) + inMax;
    }

    public static float QuadLerp(float a, float b, float t) => ((b - a) * t * t) + a;

    public static Vector3 QuadLerp(Vector3 a, Vector3 b, float t)
    {
        float x = QuadLerp(a.x, b.x, t);
        float y = QuadLerp(a.y, b.y, t);
        float z = QuadLerp(a.z, b.z, t);
        return new Vector3(x, y, z);
    }

    public static T[] RemoveDuplicates<T>(T[] arr)
    {
        List<T> list = new List<T>();
        foreach (T t in arr)
        {
            if (!list.Contains(t))
            {
                list.Add(t);
            }
        }
        return list.ToArray();
    }

    public static List<T> RemoveDuplicates<T>(List<T> arr)
    {
        List<T> list = new List<T>();
        foreach (T t in arr)
        {
            if (!list.Contains(t))
            {
                list.Add(t);
            }
        }
        return list;
    }

    public static Vector3 GetRandomPositionWithinRectangle(float xMin, float xMax, float yMin, float yMax)
    {
        return new Vector3(UnityEngine.Random.Range(xMin, xMax), UnityEngine.Random.Range(yMin, yMax));
    }

    public static Vector3 GetRandomPositionWithinRectangle(Vector3 lowerLeft, Vector3 upperRight)
    {
        return new Vector3(UnityEngine.Random.Range(lowerLeft.x, upperRight.x), UnityEngine.Random.Range(lowerLeft.y, upperRight.y));
    }

    public static Vector3 GetRandomPositionWithinRectangle(Vector2 lowerLeft, Vector2 upperRight)
    {
        return new Vector3(UnityEngine.Random.Range(lowerLeft.x, upperRight.x), UnityEngine.Random.Range(lowerLeft.y, upperRight.y));
    }

    // Get a random male name and optionally single letter surname
    public static string GetRandomName(bool withSurname = false)
    {
        List<string> firstNameList = new List<string>(){"Gabe","Cliff","Tim","Ron","Jon","John","Mike","Seth","Alex","Steve","Chris","Will","Bill","James","Jim",
                                        "Ahmed","Omar","Peter","Pierre","George","Lewis","Lewie","Adam","William","Ali","Eddie","Ed","Dick","Robert","Bob","Rob",
                                        "Neil","Tyson","Carl","Chris","Christopher","Jensen","Gordon","Morgan","Richard","Wen","Wei","Luke","Lucas","Noah","Ivan","Yusuf",
                                        "Ezio","Connor","Milan","Nathan","Victor","Harry","Ben","Charles","Charlie","Jack","Leo","Leonardo","Dylan","Steven","Jeff",
                                        "Alex","Mark","Leon","Oliver","Danny","Liam","Joe","Tom","Thomas","Bruce","Clark","Tyler","Jared","Brad","Jason"};

        if (!withSurname)
        {
            return firstNameList[UnityEngine.Random.Range(0, firstNameList.Count)];
        }
        else
        {
            string alphabet = "ABCDEFGHIJKLMNOPQRSTUVXYWZ";
            return firstNameList[UnityEngine.Random.Range(0, firstNameList.Count)] + " " + alphabet[UnityEngine.Random.Range(0, alphabet.Length)] + ".";
        }
    }

    public static string GetRandomCityName()
    {
        List<string> cityNameList = new List<string>(){"Alabama","New York","Old York","Bangkok","Lisbon","Vee","Agen","Agon","Ardok","Arbok",
                            "Kobra","House","Noun","Hayar","Salma","Chancellor","Dascomb","Payn","Inglo","Lorr","Ringu",
                            "Brot","Mount Loom","Kip","Chicago","Madrid","London","Gam",
                            "Greenvile","Franklin","Clinton","Springfield","Salem","Fairview","Fairfax","Washington","Madison",
                            "Georgetown","Arlington","Marion","Oxford","Harvard","Valley","Ashland","Burlington","Manchester","Clayton",
                            "Milton","Auburn","Dayton","Lexington","Milford","Riverside","Cleveland","Dover","Hudson","Kingston","Mount Vernon",
                            "Newport","Oakland","Centerville","Winchester","Rotary","Bailey","Saint Mary","Three Waters","Veritas","Chaos","Center",
                            "Millbury","Stockland","Deerstead Hills","Plaintown","Fairchester","Milaire View","Bradton","Glenfield","Kirkmore",
                            "Fortdell","Sharonford","Inglewood","Englecamp","Harrisvania","Bosstead","Brookopolis","Metropolis","Colewood","Willowbury",
                            "Hearthdale","Weelworth","Donnelsfield","Greenline","Greenwich","Clarkswich","Bridgeworth","Normont",
                            "Lynchbrook","Ashbridge","Garfort","Wolfpain","Waterstead","Glenburgh","Fortcroft","Kingsbank","Adamstead","Mistead",
                            "Old Crossing","Crossing","New Agon","New Agen","Old Agon","New Valley","Old Valley","New Kingsbank","Old Kingsbank",
            "New Dover","Old Dover","New Burlington","Shawshank","Old Shawshank","New Shawshank","New Bradton", "Old Bradton","New Metropolis","Old Clayton","New Clayton"
        };
        return cityNameList[UnityEngine.Random.Range(0, cityNameList.Count)];
    }
}

public static class ColorHex
{
    public static readonly string
        Aqua = "#00ffffff", Black = "#000000ff", Brown = "#a52a2aff",
        DarkBlue = "#0000a0ff", Magenta = "#ff00ffff", Green = "#008000ff",
        Grey = "#808080ff", LightBlue = "#add8e6ff", Lime = "#00ff00ff",
        Maroon = "#800000ff", Navy = "#000080ff", Olive = "#808000ff",
        Orange = "#ffa500ff", Purple = "#800080ff", Red = "#ff0000ff",
        Silver = "#c0c0c0ff", Teal = "#008080ff", White = "#ffffffff",
        Yellow = "#ffff00ff";

    public static string ToHtmlColorString(this string s, ColorsEnum colorCode = ColorsEnum.White)
    {
        switch(colorCode)
        {
            case ColorsEnum.Aqua:
                return "<color=" + Aqua + ">" + s + "</color>";
            case ColorsEnum.Black:
                return "<color=" + Black + ">" + s + "</color>";
            case ColorsEnum.Brown:
                return "<color=" + Brown + ">" + s + "</color>";
            case ColorsEnum.DarkBlue:
                return "<color=" + DarkBlue + ">" + s + "</color>";
            case ColorsEnum.Magenta:
                return "<color=" + Magenta + ">" + s + "</color>";
            case ColorsEnum.Green:
                return "<color=" + Green + ">" + s + "</color>";
            case ColorsEnum.Grey:
                return "<color=" + Grey + ">" + s + "</color>";
            case ColorsEnum.LightBlue:
                return "<color=" + LightBlue + ">" + s + "</color>";
            case ColorsEnum.Lime:
                return "<color=" + Lime + ">" + s + "</color>";
            case ColorsEnum.Maroon:
                return "<color=" + Maroon + ">" + s + "</color>";
            case ColorsEnum.Navy:
                return "<color=" + Navy + ">" + s + "</color>";
            case ColorsEnum.Olive:
                return "<color=" + Olive + ">" + s + "</color>";
            case ColorsEnum.Orange:
                return "<color=" + Orange + ">" + s + "</color>";
            case ColorsEnum.Purple:
                return "<color=" + Purple + ">" + s + "</color>";
            case ColorsEnum.Red:
                return "<color=" + Red + ">" + s + "</color>";
            case ColorsEnum.Silver:
                return "<color=" + Silver + ">" + s + "</color>";
            case ColorsEnum.Teal:
                return "<color=" + Teal + ">" + s + "</color>";
            case ColorsEnum.White:
                return "<color=" + White + ">" + s + "</color>";
            case ColorsEnum.Yellow:
                return "<color=" + Yellow + ">" + s + "</color>";
            default:
                return "<color=" + White + ">" + s + "</color>";
        }
    }

    public static string ToHtmlColorString(this string s, string colorCode = "FFFFFF")
    {
        return "<color=#" + colorCode + ">" + s + "</color>";
    }

    public static string ToHtmlColorString(this int i, ColorsEnum colorCode) => i.ToString().ToHtmlColorString(colorCode);

    public static string ToHtmlColorString(this int i, string colorCode) => i.ToString().ToHtmlColorString(colorCode);

    public static string ToHtmlColorString(this float value, ColorsEnum colorCode) => value.ToString().ToHtmlColorString(colorCode);

    public static string ToHtmlColorString(this float value, string colorCode) => value.ToString().ToHtmlColorString(colorCode);
}

public enum ColorsEnum
{
    Aqua, Black, Brown, DarkBlue, Magenta, Green, Grey, LightBlue, Lime,
    Maroon, Navy, Olive, Orange, Purple, Red, Silver, Teal, White, Yellow
}

public static class Variance
{
    public static bool Roll2 => Roll(2);
    public static bool Roll3 => Roll(3);
    public static bool Roll4 => Roll(4);
    public static bool Roll5 => Roll(5);
    public static bool Roll6 => Roll(6);
    public static bool Roll7 => Roll(7);
    public static bool Roll8 => Roll(8);
    public static bool Roll9 => Roll(9);
    public static bool Roll10 => Roll(10);

    public static bool ChanceOf10 => Chance(10);
    public static bool ChanceOf20 => Chance(20);
    public static bool ChanceOf30 => Chance(30);
    public static bool ChanceOf40 => Chance(40);
    public static bool ChanceOf50 => Chance(50);
    public static bool ChanceOf60 => Chance(60);
    public static bool ChanceOf70 => Chance(70);
    public static bool ChanceOf80 => Chance(80);
    public static bool ChanceOf90 => Chance(90);

    public static bool Roll(int i)
    {
        return Random.Range(0, i) == 0;
    }

    public static bool Chance(float chance)
    {
        if (chance > 100)
            return true;
        else
        {
            int c = Mathf.RoundToInt(chance);
            return Random.Range(1, 101) < c;
        }
    }
}

public enum DIRECTION
{
    Up,
    Down,
    Right,
    Left,
    Forward,
    Back
}
