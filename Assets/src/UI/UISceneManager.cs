using UnityEngine;
using UnityEngine.SceneManagement;

public class UISceneManager : MonoBehaviour
{
    void Start()
    {
        Debug.Log("game UI is mounting");
        if (!SceneManager.GetSceneByName("GameUI").isLoaded)
        {
            SceneManager.LoadSceneAsync("GameUI", LoadSceneMode.Additive).completed += (op) =>
            {
                Debug.Log("game UI was mounted");
            };
        }
    }
}
