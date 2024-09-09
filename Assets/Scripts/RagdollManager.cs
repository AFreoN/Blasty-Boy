using UnityEngine;

public class RagdollManager : MonoBehaviour
{
    public Rigidbody[] BodyParts;
    Rigidbody MainBody;
    Animator anim;

    void Awake()
    {
        anim = GetComponent<Animator>();
        MainBody = GetComponent<Rigidbody>();

        Startcall();
    }

    public void ActivateRagdoll()
    {
        MainBody.isKinematic = true;
        MainBody.detectCollisions = false;

        anim.enabled = false;
        for (int i=0; i < BodyParts.Length; i++)
        {
            BodyParts[i].isKinematic = false;
            BodyParts[i].detectCollisions = true;
        }
    }

    public void DeactivateRagdoll()
    {
        anim.enabled = true;
        MainBody.isKinematic = false;
        MainBody.detectCollisions = true;

        for (int i = 0; i < BodyParts.Length; i++)
        {
            BodyParts[i].isKinematic = true;
            BodyParts[i].detectCollisions = true;
        }
    }

    void Startcall()
    {
        anim.enabled = true;
        MainBody.isKinematic = false;
        MainBody.detectCollisions = true;
        for (int i =0; i < BodyParts.Length; i++)
        {
            BodyParts[i].isKinematic = true;
            BodyParts[i].detectCollisions = true;
        }
    }
}
