#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 의상 창·캐릭터 생성 화면의 **실제 IMGUI** 촬영 fixture(<c>-battleScenario outfit</c>).
    ///
    /// 두 화면은 OnGUI라 배치모드 캡처(OutfitRenderProbe)로는 3D 마네킹만 보이고 창은 안 보인다
    /// (<c>rules/testing.md</c> 「한계 셋」). 스탠드얼론 검수 빌드에서 <c>ScreenCapture</c>로 찍는다.
    ///
    /// <b>저장을 남기지 않는다</b> — 의상 매니저는 소유·장착을 PlayerPrefs에 쓰므로(해금·장착 연출용),
    /// 시작 전에 두 키를 잡아 두었다가 끝나면 그대로 되돌린다. 입어보기·필터·확대는 창의 private 상태를
    /// 리플렉션으로 넣는다(버튼 좌표를 흉내 내면 해상도마다 깨진다).
    /// </summary>
    public static class OutfitVisualCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] GuardedKeys = { "InsectGame.Equipped", "InsectGame.OwnedOutfits" };

        public static IEnumerator Run(string output, Camera camera)
        {
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.14f);

            // ── 저장 보호 ──
            Dictionary<string, string> saved = new Dictionary<string, string>();
            foreach (string k in GuardedKeys)
                if (PlayerPrefs.HasKey(k)) saved[k] = PlayerPrefs.GetString(k);

            int shots = 0;
            GameObject host = new GameObject("OutfitQA");
            try
            {
                CharacterOutfitManager outfit = host.AddComponent<CharacterOutfitManager>();
                OutfitBonusProvider bonus = host.AddComponent<OutfitBonusProvider>();
                bonus.AutoWire(outfit);
                CharacterModelPreviewRenderer preview = new GameObject("CharacterModelPreviewQA").AddComponent<CharacterModelPreviewRenderer>();
                CharacterOutfitUI ui = new GameObject("CharacterOutfitUIQA").AddComponent<CharacterOutfitUI>();
                ui.AutoWire(outfit, bonus);
                ui.AutoWire(preview);

                // 여러 벌을 가진 상태 — 보유·미보유·NEW·장착 테두리가 한 화면에 섞이게.
                foreach (string id in new[] { "hat_straw", "hat_safari", "top_stripe", "top_galaxy", "top_vest", "bot_jeans",
                                              "bot_overalls", "outer_raincoat", "shoe_waders", "bag_satchel" })
                    outfit.UnlockItem(id);
                HashSet<string> news = (HashSet<string>)typeof(CharacterOutfitManager).GetField("newItems", Private).GetValue(outfit);
                news.Add("hat_flower");
                outfit.UnlockItem("hat_flower");
                news.Add("top_galaxy");
                outfit.Equip("top_stripe");

                ui.Toggle();
                yield return Wait(2.2f);                           // 카드 썸네일은 2프레임에 1장씩 굽는다
                shots++; yield return Capture(output, "outfit-hat");

                SetField(ui, "selectedSlot", OutfitSlot.Top);
                yield return Wait(1.8f);
                shots++; yield return Capture(output, "outfit-top");

                // 여러 슬롯 입어보기(사기 전 조합 맞추기) — 카드 클릭이 넣는 것과 같은 상태
                Dictionary<OutfitSlot, OutfitItem> picks =
                    (Dictionary<OutfitSlot, OutfitItem>)typeof(CharacterOutfitUI).GetField("trySelections", Private).GetValue(ui);
                picks[OutfitSlot.Top] = outfit.FindItem("top_galaxy");
                picks[OutfitSlot.Hat] = outfit.FindItem("hat_cowboy");
                picks[OutfitSlot.Bottom] = outfit.FindItem("bot_overalls");
                picks[OutfitSlot.Shoes] = outfit.FindItem("shoe_rocket");
                picks[OutfitSlot.Outerwear] = outfit.FindItem("outer_legendary");
                yield return Wait(0.8f);
                shots++; yield return Capture(output, "outfit-tryon");

                SetField(ui, "previewCloseUp", true);
                yield return Wait(0.6f);
                shots++; yield return Capture(output, "outfit-closeup");

                picks.Clear();
                SetField(ui, "previewCloseUp", false);
                SetField(ui, "selectedSlot", OutfitSlot.Outerwear);
                System.Type filterType = typeof(CharacterOutfitUI).GetNestedType("ItemFilter", BindingFlags.NonPublic);
                SetField(ui, "filter", System.Enum.Parse(filterType, "NotOwned"));
                yield return Wait(1.6f);
                shots++; yield return Capture(output, "outfit-outer-notowned");
                ui.CloseModal();
                yield return Wait(0.4f);

                // ── 캐시샵 — 재화·가격 라벨(예전 💎·🪙 이모지가 □로 깨지던 자리)과 왼쪽 3D 캐릭터 ──
                // 매니저는 따로 둔다: 이미 있으면 Awake가 자기 GameObject를 파기하는데, host엔 의상 매니저가 있다.
                GameObject shopHost = new GameObject("CashShopQA");
                shopHost.AddComponent<CashShopManager>();
                CashShopUI shop = new GameObject("CashShopUIQA").AddComponent<CashShopUI>();
                shop.AutoWire(preview);
                shop.OpenAtTab(1);
                yield return Wait(1.2f);
                shots++; yield return Capture(output, "shop-items");
                shop.OpenAtTab(2);
                yield return Wait(0.8f);
                shots++; yield return Capture(output, "shop-boxes");
                shop.CloseModal();
                Object.Destroy(shop.gameObject);
                Object.Destroy(shopHost);
                yield return Wait(0.4f);

                // ── 캐릭터 생성 화면 ──
                LoginUI login = new GameObject("LoginUIQA").AddComponent<LoginUI>();
                login.AutoWire(preview);
                typeof(LoginUI).GetMethod("EnterCharacterCreate", Private).Invoke(login, null);
                yield return Wait(1.0f);
                shots++; yield return Capture(output, "create-preset");

                typeof(LoginUI).GetMethod("ApplyPreset", Private).Invoke(login, new object[] { 1 });
                System.Type stepType = typeof(LoginUI).GetNestedType("CreateStep", BindingFlags.Public | BindingFlags.NonPublic);
                SetField(login, "createStep", System.Enum.Parse(stepType, "Customize"));
                yield return Wait(1.0f);
                shots++; yield return Capture(output, "create-customize");

                SetField(login, "selectedHairStyle", 3);
                SetField(login, "selectedHairColor", 4);
                SetField(login, "selectedSkinColor", 3);
                SetField(login, "selectedFaceType", 1);
                yield return Wait(1.0f);
                shots++; yield return Capture(output, "create-customize-changed");
                Object.Destroy(login.gameObject);
            }
            finally
            {
                // ── 저장 복원 ──
                foreach (string k in GuardedKeys)
                {
                    if (saved.TryGetValue(k, out string v)) PlayerPrefs.SetString(k, v);
                    else PlayerPrefs.DeleteKey(k);
                }
                PlayerPrefs.Save();
            }

            File.WriteAllText(Path.Combine(output, "README.txt"),
                $"Actual standalone IMGUI. {shots} shots at {Screen.width}x{Screen.height} (mobile layout={UIScale.IsMobileLayout}).\n" +
                "Outfit ownership/equipment written during the run are restored from a snapshot (no net PlayerPrefs change).\n" +
                "Try-on selections, filter and close-up were injected into the window's private state (same state a card click sets).\n");
            Object.Destroy(host);
            Application.Quit(shots > 0 ? 0 : 3);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, Private);
            if (f == null) throw new FixtureFieldMissing(target.GetType().Name, name);
            f.SetValue(target, value);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Capture(string output, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), shot.EncodeToPNG());
            Object.Destroy(shot);
        }

        private sealed class FixtureFieldMissing : System.Exception
        {
            public FixtureFieldMissing(string type, string field) : base(type + "." + field + " 필드가 없다 — 창 구조가 바뀌었으면 fixture도 고칠 것") { }
        }
    }
}
#endif
