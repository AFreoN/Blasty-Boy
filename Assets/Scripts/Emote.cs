using UnityEngine;
using UnityEngine.UI;

public class Emote : MonoBehaviour
{
    const string emoteCanvasName = "Emote_Canvas";
    public static Emote Create(EMOTETYPE type, Transform _target, Vector3 _Offset, float _duration)
    {
        GameObject prefab = PrefabManager.emotes.EmoteCanvas;
        GameObject g = Instantiate(prefab);
        Emote ec = g.AddComponent<Emote>();
        ec.Initialize(type,_target, _Offset, _duration);

        return ec;
    }

    Transform mainCamera = null;
    float duration = 0;
    float tempTimer = 0;

    Transform target = null;
    Vector3 offset = Vector3.zero;
    bool initialized = false;

    Image emoteImg = null;
    const string loveImg = "Smiley_LovedSmile";
    const string smilingImg = "Smiley_SmilingFace";
    const string smileEyeClosedImg = "Smiley_SmileEyeClosed";
    const string roflImg = "Smiley_Rofl";
    const string verySadImg = "Smiley_VerySad";
    const string yummyImg = "Smiley_Yummy";
    const string kissingImg = "Smiley_Kissing";
    const string angryImg = "Smiley_Angry";
    const string cryOneEyeImg = "Smiley_CryOneEye";
    const string heavenImg = "Smiley_Heaven";
    const string thugLifeImg = "Smiley_ThugLife";

    #region OldGetSprite
    /*
    public static Sprite GetEmoteSprite(EMOTETYPE type)
    {
        Sprite emoteSprite = null;
        switch (type)
        {
            case EMOTETYPE.Smiling:
                emoteSprite = Resources.Load<Sprite>(smilingImg);
                break;

            case EMOTETYPE.SmileEyeClosed:
                emoteSprite = Resources.Load<Sprite>(smileEyeClosedImg);
                break;

            case EMOTETYPE.Rofl:
                emoteSprite = Resources.Load<Sprite>(roflImg);
                break;

            case EMOTETYPE.Love:
                emoteSprite = Resources.Load<Sprite>(loveImg);
                break;

            case EMOTETYPE.Sad:
                emoteSprite = Resources.Load<Sprite>(verySadImg);
                break;

            case EMOTETYPE.Yummy:
                emoteSprite = Resources.Load<Sprite>(yummyImg);
                break;

            case EMOTETYPE.Kissing:
                emoteSprite = Resources.Load<Sprite>(kissingImg);
                break;

            case EMOTETYPE.Angry:
                emoteSprite = Resources.Load<Sprite>(angryImg);
                break;

            case EMOTETYPE.CryOneEye:
                emoteSprite = Resources.Load<Sprite>(cryOneEyeImg);
                break;

            case EMOTETYPE.Heaven:
                emoteSprite = Resources.Load<Sprite>(heavenImg);
                break;

            case EMOTETYPE.ThugLife:
                emoteSprite = Resources.Load<Sprite>(thugLifeImg);
                break;

            default:
                emoteSprite = Resources.Load<Sprite>(smilingImg);
                break;
        }
        return emoteSprite;
    }
    */
    #endregion

    public static Sprite GetEmoteSprite(EMOTETYPE type)
    {
        Sprite result = null;
        switch(type)
        {
            case EMOTETYPE.Smiling:
                result = PrefabManager.emotes.Smiling;
                break;
            case EMOTETYPE.SmileEyeClosed:
                result = PrefabManager.emotes.SmilingEyeClosed;
                break;
            case EMOTETYPE.Rofl:
                result = PrefabManager.emotes.Rofl;
                break;
            case EMOTETYPE.Love:
                result = PrefabManager.emotes.Love;
                break;
            case EMOTETYPE.Sad:
                result = PrefabManager.emotes.Sad;
                break;
            case EMOTETYPE.Kissing:
                result = PrefabManager.emotes.Kissing;
                break;
            case EMOTETYPE.Angry:
                result = PrefabManager.emotes.Angry;
                break;
            case EMOTETYPE.Yummy:
                result = PrefabManager.emotes.Yummy;
                break;
            case EMOTETYPE.CryOneEye:
                result = PrefabManager.emotes.CryOneEye;
                break;
            case EMOTETYPE.Heaven:
                result = PrefabManager.emotes.Heaven;
                break;
            case EMOTETYPE.ThugLife:
                result = PrefabManager.emotes.ThugLife;
                break;
        }
        return result;
    }

    public void Initialize(EMOTETYPE type, Transform _target, Vector3 _offset, float _duration)
    {
        target = _target;
        offset = _offset;
        duration = _duration;

        mainCamera = CameraController.mainCamera.transform;
        transform.position = target.position + offset;
        emoteImg = transform.GetChild(0).GetComponent<Image>();

        Sprite emoteSprite = GetEmoteSprite(type);

        if (emoteSprite != null)
            emoteImg.sprite = emoteSprite;
        else
            Debug.LogError("No Image Found from Resources");

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        transform.forward = mainCamera.forward;
        transform.position = Vector3.Lerp(transform.position, target.position + offset, .3f);

        tempTimer += Time.unscaledDeltaTime;
        if (tempTimer >= duration)
            Destroy(gameObject);
    }

    public void Destroy()
    {
        Destroy(gameObject);
    }
}

public enum EMOTETYPE
{
    Smiling,
    SmileEyeClosed,
    Rofl,
    Love,
    Sad,
    Kissing,
    Angry,
    Yummy,
    CryOneEye,
    Heaven,
    ThugLife
}
