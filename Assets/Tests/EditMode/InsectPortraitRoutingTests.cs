#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 곤충 ID → 2D 초상 분기의 부분문자열 오매칭 방어.
    ///
    /// 이 저장소에는 `id.Contains(...)` 사슬로 곤충 그림을 고르는 자리가 **5곳**에 흩어져 있다
    /// (포획 팝업·도감·1v1 배틀·레이드 2곳). 각자 독립적으로 자라다 보니 한 곳에만 가드가
    /// 들어가고 나머지가 빠지는 일이 실제로 벌어졌다 — 2026-08-07에 `"beetle"`이 `"bee"`를
    /// 품는 탓에 **딱정벌레 31종(사슴벌레·장수풍뎅이·헤라클레스 포함)이 4개 화면에서 벌로
    /// 그려지고 있었다.** `InsectEntity.BuildModel`만 `&amp;&amp; !id.Contains("beetle")` 가드를 갖고 있었다.
    ///
    /// 분기 사슬 자체는 각 화면의 렌더 코드라 여기서 실행할 수 없다(IMGUI). 대신
    /// **소스에서 조건을 읽어** 위험한 순서가 되살아나면 잡는다 — 규칙이 5곳에 복제돼 있는 한
    /// 이 방식이 가장 싸게 전부를 덮는다.
    /// </summary>
    [TestFixture]
    public class InsectPortraitRoutingTests
    {
        // "bee" 분기를 가진 파일들. 새 화면이 생기면 여기 추가한다.
        private static readonly string[] DispatchFiles =
        {
            "Scripts/UI/CapturePopupUI.cs",
            "Scripts/Dex/DexScreenUI.cs",
            "Scripts/UI/BattleScreenUI.cs",
            "Scripts/UI/RaidBattleUI.Draw.cs",
        };

        // "bee"보다 뒤에 오면 가려지는 딱정벌레 계열 키워드.
        private static readonly string[] BeetleKeywords = { "stag", "rhinoceros", "hercules", "longhorn" };

        [Test]
        public void EveryBeeBranch_GuardsAgainstBeetle()
        {
            int checkedBranches = 0;
            foreach (string rel in DispatchFiles)
            {
                string path = Path.Combine(Application.dataPath, rel);
                Assert.IsTrue(File.Exists(path), $"{rel}: 파일을 못 찾았다 — 경로가 바뀌었으면 이 테스트도 고칠 것");
                string src = File.ReadAllText(path);

                foreach (Match m in Regex.Matches(src, @"id\.Contains\(""bee""\)"))
                {
                    checkedBranches++;
                    // 같은 조건식 안에 beetle 제외 가드가 있어야 한다. 조건식은 그 줄에 다 들어 있다.
                    int lineStart = src.LastIndexOf('\n', m.Index) + 1;
                    int lineEnd = src.IndexOf('\n', m.Index);
                    if (lineEnd < 0) lineEnd = src.Length;
                    string line = src.Substring(lineStart, lineEnd - lineStart);

                    Assert.IsTrue(line.Contains("!id.Contains(\"beetle\")"),
                        $"{rel}: `id.Contains(\"bee\")` 분기에 beetle 제외 가드가 없다 — " +
                        $"딱정벌레가 벌로 그려진다.\n    {line.Trim()}");
                }
            }

            Assert.Greater(checkedBranches, 0,
                "bee 분기를 하나도 못 찾았다 — 분기 형태가 바뀌었으면 이 테스트도 함께 고칠 것");
        }

        [Test]
        public void BeetleKeywords_AppearInDispatch_SoGuardMatters()
        {
            // 가드가 의미를 가지려면 딱정벌레 계열 분기가 실제로 존재해야 한다.
            // 없다면 가드만 남고 라우팅이 사라진 것이므로 이 테스트가 그걸 알린다.
            foreach (string rel in DispatchFiles)
            {
                string path = Path.Combine(Application.dataPath, rel);
                if (!File.Exists(path)) continue;
                string src = File.ReadAllText(path);

                bool hasAny = false;
                foreach (string kw in BeetleKeywords)
                {
                    if (src.Contains($"id.Contains(\"{kw}\")")) { hasAny = true; break; }
                }
                Assert.IsTrue(hasAny,
                    $"{rel}: 딱정벌레 계열 분기(stag/rhinoceros/hercules/longhorn)가 하나도 없다");
            }
        }

        [Test]
        public void KnownBeetleIds_WouldNotRouteToBee()
        {
            // 대표 딱정벌레 ID가 가드에 실제로 걸리는지 문자열 수준에서 확인.
            // (분기 사슬 실행이 아니라 가드 조건의 의미 검증 — 회귀 시 위 테스트와 함께 울린다.)
            string[] beetles =
            {
                "stag_beetle", "rhinoceros_beetle", "beetle_hercules", "beetle_longhorn_oak",
                "diving_beetle_great", "jewel_beetle_gold", "beetle_basic", "beetle_husk",
            };
            foreach (string id in beetles)
            {
                Assert.IsTrue(id.Contains("bee"), $"{id}: 전제가 깨졌다 — beetle이 bee를 품지 않는다");
                Assert.IsTrue(id.Contains("beetle"), $"{id}: beetle 키워드가 없어 가드가 안 걸린다");
            }

            // 진짜 벌은 가드에 걸리지 않아야 한다.
            string[] bees = { "bee_worker", "bee_bumble", "bee_carpenter", "bee_queen", "bee_digger", "bee_stingless" };
            foreach (string id in bees)
            {
                Assert.IsFalse(id.Contains("beetle"), $"{id}: 진짜 벌인데 가드에 걸린다");
            }
        }

        [Test]
        public void AllInsectIds_BeeNamingRuleHolds()
        {
            // 벌 ID는 bee_ 접두여야 한다 — 그래야 가드(!beetle)만으로 벌과 딱정벌레가 갈린다.
            HashSet<string> ids = new HashSet<string>();
            foreach (Data.InsectSeed s in Data.InsectExpansionDefinitions.CreateAll()) ids.Add(s.id);
            foreach (Data.InsectSeed s in Data.InsectExpansion2Definitions.CreateAll()) ids.Add(s.id);

            foreach (string id in ids)
            {
                if (!id.Contains("bee") || id.Contains("beetle")) continue;
                Assert.IsTrue(id.StartsWith("bee_") || id.EndsWith("_bee") || id.Contains("_bee_"),
                    $"{id}: 벌인데 bee_ 접두가 아니다 — 초상 분기가 갈리지 않는다");
            }
        }
    }
}
#endif
