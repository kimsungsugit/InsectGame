#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using InsectGame.Core;
using InsectGame.Story;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 「지난 이야기」 카드·HUD 목표 행 두 줄(할 일 + 왜)·잡담 띠의 "다음 이야기 — 이유"·대사 직후 대결 버튼(「건너뛰고 승부」·「승부!」)
    /// — 실제 IMGUI 검수(<c>-battleScenario story</c>의 앞 컷).
    /// 대사 무대 컷(<c>StoryDialogueCapture</c>)보다 먼저 돌고, 끝나면 만든 것을 전부 치운다.
    ///
    /// <b>저장을 건드리지 않는다</b> — <see cref="StoryDirector"/>를 만들지도 주입하지도 않아 카드 뒤 대사를 닫아도 완료 처리(보상·열람 기록)가 없고,
    /// 목표 트래커는 꺼진 오브젝트에 세워 갱신(Refresh)이 돌지 않는다(필드는 리플렉션으로 채운다). 진짜 퀘스트 칩은 로그인 세션이 있어야 그려져서
    /// 칩 자리는 같은 순수 계산(<see cref="QuestChipLayout"/>)으로 대역을 그리고, 목표 행은 진짜 <see cref="TutorialQuestUI"/>가 그린다.
    /// </summary>
    public static class StoryRecapVisualCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        /// <summary>카드 검수에 쓰는 장 — Story.json의 장 데이터가 있으면 그걸, 없으면 아래 예시로 만든다.</summary>
        public const string SampleChapterId = "ch8";
        /// <summary>「승부!」 검수 비트 — 집게 대치(대사가 끝나면 곧바로 간부전). 없으면 아래 예시로 만든다.</summary>
        public const string DuelBeatId = "ch8_confront";

        public static IEnumerator Run(string output, Camera camera)
        {
            var made = new List<GameObject>();
            var notes = new StringBuilder();
            var shots = new List<string>();
            try
            {
                // ── 1. 「지난 이야기」 카드 → 첫 대사 ──
                var uiGo = new GameObject("RecapQA");
                made.Add(uiGo);
                NpcDialogueUI ui = uiGo.AddComponent<NpcDialogueUI>();

                bool fromData = StoryService.TryGetChapter(SampleChapterId, out StoryChapter chapter)
                                && StoryRecapLayout.ShouldShow(chapter, false);
                if (!fromData) chapter = SampleChapter();
                StoryBeat beat = null;
                bool realBeat = !string.IsNullOrEmpty(chapter.openingBeatId)
                                && StoryService.TryGetBeat(chapter.openingBeatId, out beat)
                                && beat != null && beat.lines != null && beat.lines.Count > 0;
                if (!realBeat) beat = SampleBeat(chapter.openingBeatId);
                notes.AppendLine($"recap chapter: {(fromData ? "Story.json" : "fixture sample")} {chapter.chapterId} \"{chapter.title}\"");
                notes.AppendLine($"opening beat: {(realBeat ? "Story.json" : "fixture sample")} {beat.beatId}");

                // 진짜 발화 경로(ShowStory, 다시보기 아님)로 연다 — 장 데이터에 이 비트가 여는 장이 있으면 카드가 스스로 선다.
                ui.ShowStory(beat);
                bool cardByRealPath = ui.IsShowingRecap;
                if (!cardByRealPath) ShowRecapCard(ui, chapter);   // 데이터가 아직 없으면 카드만 주입한다
                notes.AppendLine($"card opened by: {(cardByRealPath ? "real ShowStory path (TryGetChapterOpenedBy)" : "injected (no chapter data for this beat)")}");
                yield return Wait(1.0f);   // 패널 페이드 + 입력 지연(0.4초)이 지나 안내 글자가 진해진 뒤
                yield return Capture(output, "recap-card", shots);

                // 카드를 넘긴다(탭과 같은 경로) → 첫 대사가 타자 효과로 시작한다.
                typeof(NpcDialogueUI).GetMethod("EndRecap", Private)?.Invoke(ui, null);
                typeof(NpcDialogueUI).GetField("lineRevealAll", Private)?.SetValue(ui, true);
                yield return Wait(0.6f);
                yield return Capture(output, "recap-first-line", shots);
                ForceClose(ui);

                // 줄이 많고 긴 장 — 판(대사 상자 위)을 넘지 않고 글자를 줄여 맞추는지.
                ui.ShowStory(beat);
                ShowRecapCard(ui, LongChapter());
                yield return Wait(1.0f);
                yield return Capture(output, "recap-card-long", shots);
                ForceClose(ui);

                // ── 2. HUD 목표 행 — 할 일 + 왜 두 줄 ──
                var trackerGo = new GameObject("ObjectiveWhyQA");
                made.Add(trackerGo);
                trackerGo.SetActive(false);   // Refresh(진짜 목표 고르기)가 돌지 않게 — 필드만 채운다
                StoryObjectiveTracker tracker = trackerGo.AddComponent<StoryObjectiveTracker>();
                var playerGo = new GameObject("ObjectiveWhyQAPlayer");
                made.Add(playerGo);
                SetField(tracker, "hasObjective", true);
                SetField(tracker, "label", "모래언덕으로");
                SetField(tracker, "hasWorldTarget", true);
                SetField(tracker, "targetPosition", new Vector3(0f, 0f, 42f));
                SetField(tracker, "playerTransform", playerGo.transform);
                bool whySet = SetWhy(tracker, "상자에 갇힌 곤충이 있대");
                notes.AppendLine($"tracker Why injected: {whySet} (Why = \"{tracker.Why}\")");

                var questGo = new GameObject("QuestRowQA");
                made.Add(questGo);
                questGo.SetActive(false);   // 진짜 OnGUI(로그인 세션이 필요한 칩)는 돌지 않게 — 행만 대역이 부른다
                TutorialQuestUI questUi = questGo.AddComponent<TutorialQuestUI>();
                questUi.AutoWire(tracker);
                var standInGo = new GameObject("QuestChipStandInQA");
                made.Add(standInGo);
                standInGo.AddComponent<RecapQaQuestStack>().Bind(questUi, tracker);
                yield return Wait(0.6f);
                yield return Capture(output, "hud-objective-why", shots);

                // 비교 — 이유가 비면 예전과 같은 한 줄 높이.
                SetWhy(tracker, string.Empty);
                yield return Wait(0.4f);
                yield return Capture(output, "hud-objective-oneline", shots);
                UnityEngine.Object.Destroy(standInGo);

                // ── 3. 잡담 띠 — "다음 이야기 · 할 일 — 이유" ──
                SetWhy(tracker, "상자에 갇힌 곤충이 있대");
                var npcGo = new GameObject("AmbientWhyQA");
                made.Add(npcGo);
                var npc = npcGo.AddComponent<InsectGame.NPC.VillagerNpc>();
                npc.Initialize(new NpcSpawnAnchor { regionId = "meadow" }, "qa_resident", "마을 주민", 8173);
                ui.AutoWire(tracker);
                ui.Show(npc);
                yield return Wait(0.8f);
                yield return Capture(output, "ambient-next-why", shots);
                ForceClose(ui);

                // ── 4. 대사 직후 대결 — 첫 줄의 「건너뛰고 승부」, 마지막 줄의 「승부!」 ──
                // 진짜 발화 경로(ShowStory)로 연다. 대결 대기열(StoryDuelLauncher)은 세우지 않는다 — 비트 자신의 duelAfter만으로 버튼이 바뀌어야 한다.
                bool realDuelBeat = StoryService.TryGetBeat(DuelBeatId, out StoryBeat duelBeat)
                                    && duelBeat != null && duelBeat.lines != null && duelBeat.lines.Count > 1
                                    && !string.IsNullOrEmpty(duelBeat.duelAfter);
                if (!realDuelBeat) duelBeat = SampleDuelBeat();
                notes.AppendLine($"duel beat: {(realDuelBeat ? "Story.json" : "fixture sample")} {duelBeat.beatId} → duelAfter {duelBeat.duelAfter}");
                ui.ShowStory(duelBeat);
                if (ui.IsShowingRecap) typeof(NpcDialogueUI).GetMethod("EndRecap", Private)?.Invoke(ui, null);
                typeof(NpcDialogueUI).GetField("lineRevealAll", Private)?.SetValue(ui, true);
                yield return Wait(0.8f);
                yield return Capture(output, "duel-skip", shots);

                typeof(NpcDialogueUI).GetMethod("BeginStoryLine", Private)?.Invoke(ui, new object[] { duelBeat.lines.Count - 1 });
                typeof(NpcDialogueUI).GetField("lineRevealAll", Private)?.SetValue(ui, true);
                yield return Wait(0.6f);
                yield return Capture(output, "duel-button", shots);
                ForceClose(ui);
            }
            finally
            {
                foreach (GameObject go in made)
                    if (go != null) UnityEngine.Object.Destroy(go);
            }
            yield return null;   // Destroy는 프레임 끝에 — 다음 장면(대사 무대)이 이 화면들 없이 시작하게

            File.WriteAllText(Path.Combine(output, "recap-README.txt"),
                "Actual standalone IMGUI. 「지난 이야기」 card (NpcDialogueUI), HUD objective row with the why line (real TutorialQuestUI row, " +
                "quest chip is a stand-in from the same QuestChipLayout), ambient 'next story — why', duel-after dialogue buttons " +
                "(duel-skip: first line [건너뛰고 승부], duel-button: last line 「승부!」). No StoryDirector/save services.\n" +
                notes + "shots: " + string.Join(", ", shots) + "\n");
        }

        /// <summary>장 데이터가 아직 없을 때 쓰는 예시(작업 지시의 ch8).</summary>
        private static StoryChapter SampleChapter() => new StoryChapter
        {
            chapterId = SampleChapterId,
            title = "8장 · 모래언덕",
            openingBeatId = "ch8_arrive",
            recap = new List<string>
            {
                "유적 아래에서 움직이는 빈칸을 봤다.",
                "그건 이름을 훔치는 그림자였다.",
                "그림자를 쫓아 텅 빈 들을 지나왔다.",
            },
            goal = "모래언덕의 상자 창고를 찾아라",
        };

        /// <summary>판 넘침 검수 — 줄이 많고 길다(세로 화면에서 줄마다 두세 줄로 접힌다).</summary>
        private static StoryChapter LongChapter() => new StoryChapter
        {
            chapterId = "qa_long",
            title = "12장 · 이름 없는 자리 (긴 제목 검수용 — 아주 아주 긴 장 제목)",
            openingBeatId = "qa_long_open",
            recap = new List<string>
            {
                "잿불 골짜기에서 먹이 장부의 마지막 쪽을 찢었다. 불씨가 하늘로 날아올랐다.",
                "우듬지 꼭대기에서 라온이 다시 돌아왔다. 이번엔 우리 편이었다.",
                "세라가 지도 뒷면에 적힌 이름 없는 자리의 위치를 읽어 냈다.",
                "명부회 관장이 모든 곤충의 이름을 지우려 한다는 걸 알게 됐다.",
                "어르신이 처음으로 옛날 챔피언 이야기를 들려줬다.",
                "모두와 함께 마지막 길을 나섰다. 돌아올 때는 이름을 되찾아 오기로 약속했다.",
            },
            goal = "이름 없는 자리 맨 안쪽까지 가서 관장을 만나고, 지워진 이름을 하나도 빠짐없이 되찾아라",
        };

        private static StoryBeat SampleBeat(string beatId) => new StoryBeat
        {
            beatId = string.IsNullOrEmpty(beatId) ? "qa_recap_open" : beatId,
            chapterId = SampleChapterId,
            speakerNpcId = "ruins_scholar",
            lines = new List<StoryLine>
            {
                new StoryLine { speaker = "세라", text = "여기가 모래언덕이야. 바람 냄새가 달라." },
                new StoryLine { speaker = "라온", text = "상자 창고는 저 언덕 너머래!" },
            },
        };

        /// <summary>대치 비트가 데이터에 없을 때 — 마지막 줄 뒤 곧바로 간부전.</summary>
        private static StoryBeat SampleDuelBeat() => new StoryBeat
        {
            beatId = "qa_duel_after",
            chapterId = SampleChapterId,
            speakerNpcId = "ledger_grip",
            duelAfter = "ledger_grip",
            lines = new List<StoryLine>
            {
                new StoryLine { speaker = "집게", text = "상자는 우리 거다. 돌아가라." },
                new StoryLine { speaker = "라온", text = "안 돼! 그 안에 곤충이 있잖아!" },
                new StoryLine { speaker = "집게", text = "상자를 열고 싶으면 실력으로 와라.", fx = "dark" },
            },
        };

        /// <summary>선택지가 붙은 비트여도 닫는다 — 선택 전 닫기를 삼키는 가드(choiceResolved)를 지나간다(OnDisable과 같은 길).</summary>
        private static void ForceClose(NpcDialogueUI ui)
        {
            SetField(ui, "choiceResolved", true);
            ui.CloseModal();
        }

        /// <summary>장 데이터가 이 비트를 모를 때 카드만 세운다(진짜 경로는 <c>NpcDialogueUI.OpenStory</c>).</summary>
        private static void ShowRecapCard(NpcDialogueUI ui, StoryChapter chapter)
        {
            SetField(ui, "recapChapter", chapter);
            SetField(ui, "recapShowing", true);
            SetField(ui, "recapShownAt", Time.unscaledTime);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, Private);
            if (f == null) throw new MissingFieldException(target.GetType().Name, name);
            f.SetValue(target, value);
        }

        /// <summary>
        /// 트래커의 「왜」를 채운다 — 쓰기 가능한 <c>Why</c> 속성이 있으면 그걸, 없으면 이름에 why가 든 string 필드.
        /// 트래커 쪽 구현(game-designer)이 바뀌어도 검수가 통째로 서지 않게 이름을 하나로 못 박지 않는다.
        /// </summary>
        private static bool SetWhy(StoryObjectiveTracker tracker, string value)
        {
            Type type = typeof(StoryObjectiveTracker);
            PropertyInfo prop = type.GetProperty("Why", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo setter = prop != null ? prop.GetSetMethod(true) : null;
            if (setter != null) setter.Invoke(tracker, new object[] { value });
            else
            {
                foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType != typeof(string) || f.Name.IndexOf("why", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    f.SetValue(tracker, value);
                    break;
                }
            }
            return string.Equals(tracker.Why ?? string.Empty, value ?? string.Empty, StringComparison.Ordinal);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Capture(string output, string name, List<string> shots)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), shot.EncodeToPNG());
            UnityEngine.Object.Destroy(shot);
            shots.Add(name);
        }
    }

    /// <summary>
    /// 검수 전용 — 퀘스트 칩 대역(진짜 칩은 로그인 세션이 있어야 그려진다) + 그 아래 <b>진짜</b> 목표 행.
    /// 칩 자리는 실제와 같은 순수 계산(<see cref="QuestChipLayout.ChipRect"/>)이고, 목표 행 높이는 「왜」 유무를 따른다.
    /// </summary>
    internal class RecapQaQuestStack : MonoBehaviour
    {
        private TutorialQuestUI questUi;
        private StoryObjectiveTracker tracker;
        private GUIStyle chipStyle;

        public void Bind(TutorialQuestUI ui, StoryObjectiveTracker objective)
        {
            questUi = ui;
            tracker = objective;
        }

        private void OnGUI()
        {
            if (questUi == null || tracker == null) return;
            UIScale.Begin();
            if (chipStyle == null)
            {
                chipStyle = new GUIStyle(GUI.skin.label)
                { fontSize = QuestChipLayout.TitleFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                chipStyle.normal.textColor = UITheme.Instance.accentAmber;
            }
            HudFrame f = HudFrame.Current;
            bool hasWhy = !string.IsNullOrEmpty(tracker.Why);
            Rect chip = QuestChipLayout.ChipRect(f, QuestChipLayout.ExpandedHeight, QuestChipLayout.RowHeightFor(hasWhy));
            UISurface.HudCard(chip);
            UIHelper.LabelFit(new Rect(chip.x + UITheme.Space.S, chip.y + UITheme.Space.S, chip.width - UITheme.Space.S * 2f,
                Mathf.Ceil(QuestChipLayout.TitleFontSize * 1.35f)), "★ 퀘스트 칩 자리(검수용)", chipStyle);
            questUi.DrawObjectiveRowForCapture(chip, QuestChipLayout.StackWidth(f));
            UIScale.End();
        }
    }
}
#endif
