using TMPro;
using UnityEngine;

// Art for the runtime-built HUD. The builder fills it from the (locally installed, gitignored) Asset Store GUI kit;
// any slot left empty falls back to ProtoArt's generated sprites, so the prototype still runs from a fresh clone.
[CreateAssetMenu(menuName = "Prototype/UI Skin")]
public class ProtoUISkin : ScriptableObject
{
    [Header("Text")]
    public TMP_FontAsset font;
    public Material fontOutlineMaterial;

    [Header("Buttons & panels")]
    public Sprite buttonGreen;
    public Sprite buttonYellow;
    public Sprite buttonBlue;
    public Sprite buttonRed;
    public Sprite buttonPause;
    public Sprite panel;
    public Sprite pill;
    public Sprite toast;
    public Sprite dim;
    public Sprite glow;

    [Header("Banners & rewards")]
    public Sprite ribbonOrange;
    public Sprite ribbonGreen;
    public Sprite ribbonYellow;
    public Sprite starLarge;
    public Sprite starSmall;
    public Sprite tile;
    public Sprite textDecoLeft;
    public Sprite textDecoRight;

    [Header("Callouts")]
    public Sprite killDouble;
    public Sprite killTriple;
    public Sprite killQuadra;
    public Sprite killPenta;

    [Header("Bars")]
    public Sprite bossBarBack;
    public Sprite bossBarFill;
    public Sprite bossNameTag;
    public Sprite barFrame;
    public Sprite stageNode;
    public Sprite stageNodeActive;
    public Sprite stagePath;

    [Header("Icons")]
    public Sprite iconHeart;
    public Sprite iconTrophy;
    public Sprite iconCrown;
    public Sprite iconSword;
    public Sprite iconTarget;
    public Sprite iconBolt;
    public Sprite iconBomb;
    public Sprite iconBoss;
    public Sprite skillFrame;
    public Sprite skillCooldown;
    public Sprite targetReticle;
    public Sprite tutorialHand;
    public Sprite speechBubble;
    public Sprite speechArrow;

    public Sprite Kill(int n)
    {
        if (n >= 5) return killPenta;
        if (n == 4) return killQuadra;
        if (n == 3) return killTriple;
        if (n == 2) return killDouble;
        return null;
    }
}
