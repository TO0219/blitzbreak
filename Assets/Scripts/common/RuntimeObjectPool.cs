using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public interface IRuntimePoolable
    {
        void OnPoolGet();
        void OnPoolRelease();
    }

    internal sealed class RuntimePooledObject : MonoBehaviour
    {
        internal GameObject SourcePrefab { get; private set; }
        internal bool CanReparent { get; private set; }

        private Entity[] m_entities;
        private Rigidbody[] m_rigidbodies;
        private IRuntimePoolable[] m_poolables;

        internal void Initialize(GameObject sourcePrefab)
        {
            SourcePrefab = sourcePrefab;
            CanReparent = GetComponent<NetworkObject>() == null;

            m_entities = GetComponentsInChildren<Entity>(true);
            m_rigidbodies = GetComponentsInChildren<Rigidbody>(true);

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            List<IRuntimePoolable> poolables = new List<IRuntimePoolable>(behaviours.Length);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRuntimePoolable poolable)
                    poolables.Add(poolable);
            }

            m_poolables = poolables.ToArray();
        }

        internal void PrepareForUse()
        {
            for (int i = 0; i < m_entities.Length; i++)
            {
                if (m_entities[i] != null)
                    m_entities[i].PrepareForReuse();
            }

            for (int i = 0; i < m_poolables.Length; i++)
                m_poolables[i].OnPoolGet();
        }

        internal void PrepareForRelease()
        {
            for (int i = 0; i < m_poolables.Length; i++)
                m_poolables[i].OnPoolRelease();

            if (EntityManager.Instance != null)
            {
                for (int i = 0; i < m_entities.Length; i++)
                {
                    if (m_entities[i] != null)
                        EntityManager.Instance.UnregisterEntity(m_entities[i]);
                }
            }

            for (int i = 0; i < m_rigidbodies.Length; i++)
            {
                Rigidbody body = m_rigidbodies[i];
                if (body == null || body.isKinematic)
                    continue;

                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }

    public sealed class RuntimeObjectPool : MonoBehaviour
    {
        private const int MaxStoredPerPrefab = 64;

        private static RuntimeObjectPool s_instance;

        private readonly Dictionary<GameObject, Stack<RuntimePooledObject>> m_available =
            new Dictionary<GameObject, Stack<RuntimePooledObject>>();

        public static GameObject Spawn(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            if (prefab == null)
                return null;

            RuntimeObjectPool pool = GetOrCreateInstance();
            RuntimePooledObject pooledObject = pool.Take(prefab);

            if (pooledObject == null)
            {
                bool canReparent = prefab.GetComponent<NetworkObject>() == null;
                GameObject instance = canReparent
                    ? Instantiate(prefab, position, rotation, parent)
                    : Instantiate(prefab, position, rotation);
                pooledObject = instance.AddComponent<RuntimePooledObject>();
                pooledObject.Initialize(prefab);
                pooledObject.PrepareForUse();
                return instance;
            }

            Transform objectTransform = pooledObject.transform;
            if (pooledObject.CanReparent)
                objectTransform.SetParent(parent, false);
            objectTransform.SetPositionAndRotation(position, rotation);
            objectTransform.localScale = prefab.transform.localScale;

            pooledObject.PrepareForUse();
            pooledObject.gameObject.SetActive(true);
            return pooledObject.gameObject;
        }

        public static bool TryRelease(GameObject instance)
        {
            if (instance == null)
                return false;

            RuntimePooledObject pooledObject = instance.GetComponent<RuntimePooledObject>();
            if (pooledObject == null || pooledObject.SourcePrefab == null)
                return false;

            GetOrCreateInstance().Release(pooledObject);
            return true;
        }

        private static RuntimeObjectPool GetOrCreateInstance()
        {
            if (s_instance != null)
                return s_instance;

            GameObject poolObject = new GameObject(nameof(RuntimeObjectPool));
            s_instance = poolObject.AddComponent<RuntimeObjectPool>();
            DontDestroyOnLoad(poolObject);
            return s_instance;
        }

        private RuntimePooledObject Take(GameObject prefab)
        {
            if (!m_available.TryGetValue(prefab, out Stack<RuntimePooledObject> available))
                return null;

            while (available.Count > 0)
            {
                RuntimePooledObject pooledObject = available.Pop();
                if (pooledObject != null)
                    return pooledObject;
            }

            return null;
        }

        private void Release(RuntimePooledObject pooledObject)
        {
            if (!pooledObject.gameObject.activeSelf)
                return;

            pooledObject.PrepareForRelease();
            pooledObject.gameObject.SetActive(false);
            if (pooledObject.CanReparent)
                pooledObject.transform.SetParent(transform, false);

            GameObject sourcePrefab = pooledObject.SourcePrefab;
            if (!m_available.TryGetValue(sourcePrefab, out Stack<RuntimePooledObject> available))
            {
                available = new Stack<RuntimePooledObject>();
                m_available.Add(sourcePrefab, available);
            }

            if (available.Count >= MaxStoredPerPrefab)
            {
                Destroy(pooledObject.gameObject);
                return;
            }

            available.Push(pooledObject);
        }
    }
}
