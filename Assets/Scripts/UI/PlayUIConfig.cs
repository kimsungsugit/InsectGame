using UnityEngine;

namespace InsectGame.UI
{
    [CreateAssetMenu(menuName = "InsectGame/UI Config", fileName = "PlayUIConfig")]
    public class PlayUIConfig : ScriptableObject
    {
        public GameObject playHudPrefab;
    }
}
