using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Spawning
{
    public class SimpleObjectPool
    {
        private readonly Queue<GameObject> pool = new Queue<GameObject>();
        private readonly GameObject prefab;
        private readonly Transform parent;

        public SimpleObjectPool(GameObject prefab, int prewarmCount, Transform parent)
        {
            this.prefab = prefab;
            this.parent = parent;

            for (int i = 0; i < prewarmCount; i++)
            {
                GameObject instance = Object.Instantiate(prefab, parent);
                instance.SetActive(false);
                pool.Enqueue(instance);
            }
        }

        public GameObject Get()
        {
            if (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                obj.SetActive(true);
                return obj;
            }

            GameObject newObj = Object.Instantiate(prefab, parent);
            newObj.SetActive(true);
            return newObj;
        }

        public void Return(GameObject obj)
        {
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }
}
