using UnityEngine;
using CustomExtensions;

public class Weapon : MonoBehaviour
{
    [SerializeField] float rotSpeed = 10f;

    float temp = 0;

    private void Update()
    {
        temp += Time.deltaTime * rotSpeed * 100;
        transform.localRotation = Quaternion.Euler(transform.localEulerAngles.ReplaceY(temp));
    }
}
