using UnityEngine;

public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance;
    public GameObject loadingPanel;

    void Awake()
    {
        Instance = this;
    }

    public void HideLoadingScreen()
    {
        Debug.Log("Hiding loading screen.");
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }
}