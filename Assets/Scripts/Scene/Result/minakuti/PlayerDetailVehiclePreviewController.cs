using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class PlayerDetailVehiclePreviewController : MonoBehaviour
    {
        [SerializeField] private RawImage previewImage;
        [SerializeField] private GameObject previewVehiclePrefab;
        [SerializeField] private float previewSpinSpeed = 32f;
        [SerializeField] private Vector3 previewRootPosition = new Vector3(0f, -200f, 0f);
        [SerializeField] private Vector3 previewCameraPosition = new Vector3(0.45f, 3.7f, -7.2f);
        [SerializeField] private Vector3 previewCameraEulerAngles = new Vector3(22f, 0f, 0f);
        [SerializeField] private float previewCameraSize = 4.6f;
        [SerializeField] private float previewVehicleVerticalOffset = -0.18f;
        [SerializeField] private float previewVehicleBaseScale = 1.45f;

        private Transform m_previewRoot;
        private GameObject m_previewWorld;
        private RenderTexture m_previewTexture;

        private void Start()
        {
            CreatePreview();
        }

        private void Update()
        {
            if (m_previewRoot == null || previewSpinSpeed <= 0f)
                return;

            m_previewRoot.Rotate(Vector3.up, previewSpinSpeed * Time.unscaledDeltaTime, Space.Self);
        }

        private void OnDestroy()
        {
            if (m_previewWorld != null)
            {
                Destroy(m_previewWorld);
                m_previewWorld = null;
            }

            if (m_previewTexture == null)
                return;

            m_previewTexture.Release();
            Destroy(m_previewTexture);
            m_previewTexture = null;
        }

        private void CreatePreview()
        {
            if (previewImage == null || previewVehiclePrefab == null)
                return;

            BattleResultEntry entry = ResolveSelectedEntry();

            m_previewWorld = new GameObject("PlayerDetailVehiclePreviewRoot");
            Transform worldRoot = m_previewWorld.transform;
            worldRoot.position = previewRootPosition;

            CreatePreviewLighting(worldRoot);

            GameObject slotObj = new GameObject("PlayerDetailVehiclePreviewSlot");
            m_previewRoot = slotObj.transform;
            m_previewRoot.SetParent(worldRoot, false);
            m_previewRoot.localPosition = Vector3.zero;
            m_previewRoot.localRotation = Quaternion.Euler(0f, 150f, 0f);
            m_previewRoot.localScale = Vector3.one * GetPreviewScale(entry.MachineIndex);

            GameObject vehicle = Instantiate(previewVehiclePrefab, m_previewRoot);
            vehicle.name = "PlayerDetailVehiclePreview";
            vehicle.transform.localPosition = new Vector3(0f, previewVehicleVerticalOffset, 0f);
            vehicle.transform.localRotation = Quaternion.identity;

            PlayerEntity playerEntity = vehicle.GetComponentInChildren<PlayerEntity>(true);
            if (playerEntity != null)
                playerEntity.ApplyMachineIndex(entry.MachineIndex);

            ResetPreviewVehiclePose(vehicle, playerEntity, previewVehicleVerticalOffset);
            PrepareVehicleForPreview(vehicle);
            CreatePreviewCamera(worldRoot);
        }

        private static BattleResultEntry ResolveSelectedEntry()
        {
            BattleResultEntry entry = PlayerDetailButtonController.SelectedEntry;
            if (PlayerDetailButtonController.HasSelectedEntry)
                return entry;

            if (!BattleResultData.HasResult)
                BattleResultData.TryRestoreSavedResults();

            if (BattleResultData.HasResult && BattleResultData.Entries.Count > 0)
                return BattleResultData.GetRankedEntries()[0];

            return default;
        }

        private float GetPreviewScale(int machineIndex)
        {
            MachineProfile profile = MachineProfileCatalog.GetProfile(machineIndex);
            float sizeCompensation = Mathf.Approximately(profile.SizeMultiplier, 0f)
                ? 1f
                : 1f / Mathf.Sqrt(profile.SizeMultiplier);

            return Mathf.Max(0.1f, previewVehicleBaseScale) * Mathf.Lerp(1f, sizeCompensation, 0.65f);
        }

        private void CreatePreviewCamera(Transform parent)
        {
            GameObject cameraObj = new GameObject("PlayerDetailVehiclePreviewCamera");
            cameraObj.transform.SetParent(parent, false);
            cameraObj.transform.localPosition = previewCameraPosition;
            cameraObj.transform.localRotation = Quaternion.Euler(previewCameraEulerAngles);

            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.25f, 0.32f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = previewCameraSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.allowHDR = true;

            m_previewTexture = new RenderTexture(1024, 576, 16, RenderTextureFormat.ARGB32)
            {
                name = "PlayerDetailVehiclePreviewTexture"
            };
            camera.targetTexture = m_previewTexture;
            previewImage.texture = m_previewTexture;
            previewImage.color = Color.white;
        }

        private static void CreatePreviewLighting(Transform parent)
        {
            GameObject keyLightObj = new GameObject("PlayerDetailVehiclePreviewKeyLight");
            keyLightObj.transform.SetParent(parent, false);
            keyLightObj.transform.localRotation = Quaternion.Euler(42f, -28f, 0f);
            Light keyLight = keyLightObj.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.96f, 0.86f, 1f);
            keyLight.intensity = 1.45f;

            GameObject fillLightObj = new GameObject("PlayerDetailVehiclePreviewFillLight");
            fillLightObj.transform.SetParent(parent, false);
            fillLightObj.transform.localPosition = new Vector3(-3f, 2f, -2f);
            Light fillLight = fillLightObj.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.45f, 0.7f, 1f, 1f);
            fillLight.intensity = 1.8f;
            fillLight.range = 8f;
        }

        private static void ResetPreviewVehiclePose(
            GameObject vehicle,
            PlayerEntity playerEntity,
            float verticalOffset)
        {
            if (vehicle == null)
                return;

            vehicle.transform.localPosition = new Vector3(0f, verticalOffset, 0f);
            vehicle.transform.localRotation = Quaternion.identity;

            if (playerEntity == null)
                return;

            if (playerEntity.BodyTransform != null)
                playerEntity.BodyTransform.localRotation = Quaternion.identity;

            if (playerEntity.TurretTransform != null)
                playerEntity.TurretTransform.localRotation = Quaternion.identity;

            if (playerEntity.BarrelPitchTransform != null)
                playerEntity.BarrelPitchTransform.localRotation = Quaternion.identity;
        }

        private static void PrepareVehicleForPreview(GameObject vehicle)
        {
            if (vehicle == null)
                return;

            Camera[] cameras = vehicle.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
                cameras[i].enabled = false;

            AudioListener[] listeners = vehicle.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
                listeners[i].enabled = false;

            Collider[] colliders = vehicle.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Rigidbody[] bodies = vehicle.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].useGravity = false;
            }

            MonoBehaviour[] behaviours = vehicle.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null)
                    continue;

                behaviours[i].enabled = false;
            }

            if (EntityManager.Instance == null)
                return;

            Entity[] entities = vehicle.GetComponentsInChildren<Entity>(true);
            for (int i = 0; i < entities.Length; i++)
                EntityManager.Instance.UnregisterEntity(entities[i]);
        }
    }
}
