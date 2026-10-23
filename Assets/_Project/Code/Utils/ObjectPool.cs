using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  ObjectPool<T> — Generic reusable object pool
//  Usage: var pool = new ObjectPool<GameObject>(prefab, parent, 10);
//         var obj = pool.Get();
//         pool.Return(obj);
// ============================================================
namespace EchoesOfTheLost.Utils
{
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly List<T> _active = new List<T>();
        private int _totalCreated = 0;

        public int ActiveCount => _active.Count;
        public int PooledCount => _pool.Count;

        public ObjectPool(T prefab, Transform parent = null, int initialSize = 0)
        {
            _prefab = prefab;
            _parent = parent;
            WarmUp(initialSize);
        }

        /// <summary>Get an instance from the pool. Creates new if pool is empty.</summary>
        public T Get()
        {
            T instance = _pool.Count > 0 ? _pool.Dequeue() : CreateNew();
            instance.gameObject.SetActive(true);
            _active.Add(instance);
            return instance;
        }

        /// <summary>Get at a specific position and rotation.</summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance = Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        /// <summary>Return an instance to the pool.</summary>
        public void Return(T instance)
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);
            if (_parent != null) instance.transform.SetParent(_parent);
            _active.Remove(instance);
            _pool.Enqueue(instance);
        }

        /// <summary>Pre-create instances to avoid runtime allocation.</summary>
        public void WarmUp(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T instance = CreateNew();
                instance.gameObject.SetActive(false);
                _pool.Enqueue(instance);
            }
        }

        /// <summary>Return all active instances to the pool.</summary>
        public void ReturnAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Return(_active[i]);
        }

        private T CreateNew()
        {
            T instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.name = $"{_prefab.name}_{_totalCreated++:000}";
            return instance;
        }
    }
}
