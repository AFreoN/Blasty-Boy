using UnityEngine;

public class TagsLayers : MonoBehaviour
{
    public const string playerTag = "Player";
    public const string enemyTag = "Enemy";
    public const string prisonerTag = "Prisoner";
    public const string bodyPartsTag = "BodyParts";
    public const string groundTag = "Ground";
    public const string targetBoardTag = "GameController";
    public const string finishLineTag = "Finish";
    public const string defaultTag = "Untagged";
    public const string pointerTag = "Pointer";
    public const string objectTag = "Object";

    public static readonly LayerMask playerLayer = LayerMask.GetMask("Player");
    public static readonly LayerMask groundLayer = LayerMask.GetMask(LayerMask.LayerToName(1));
    public static readonly LayerMask bodyLayer = LayerMask.GetMask("BodyParts");
}
