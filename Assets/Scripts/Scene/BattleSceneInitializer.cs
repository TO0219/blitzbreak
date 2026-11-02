using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame
{
    public class BattleSceneInitializer : MonoBehaviour
    {
        [SerializeField] private GameObject soloPlayerPrefab;
        [SerializeField] private Transform soloSpawnPoint;
        [SerializeField] private GameObject soloCpuPrefab;
        [SerializeField] private Transform[] soloCpuSpawnPoints;
        [SerializeField] private Transform centralLandmark;
        [SerializeField] private string battleUiSceneName = "BattleUIScene";
        [SerializeField] private string battlePauseUiSceneName = "BattlePauseUIScene";

        [Header("Building Destruction")]
        [SerializeField] private BattleBuildingDestructionSettings buildingDestructionSettings = new BattleBuildingDestructionSettings();

        [Header("Small Object Destruction")]
        [SerializeField] private Transform smallObjectRoot;
        [SerializeField] private BattleSmallObjectDestructionSettings smallObjectDestructionSettings = new BattleSmallObjectDestructionSettings();

        private IEnumerator Start()
        {
            DestroyVehicleSelectUiObjects();
            BattleBuildingDestructionInstaller.Install(
                buildingDestructionSettings,
                centralLandmark);
            BattleSmallObjectDestructionInstaller.Install(
                smallObjectRoot,
                smallObjectDestructionSettings);

            if (GameModeManager.Instance == null)
            {
                yield break;
            }

            Scene uiScene = SceneManager.GetSceneByName(battleUiSceneName);
            if (!uiScene.IsValid() || !uiScene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync(battleUiSceneName, LoadSceneMode.Additive);
            }

            Scene pauseUiScene = SceneManager.GetSceneByName(battlePauseUiSceneName);
            if (!string.IsNullOrWhiteSpace(battlePauseUiSceneName) &&
                (!pauseUiScene.IsValid() || !pauseUiScene.isLoaded))
            {
                yield return SceneManager.LoadSceneAsync(battlePauseUiSceneName, LoadSceneMode.Additive);
            }

            if (GameModeManager.Instance.IsOnlineMultiplayer())
            {
                yield break;
            }

            if (!GameModeManager.Instance.IsSoloOffline()) yield break;
            if (soloPlayerPrefab == null) yield break;

            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (soloSpawnPoint != null)
            {
                spawnPos = soloSpawnPoint.position;
                spawnRot = soloSpawnPoint.rotation;
            }

            GameObject playerObj = Instantiate(soloPlayerPrefab, spawnPos, spawnRot);
            playerObj.name = "SoloPlayer_Runtime";

            SceneManager.MoveGameObjectToScene(playerObj, gameObject.scene);

            RegisterEntities(playerObj);

            BattleUIBinderEntity binder = FindAnyObjectByType<BattleUIBinderEntity>();
            PlayerEntity playerEntity = playerObj.GetComponent<PlayerEntity>();
            if (playerEntity != null)
            {
                playerEntity.ApplyMachineIndex(GameModeManager.Instance.SelectedMachineIndex);
                playerEntity.SetRespawnPoint(spawnPos, spawnRot);
            }

            if (binder != null && playerEntity != null)
            {
                binder.SetPlayerEntity(playerEntity);
            }

            SpawnSoloCpus();

            Camera playerCam = playerObj.GetComponentInChildren<Camera>(true);
            if (playerCam != null)
            {
                playerCam.gameObject.SetActive(true);
                playerCam.enabled = true;
                playerCam.targetDisplay = 0;
                playerCam.tag = "MainCamera";
            }

            AudioListener playerListener = playerObj.GetComponentInChildren<AudioListener>(true);
            if (playerListener != null)
            {
                AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
                for (int i = 0; i < listeners.Length; i++)
                {
                    listeners[i].enabled = false;
                }

                playerListener.enabled = true;
            }
        }

        private void SpawnSoloCpus()
        {
            if (soloCpuPrefab == null || soloCpuSpawnPoints == null)
                return;

            int cpuCount = Mathf.Min(3, soloCpuSpawnPoints.Length);
            int machineCount = MachineProfileCatalog.Count;

            for (int i = 0; i < cpuCount; i++)
            {
                Transform spawnPoint = soloCpuSpawnPoints[i];
                if (spawnPoint == null)
                    continue;

                GameObject cpuObj = Instantiate(
                    soloCpuPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation
                );

                cpuObj.name = "EnemyCPU_" + (i + 1) + "_Runtime";
                SceneManager.MoveGameObjectToScene(cpuObj, gameObject.scene);
                RegisterEntities(cpuObj);

                EnemyCPUEntity cpu = cpuObj.GetComponent<EnemyCPUEntity>();
                if (cpu == null)
                    continue;

                if (machineCount > 0)
                    cpu.ApplyMachineIndex((i + 1) % machineCount);

                cpu.SetObstacleAvoidanceSide(i % 2 == 0 ? 1 : -1);
                cpu.SetRespawnPoint(spawnPoint.position, spawnPoint.rotation);
                cpu.SetCentralLandmark(centralLandmark, spawnPoint.position);
            }
        }

        private void DestroyVehicleSelectUiObjects()
        {
            DestroyNamedObject("VehicleSelectCanvas");
            DestroyNamedObject("VehicleSelectUIController");
        }

        private void DestroyNamedObject(string objectName)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        private void RegisterEntities(GameObject rootObj)
        {
            EntityManager entityManager = FindAnyObjectByType<EntityManager>();
            if (entityManager == null) return;

            Entity[] entities = rootObj.GetComponentsInChildren<Entity>(true);
            for (int i = 0; i < entities.Length; i++)
            {
                entityManager.AddEntity(entities[i]);
            }
        }
    }
}
