using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public const string LoadingSceneName = "LoadingScene";

    public static string NextSceneName { get; private set; }
    public static bool IsNetworkTransition { get; private set; }

    public void LoadScene(string sceneName)
    {
        Load(sceneName);
    }

    public static void Load(string sceneName)
    {
        if (!TrySetRequest(sceneName, false))
            return;

        SceneManager.LoadScene(LoadingSceneName, LoadSceneMode.Single);
    }

    public static SceneEventProgressStatus LoadNetworkScene(string sceneName)
    {
        if (!TrySetRequest(sceneName, true))
            return SceneEventProgressStatus.InvalidSceneName;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.IsServer ||
            networkManager.SceneManager == null)
        {
            Debug.LogWarning("[SceneLoader] Network scene loading requires an active server.");
            ClearRequest();
            return SceneEventProgressStatus.InternalNetcodeError;
        }

        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(
            LoadingSceneName,
            LoadSceneMode.Single);

        if (status != SceneEventProgressStatus.Started &&
            status != SceneEventProgressStatus.SceneEventInProgress)
        {
            ClearRequest();
        }

        return status;
    }

    public static void ClearRequest()
    {
        NextSceneName = string.Empty;
        IsNetworkTransition = false;
    }

    private static bool TrySetRequest(string sceneName, bool networkTransition)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneLoader] The destination scene is not set.");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[SceneLoader] The destination scene is not in Build Settings: " + sceneName);
            return false;
        }

        NextSceneName = sceneName;
        IsNetworkTransition = networkTransition;
        return true;
    }
}
