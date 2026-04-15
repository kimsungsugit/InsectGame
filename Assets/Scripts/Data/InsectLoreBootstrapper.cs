using UnityEngine;

namespace InsectGame.Data
{
    public class InsectLoreBootstrapper : MonoBehaviour
    {
        [SerializeField] private InsectDatabase database;

        private void Awake()
        {
            InsectLoreService.ApplyToDatabase(database);
        }

        public void AutoWire(InsectDatabase db)
        {
            if (database == null)
            {
                database = db;
                InsectLoreService.ApplyToDatabase(database);
            }
        }
    }
}
