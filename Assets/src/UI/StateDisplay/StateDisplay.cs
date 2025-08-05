using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class StateDisplay : MonoBehaviour
{
    [SerializeField] Text displayLabel;

    public void SetText(string text)
    {
        if (displayLabel != null)
            displayLabel.text = text;
    }
}