using UnityEngine;
using UnityEngine.UI;

public class CrosshairSetter : MonoBehaviour
{
    public Image crosshairImage;
    public Sprite crosshairSprite;

    void Start()
    {
        crosshairImage.sprite = crosshairSprite;
    }
}
