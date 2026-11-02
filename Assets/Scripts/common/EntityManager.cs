using MyGame;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Entity = MyGame.Entity;

namespace MyGame
{
    public class EntityManager : MonoBehaviour
    {
        // シングルトンのインスタンス  
        public static EntityManager Instance { get; private set; }

        // EntityList
        private readonly List<Entity> Entities = new List<Entity>();

        //********************************************************
        // ライフサイクル Entityクラスをポリモーフィズムで自動化
        //********************************************************

        void Awake()
        {
            // シングルトンのインスタンスを設定
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // シーン内の全てのEntityを自動登録
            var entities = FindObjectsByType<Entity>();
            for (int i = 0; i < entities.Length; i++)
            {
                AddEntity(entities[i]);
            }

            // あとからAdditiveで読み込まれたシーンも拾う
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            // 登録解除
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        void Start()
        {

        }

        void Update()
        {
            if (BattlePauseUIController.IsPaused && Time.timeScale <= 0f)
                return;
            // Entityの状態更新
            for (int i = 0; i < Entities.Count; i++)
            {
                Entity entity = Entities[i];
                if (entity == null) continue;
                entity.UpdateActiveState();
            }

            for (int i = 0; i < Entities.Count; i++)
            {
                Entity entity = Entities[i];
                // アクティブでないEntityはスキップ
                if (entity == null) continue;
                if (!entity.IsActive) continue;

                entity.UpdateEntity();
            }
        }

        void FixedUpdate()
        {
            if (BattlePauseUIController.IsPaused && Time.timeScale <= 0f)
                return;
            for (int i = 0; i < Entities.Count; i++)
            {
                Entity entity = Entities[i];
                // アクティブでないEntityはスキップ
                if (entity == null) continue;
                if (!entity.IsActive) continue;

                entity.FixedUpdateEntity();
            }
        }

        void LateUpdate()
        {
            if (BattlePauseUIController.IsPaused && Time.timeScale <= 0f)
                return;
            for (int i = 0; i < Entities.Count; i++)
            {
                Entity entity = Entities[i];
                // アクティブでないEntityはスキップ
                if (entity == null) continue;
                if (!entity.IsActive) continue;

                entity.LateUpdateEntity();
            }

            // 非アクティブなEntityの状態更新
            for (int i = 0; i < Entities.Count; i++)
            {
                Entity entity = Entities[i];
                if (entity == null) continue;
                if (entity.IsActive) continue;
                entity.UpdateActiveState();
            }

            int destroyIndex = Entities.Count - 1;
            while (destroyIndex >= 0)
            {
                if (destroyIndex >= Entities.Count)
                {
                    destroyIndex = Entities.Count - 1;
                    continue;
                }

                Entity entity = Entities[destroyIndex];
                if (entity == null)
                {
                    Entities.RemoveAt(destroyIndex);
                    destroyIndex--;
                    continue;
                }

                if (entity.IsDestroy)
                    DestroyEntityOwner(entity);

                destroyIndex--;
            }
        }

        //********************************************************
        // Scene読み込み時
        //********************************************************

        /// <summary>
        /// シーン読み込み時に、そのシーン内のEntityを追加登録
        /// </summary>
        /// <param name="scene">読み込まれたシーン</param>
        /// <param name="mode">読み込みモード</param>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();

            for (int i = 0; i < rootObjects.Length; i++)
            {
                Entity[] entities = rootObjects[i].GetComponentsInChildren<Entity>(true);

                for (int j = 0; j < entities.Length; j++)
                {
                    AddEntity(entities[j]);
                }
            }
        }

        //********************************************************
        // Entity管理
        //********************************************************

        /// <summary>
        /// Entityを追加
        /// </summary>
        /// <param name="entity">追加するEntity</param>
        public void AddEntity(Entity entity)
        {
            if (entity == null) return;
            if (Entities.Contains(entity)) return;
            Entities.Add(entity);
            entity.InitEntity();
        }

        /// <summary>
        /// Entityを削除
        /// </summary>
        /// <param name="entity">削除するEntity</param>
        public void RemoveEntity(Entity entity)
        {
            UnregisterEntity(entity);
        }

        public void UnregisterEntity(Entity entity)
        {
            if (ReferenceEquals(entity, null)) return;
            if (!Entities.Remove(entity)) return;

            entity.UninitEntity();
        }

        public void GetEntities<T>(List<T> results) where T : Entity
        {
            if (results == null) return;

            results.Clear();
            for (int i = 0; i < Entities.Count; i++)
            {
                if (Entities[i] is T entity && entity != null)
                    results.Add(entity);
            }
        }

        /// <summary>
        /// 全てのEntityを削除
        /// </summary>
        public void ClearEntities()
        {
            for (int i = 0; i < Entities.Count; i++)
            {
                Entity entity = Entities[i];
                if (entity == null) continue;
                entity.UninitEntity();
            }

            Entities.Clear();
        }

        private void DestroyEntityOwner(Entity entity)
        {
            if (entity == null) return;

            GameObject owner = entity.gameObject;
            Entity[] ownerEntities = owner.GetComponentsInChildren<Entity>(true);
            for (int i = 0; i < ownerEntities.Length; i++)
                UnregisterEntity(ownerEntities[i]);

            if (!RuntimeObjectPool.TryRelease(owner))
                Destroy(owner);
        }
    }
}
