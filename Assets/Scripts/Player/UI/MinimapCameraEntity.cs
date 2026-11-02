using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    [RequireComponent(typeof(Camera))]
    public class MinimapCameraEntity : MonoBehaviour
    {
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RawImage minimapDisplay;
        [SerializeField] private float cameraHeight = 45f;
        [SerializeField] private float orthographicSize = 35f;
        [SerializeField] private int textureSize = 512;
        [SerializeField] private bool rotateWithPlayer;
        [SerializeField] private float followLerp = 18f;
        [SerializeField] private Color backgroundColor = new(0.04f, 0.06f, 0.08f, 1f);

        private Transform m_target;
        private RenderTexture m_runtimeTexture;

        private void Awake()
        {
            ConfigureCamera();
        }

        private void OnEnable()
        {
            ConfigureCamera();
        }

        private void OnDisable()
        {
            ReleaseRuntimeTexture();
        }

        private void LateUpdate()
        {
            if (m_target == null)
                FindTarget();

            if (m_target == null)
                return;

            Vector3 targetPosition = m_target.position + Vector3.up * cameraHeight;
            float lerpRate = 1f - Mathf.Exp(-followLerp * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, lerpRate);
            transform.rotation = Quaternion.Euler(90f, rotateWithPlayer ? m_target.eulerAngles.y : 0f, 0f);
        }

        private void ConfigureCamera()
        {
            if (minimapCamera == null)
                minimapCamera = GetComponent<Camera>();

            if (minimapCamera == null)
                return;

            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = backgroundColor;
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = orthographicSize;
            minimapCamera.depth = 20f;
            minimapCamera.rect = new Rect(0f, 0f, 1f, 1f);
            minimapCamera.useOcclusionCulling = false;
            minimapCamera.targetTexture = GetOrCreateRuntimeTexture();

            if (minimapDisplay != null)
                minimapDisplay.texture = minimapCamera.targetTexture;
        }

        private RenderTexture GetOrCreateRuntimeTexture()
        {
            int size = Mathf.Max(64, textureSize);
            if (m_runtimeTexture != null && m_runtimeTexture.width == size && m_runtimeTexture.height == size)
                return m_runtimeTexture;

            ReleaseRuntimeTexture();

            m_runtimeTexture = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
            {
                name = "MinimapRuntimeTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            m_runtimeTexture.Create();

            return m_runtimeTexture;
        }

        private void ReleaseRuntimeTexture()
        {
            if (m_runtimeTexture == null)
                return;

            if (minimapCamera != null && minimapCamera.targetTexture == m_runtimeTexture)
                minimapCamera.targetTexture = null;

            if (minimapDisplay != null && minimapDisplay.texture == m_runtimeTexture)
                minimapDisplay.texture = null;

            m_runtimeTexture.Release();
            Destroy(m_runtimeTexture);
            m_runtimeTexture = null;
        }

        private void FindTarget()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkObject localPlayerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (localPlayerObject != null)
                {
                    PlayerEntity localPlayer = localPlayerObject.GetComponent<PlayerEntity>();
                    if (localPlayer != null)
                    {
                        m_target = localPlayer.transform;
                        return;
                    }
                }
            }

            PlayerEntity[] players = FindObjectsByType<PlayerEntity>();
            for (int i = 0; i < players.Length; i++)
            {
                if (IsLocalOrSoloPlayer(players[i]))
                {
                    m_target = players[i].transform;
                    return;
                }
            }
        }

        private static bool IsLocalOrSoloPlayer(PlayerEntity player)
        {
            if (player == null)
                return false;

            NetworkObject netObj = player.GetComponent<NetworkObject>();
            return netObj == null || !netObj.IsSpawned || netObj.IsOwner;
        }
    }
}
