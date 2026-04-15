using InsectGame.Core;
using InsectGame.Dex;
using UnityEngine;

namespace InsectGame.UI
{
    public class QuickAccessBarUI : MonoBehaviour
    {
        [SerializeField] private DexScreenUI dexScreen;
        [SerializeField] private BattleTeamUI battleTeamUI;
        [SerializeField] private TrainingUI trainingUI;
        [SerializeField] private CollectionUI collectionUI;
        [SerializeField] private RegionMapUI regionMapUI;
        [SerializeField] private CharacterViewerUI characterViewer;
        [SerializeField] private CharacterOutfitUI outfitUI;
        [SerializeField] private CashShopUI cashShopUI;
        [SerializeField] private TutorialQuestUI questUI;

        private struct ButtonDef
        {
            public string label;
            public string key;
            public Color color;
        }

        private readonly ButtonDef[] buttons = new ButtonDef[]
        {
            new ButtonDef { label = "도감", key = "N", color = new Color(1f, 0.85f, 0.3f) },
            new ButtonDef { label = "배틀팀", key = "T", color = new Color(1f, 0.5f, 0.2f) },
            new ButtonDef { label = "훈련", key = "G", color = new Color(0.4f, 0.85f, 0.4f) },
            new ButtonDef { label = "컬렉션", key = "C", color = new Color(0.4f, 0.6f, 1f) },
            new ButtonDef { label = "퀘스트", key = "Q", color = new Color(0.3f, 0.9f, 0.7f) },
            new ButtonDef { label = "지도", key = "M", color = new Color(0.7f, 0.5f, 0.9f) },
            new ButtonDef { label = "캐릭터", key = "V", color = new Color(0.9f, 0.6f, 0.8f) },
            new ButtonDef { label = "의상", key = "P", color = new Color(0.8f, 0.7f, 1f) },
            new ButtonDef { label = "상점", key = "F4", color = new Color(1f, 0.4f, 0.4f) },
        };

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.N)) OnClick(0);
            if (Input.GetKeyDown(KeyCode.T)) OnClick(1);
            if (Input.GetKeyDown(KeyCode.G)) OnClick(2);
            if (Input.GetKeyDown(KeyCode.C)) OnClick(3);
            if (Input.GetKeyDown(KeyCode.Q)) OnClick(4);
            if (Input.GetKeyDown(KeyCode.M)) OnClick(5);
            if (Input.GetKeyDown(KeyCode.V)) OnClick(6);
            if (Input.GetKeyDown(KeyCode.P)) OnClick(7);
            if (Input.GetKeyDown(KeyCode.F4)) OnClick(8);
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                bool handled = false;
                switch (e.keyCode)
                {
                    case KeyCode.N: OnClick(0); handled = true; break;
                    case KeyCode.T: OnClick(1); handled = true; break;
                    case KeyCode.G: OnClick(2); handled = true; break;
                    case KeyCode.C: OnClick(3); handled = true; break;
                    case KeyCode.Q: OnClick(4); handled = true; break;
                    case KeyCode.M: OnClick(5); handled = true; break;
                    case KeyCode.V: OnClick(6); handled = true; break;
                    case KeyCode.P: OnClick(7); handled = true; break;
                    case KeyCode.F4: OnClick(8); handled = true; break;
                }
                if (handled) e.Use();
            }

            float btnW = Mathf.Min(140f, (Screen.width - 100f) / buttons.Length);
            float btnH = 64f;
            float gap = 6f;
            float totalW = buttons.Length * btnW + (buttons.Length - 1) * gap;
            float startX = (Screen.width - totalW) / 2f;
            float y = Screen.height - btnH - 16f;

            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(startX - 14, y - 8, totalW + 28, btnH + 16), Texture2D.whiteTexture);
            GUI.color = Color.white;

            for (int i = 0; i < buttons.Length; i++)
            {
                float bx = startX + i * (btnW + gap);
                ButtonDef def = buttons[i];

                bool isActive = IsActive(i);

                GUI.backgroundColor = isActive
                    ? new Color(def.color.r * 0.6f, def.color.g * 0.6f, def.color.b * 0.6f)
                    : new Color(def.color.r * 0.2f, def.color.g * 0.2f, def.color.b * 0.2f);

                GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
                { fontSize = 20, fontStyle = FontStyle.Bold };
                btnStyle.normal.textColor = isActive ? Color.white : def.color;
                btnStyle.hover.textColor = Color.white;

                if (GUI.Button(new Rect(bx, y, btnW, btnH), $"{def.label}\n<size=14>[{def.key}]</size>", btnStyle))
                    OnClick(i);

                if (isActive)
                {
                    GUI.color = def.color;
                    GUI.DrawTexture(new Rect(bx, y, btnW, 4), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }

            GUI.backgroundColor = Color.white;
        }

        private bool IsActive(int index)
        {
            switch (index)
            {
                case 0: return dexScreen != null && dexScreen.IsOpen;
                case 1: return battleTeamUI != null && battleTeamUI.IsOpen;
                case 2: return trainingUI != null && trainingUI.IsOpen;
                case 3: return collectionUI != null && collectionUI.IsOpen;
                case 4: return questUI != null && questUI.IsOpen;
                case 5: return regionMapUI != null && regionMapUI.IsOpen;
                case 6: return characterViewer != null && characterViewer.IsOpen;
                case 7: return outfitUI != null && outfitUI.IsOpen;
                case 8: return cashShopUI != null && cashShopUI.IsOpen;
                default: return false;
            }
        }

        private int lastToggleFrame = -1;

        private void OnClick(int index)
        {
            if (Time.frameCount == lastToggleFrame) return;
            lastToggleFrame = Time.frameCount;

            switch (index)
            {
                case 0: if (dexScreen != null) dexScreen.Toggle(); break;
                case 1: if (battleTeamUI != null) battleTeamUI.Toggle(); break;
                case 2: if (trainingUI != null) trainingUI.Toggle(); break;
                case 3: if (collectionUI != null) collectionUI.Toggle(); break;
                case 4: if (questUI != null) questUI.Toggle(); break;
                case 5: if (regionMapUI != null) regionMapUI.Toggle(); break;
                case 6: if (characterViewer != null) characterViewer.Toggle(); break;
                case 7: if (outfitUI != null) outfitUI.Toggle(); break;
                case 8: if (cashShopUI != null) cashShopUI.Toggle(); break;
            }
        }

        public void AutoWire(DexScreenUI dex, BattleTeamUI team, TrainingUI training,
            CollectionUI collection, RegionMapUI map)
        {
            if (dexScreen == null) dexScreen = dex;
            if (battleTeamUI == null) battleTeamUI = team;
            if (trainingUI == null) trainingUI = training;
            if (collectionUI == null) collectionUI = collection;
            if (regionMapUI == null) regionMapUI = map;
        }

        public void AutoWire(CharacterViewerUI viewer, CharacterOutfitUI outfit, CashShopUI cashShop)
        {
            if (characterViewer == null) characterViewer = viewer;
            if (outfitUI == null) outfitUI = outfit;
            if (cashShopUI == null) cashShopUI = cashShop;
        }

        public void AutoWire(TutorialQuestUI quest)
        {
            if (questUI == null) questUI = quest;
        }
    }
}
