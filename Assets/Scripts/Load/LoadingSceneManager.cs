using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneManager : MonoBehaviour
{
    private const float MinimumRequiredDisplaySeconds = 2f;

    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text loadCountText;
    [SerializeField] private TMP_Text[] playerNameTexts;
    [SerializeField] private Transform topicTextureRoot;
    [SerializeField, Min(MinimumRequiredDisplaySeconds)]
    private float minimumDisplaySeconds = MinimumRequiredDisplaySeconds;
    [SerializeField] private float networkRetryTimeoutSeconds = 10f;
    [SerializeField] private string disconnectReturnSceneName = "TitleScene";
    [SerializeField, Min(0f)] private float disconnectMessageSeconds = 1.5f;

    private void Start()
    {
        ResolveSceneReferences();
        SelectRandomTopicTexture();
        RefreshPlayerNames();
        StartCoroutine(LoadNextScene());
    }

    private IEnumerator LoadNextScene()
    {
        if (ShouldWaitForNetworkHost())
        {
            yield return WaitForNetworkHost();
            yield break;
        }

        string sceneName = SceneLoader.NextSceneName;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            SetStatus("移動先のシーンが設定されていません");
            Debug.LogWarning("[LoadingSceneManager] The destination scene is not set.");
            yield break;
        }

        if (SceneLoader.IsNetworkTransition)
        {
            yield return LoadNetworkScene(sceneName);
            yield break;
        }

        yield return LoadLocalScene(sceneName);
    }

    private IEnumerator LoadLocalScene(string sceneName)
    {
        float requiredDisplaySeconds = Mathf.Max(
            MinimumRequiredDisplaySeconds,
            minimumDisplaySeconds);
        float shownTime = 0f;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            SetStatus("シーンの読み込みを開始できませんでした");
            yield break;
        }

        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            shownTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            UpdateProgress(progress);

            if (operation.progress >= 0.9f && shownTime >= requiredDisplaySeconds)
            {
                UpdateProgress(1f);
                SceneLoader.ClearRequest();
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    private IEnumerator LoadNetworkScene(string sceneName)
    {
        float requiredDisplaySeconds = Mathf.Max(
            MinimumRequiredDisplaySeconds,
            minimumDisplaySeconds);
        float shownTime = 0f;
        while (shownTime < requiredDisplaySeconds)
        {
            shownTime += Time.unscaledDeltaTime;
            UpdateWaitingStatus();
            yield return null;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.IsServer ||
            networkManager.SceneManager == null)
        {
            SetStatus("オンライン接続が切断されました");
            SceneLoader.ClearRequest();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed <= networkRetryTimeoutSeconds)
        {
            SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(
                sceneName,
                LoadSceneMode.Single);

            if (status == SceneEventProgressStatus.Started)
            {
                UpdateProgress(1f);
                SceneLoader.ClearRequest();
                yield break;
            }

            if (status != SceneEventProgressStatus.SceneEventInProgress)
            {
                SetStatus("オンラインシーンの読み込みに失敗しました");
                Debug.LogWarning("[LoadingSceneManager] Network scene load failed: " + status);
                SceneLoader.ClearRequest();
                yield break;
            }

            UpdateWaitingStatus();
            yield return new WaitForSecondsRealtime(0.25f);
            elapsed += 0.25f;
        }

        SetStatus("オンラインシーンの読み込みがタイムアウトしました");
        Debug.LogWarning("[LoadingSceneManager] Network scene load timed out.");
        SceneLoader.ClearRequest();
    }

    private IEnumerator WaitForNetworkHost()
    {
        while (NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening &&
               NetworkManager.Singleton.IsConnectedClient &&
               SceneManager.GetActiveScene().name == SceneLoader.LoadingSceneName)
        {
            UpdateWaitingStatus();
            yield return null;
        }

        if (SceneManager.GetActiveScene().name != SceneLoader.LoadingSceneName)
            yield break;

        SetStatus("ホストとの接続が切断されました");
        SceneLoader.ClearRequest();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        float elapsed = 0f;
        while (elapsed < disconnectMessageSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!string.IsNullOrWhiteSpace(disconnectReturnSceneName) &&
            Application.CanStreamedLevelBeLoaded(disconnectReturnSceneName))
        {
            SceneManager.LoadScene(disconnectReturnSceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[LoadingSceneManager] Disconnect return scene is not available: " +
                             disconnectReturnSceneName);
        }
    }

    private bool ShouldWaitForNetworkHost()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null &&
               networkManager.IsListening &&
               networkManager.IsClient &&
               !networkManager.IsServer;
    }

    private void ResolveSceneReferences()
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        if (topicTextureRoot == null)
            topicTextureRoot = FindTopicTextureRoot();

        List<TMP_Text> playerLabels = new List<TMP_Text>(4);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            if (loadCountText == null && text.text == "ロードカウント")
                loadCountText = text;

            if (text.text == "プレイヤー名")
                playerLabels.Add(text);
        }

        if (playerNameTexts == null || playerNameTexts.Length == 0)
        {
            playerLabels.Sort((left, right) =>
                right.rectTransform.anchoredPosition.y.CompareTo(left.rectTransform.anchoredPosition.y));
            playerNameTexts = playerLabels.ToArray();
        }
    }

    private Transform FindTopicTextureRoot()
    {
        Transform[] transforms =
            FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == "TopicTexture")
                return candidate;
        }

        return null;
    }

    private void SelectRandomTopicTexture()
    {
        if (topicTextureRoot == null)
            return;

        Image[] topicImages = topicTextureRoot.GetComponentsInChildren<Image>(true);
        if (topicImages == null || topicImages.Length == 0)
            return;

        int startIndex = Random.Range(0, topicImages.Length);
        Image selectedImage = null;
        for (int i = 0; i < topicImages.Length; i++)
        {
            Image candidate = topicImages[(startIndex + i) % topicImages.Length];
            if (candidate != null && candidate.sprite != null)
            {
                selectedImage = candidate;
                break;
            }
        }

        if (selectedImage == null)
            return;

        for (int i = 0; i < topicImages.Length; i++)
        {
            Image image = topicImages[i];
            if (image == null)
                continue;

            image.gameObject.SetActive(image == selectedImage);
        }
    }

    private void RefreshPlayerNames()
    {
        if (playerNameTexts == null)
            return;

        Lobby lobby = MyGame.LobbyManager.Instance != null
            ? MyGame.LobbyManager.Instance.CurrentLobby
            : null;

        for (int i = 0; i < playerNameTexts.Length; i++)
        {
            if (playerNameTexts[i] == null)
                continue;

            string playerName = string.Empty;
            if (lobby != null && lobby.Players != null && i < lobby.Players.Count)
            {
                Player player = lobby.Players[i];
                if (player.Data != null &&
                    player.Data.TryGetValue(MyGame.LobbyManager.PlayerNameKey, out PlayerDataObject nameData))
                {
                    playerName = nameData.Value;
                }
            }
            else if (lobby == null)
            {
                playerName = i == 0 ? "P1" : "CPU " + i;
            }

            playerNameTexts[i].text = string.IsNullOrWhiteSpace(playerName)
                ? "---"
                : playerName;
        }
    }

    private void UpdateProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (progressBar != null)
            progressBar.value = progress;

        SetStatus($"読み込み中 {Mathf.RoundToInt(progress * 100f)}%");
        UpdateLoadCount();
    }

    private void UpdateWaitingStatus()
    {
        int dotCount = 1 + Mathf.FloorToInt(Time.unscaledTime * 2f) % 3;
        string nextSceneName = SceneLoader.NextSceneName;
        string message = nextSceneName == "ResultScene"
            ? "リザルト集計中"
            : nextSceneName == "BattleScene"
                ? "戦闘準備中"
                : "読み込み中";
        SetStatus(message + new string('.', dotCount));
        UpdateLoadCount();
    }

    private void UpdateLoadCount()
    {
        if (loadCountText == null)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            int connectedCount = networkManager.ConnectedClientsIds.Count;
            int expectedCount = connectedCount;

            Lobby lobby = MyGame.LobbyManager.Instance != null
                ? MyGame.LobbyManager.Instance.CurrentLobby
                : null;
            if (lobby != null && lobby.Players != null)
                expectedCount = Mathf.Max(expectedCount, lobby.Players.Count);

            loadCountText.text = $"{connectedCount} / {Mathf.Max(1, expectedCount)}";
            return;
        }

        loadCountText.text = string.Empty;
    }

    private void SetStatus(string message)
    {
        if (progressText != null)
            progressText.text = message;
    }
}
