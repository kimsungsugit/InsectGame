using InsectGame.Dex;
using InsectGame.Spawning;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Capture
{
    public class CaptureInputController : MonoBehaviour
    {
        [SerializeField] private CaptureTriggerModeController modeController;
        [SerializeField] private CaptureRaycastTrigger raycastTrigger;
        [SerializeField] private CaptureProximityTrigger proximityTrigger;
        [SerializeField] private CaptureMinigameController minigame;
        [SerializeField] private CaptureChoiceUI choiceUi;
        [SerializeField] private BattleScreenUI battleScreen;
        [SerializeField] private RaidBattleUI raidScreen;
        [SerializeField] private DexScreenUI dexScreen;

        private InsectEntity nearestInsect;
        private float nearCheckTimer;

        private void Update()
        {
            nearCheckTimer -= Time.deltaTime;
            if (nearCheckTimer <= 0f)
            {
                nearestInsect = FindNearestInsect();
                nearCheckTimer = 0.15f;
            }

            bool anyBlockingUI = (minigame != null && minigame.IsActive)
                || (choiceUi != null && choiceUi.IsChoiceOpen)
                || (battleScreen != null && battleScreen.IsBattleActive)
                || (raidScreen != null && raidScreen.IsRaidActive)
                || (dexScreen != null && dexScreen.IsOpen);

            if (Input.GetKeyDown(KeyCode.E) && !anyBlockingUI)
                TryStartCapture();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (choiceUi != null && choiceUi.IsChoiceOpen)
                    choiceUi.Hide();
                else if (minigame != null && minigame.IsActive)
                    minigame.CancelCapture();
            }
        }

        private void OnGUI()
        {
            bool anyUI = (minigame != null && minigame.IsActive)
                || (choiceUi != null && choiceUi.IsChoiceOpen)
                || (battleScreen != null && battleScreen.IsBattleActive)
                || (raidScreen != null && raidScreen.IsRaidActive)
                || (dexScreen != null && dexScreen.IsOpen);

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.E && !anyUI)
                {
                    TryStartCapture();
                    evt.Use();
                }
                if (evt.keyCode == KeyCode.Escape)
                {
                    if (choiceUi != null && choiceUi.IsChoiceOpen)
                        choiceUi.Hide();
                    else if (minigame != null && minigame.IsActive)
                        minigame.CancelCapture();
                    evt.Use();
                }
            }

            if (anyUI || nearestInsect == null) return;

            float w = 380f, h = 64f;
            float x = (Screen.width - w) / 2f;
            float y = Screen.height * 0.62f;

            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.9f, 0.4f);
            GUI.DrawTexture(new Rect(x, y + h - 3, w, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string name = nearestInsect.Data != null ? nearestInsect.Data.displayName : "곤충";
            GUIStyle style = new GUIStyle(GUI.skin.button)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(0.9f, 1f, 0.9f);
            style.hover.textColor = new Color(0.6f, 1f, 0.7f);
            style.normal.background = null;
            style.hover.background = Texture2D.whiteTexture;
            style.active.background = Texture2D.whiteTexture;
            style.active.textColor = Color.white;

            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f, 0.25f);
            if (GUI.Button(new Rect(x, y, w, h), $"[E / 클릭] {name} 포획", style))
                TryStartCapture();
            GUI.backgroundColor = Color.white;
        }

        public void TryStartCapture()
        {
            InsectEntity target = nearestInsect ?? FindNearestInsect();
            if (target == null) return;

            if (choiceUi != null)
                choiceUi.ShowChoice(target);
            else if (minigame != null)
                minigame.StartMinigame(target);
        }

        private InsectEntity FindNearestInsect()
        {
            if (proximityTrigger == null) return null;

            InsectEntity[] allInsects = FindObjectsByType<InsectEntity>(FindObjectsSortMode.None);
            Vector3 origin = proximityTrigger.transform.position;
            float bestDist = float.MaxValue;
            InsectEntity best = null;
            float radius = 8f;

            SphereCollider col = proximityTrigger.GetComponent<SphereCollider>();
            if (col != null) radius = col.radius;

            foreach (var e in allInsects)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(origin, e.transform.position);
                if (d <= radius && d < bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best;
        }

        public void AutoWire(CaptureTriggerModeController controller, CaptureRaycastTrigger raycast, CaptureProximityTrigger proximity)
        {
            if (modeController == null) modeController = controller;
            if (raycastTrigger == null) raycastTrigger = raycast;
            if (proximityTrigger == null) proximityTrigger = proximity;
        }

        public void AutoWire(CaptureChoiceUI choice)
        {
            if (choiceUi == null) choiceUi = choice;
        }

        public void AutoWire(BattleScreenUI battle, RaidBattleUI raid, DexScreenUI dex)
        {
            if (battleScreen == null) battleScreen = battle;
            if (raidScreen == null) raidScreen = raid;
            if (dexScreen == null) dexScreen = dex;
        }
    }
}
