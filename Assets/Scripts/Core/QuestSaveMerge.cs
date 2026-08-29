using System.Collections.Generic;

namespace InsectGame.Core
{
    /// <summary>
    /// 퀘스트 세이브의 <b>로컬 ↔ 클라우드 병합</b>. 순수 문자열 계산부라 테스트로 고정한다
    /// (<c>QuestSaveMergeTests</c>).
    ///
    /// <b>왜 병합인가 — 덮어쓰기가 진행을 지웠다.</b> <see cref="CloudSaveManager"/>의 부트 로드는
    /// "빈 클라우드 필드는 로컬을 보존한다"가 원칙이다(gems의 음수 sentinel, charStarter의 빈 문자열,
    /// <c>ApplyCloudFile</c>의 <c>forceReplace</c>가 전부 그 형태다). 그런데 퀘스트 5개 필드만
    /// <c>?? ""</c>로 null만 막고 <b>빈 문자열은 그대로 덮어썼다</b> — 클라우드가 낡았거나
    /// (오프라인·PATCH 실패) 그 필드가 없던 시절 문서면 <b>로그인할 때마다 깬 퀘스트가 미완료로
    /// 되돌아간다</b>. 예외도 경고도 없다.
    ///
    /// <b>퀘스트 완료는 단조 증가다</b> — 한 번 깬 것이 미완료로 돌아갈 이유가 없다. 그래서 부트
    /// 로드에서는 합집합·최댓값으로 병합한다. 사용자가 충돌 화면에서 "클라우드 사용"을 고른
    /// 경우(<c>forceReplace</c>)만 통째로 치환한다 — 그건 되돌리겠다는 명시적 의사다.
    /// </summary>
    public static class QuestSaveMerge
    {
        /// <summary>
        /// 쉼표 구분 ID 집합의 합집합. 로컬 순서를 유지하고 클라우드에만 있는 것을 뒤에 붙인다
        /// (완료 퀘스트 목록 — <c>QuestCompleted</c>).
        /// </summary>
        public static string UnionCsv(string local, string cloud)
        {
            List<string> merged = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            AppendCsv(local, merged, seen);
            AppendCsv(cloud, merged, seen);
            return string.Join(",", merged);
        }

        private static void AppendCsv(string source, List<string> into, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(source)) return;
            foreach (string raw in source.Split(','))
            {
                string id = raw.Trim();
                if (id.Length == 0 || !seen.Add(id)) continue;
                into.Add(id);
            }
        }

        /// <summary>
        /// <c>"key:int,key:int"</c> 사전의 키별 최댓값 병합(진행 카운트·반복 횟수).
        ///
        /// 반복 서브 퀘스트는 완료할 때 진행을 0으로 되돌리고 반복 횟수를 올린다. 그래서 한쪽이
        /// 0(방금 완료)이고 다른 쪽이 3(진행 중)이면 3이 남아 <b>약간 유리하게</b> 기운다 —
        /// 의도한 방향이다. 어긋남의 대가가 "조금 덜 남은 진행"인 쪽이, 깬 것이 사라지는 쪽보다 낫다.
        /// </summary>
        public static string MaxIntDict(string local, string cloud)
        {
            List<string> order = new List<string>();
            Dictionary<string, int> merged = new Dictionary<string, int>();
            AppendIntDict(local, order, merged);
            AppendIntDict(cloud, order, merged);

            List<string> entries = new List<string>(order.Count);
            foreach (string key in order) entries.Add(key + ":" + merged[key]);
            return string.Join(",", entries);
        }

        private static void AppendIntDict(string source, List<string> order, Dictionary<string, int> into)
        {
            if (string.IsNullOrEmpty(source)) return;
            foreach (string entry in source.Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2) continue;
                string key = parts[0].Trim();
                if (key.Length == 0 || !int.TryParse(parts[1], out int value)) continue;

                if (into.TryGetValue(key, out int existing))
                {
                    if (value > existing) into[key] = value;
                    continue;
                }
                into[key] = value;
                order.Add(key);
            }
        }

        /// <summary>
        /// 활성 퀘스트 ID — 클라우드가 비어 있으면 로컬을 지킨다.
        ///
        /// 클라우드 값이 <b>병합된 완료 목록에 이미 든 퀘스트</b>여도 그대로 둔다.
        /// <see cref="TutorialQuestManager.ReloadFromDisk"/>가 그 경우
        /// <c>ActivateNextQuest</c>로 다음 지점을 재선정하기 때문이다 — 판정에 필요한
        /// 퀘스트 배열 순서는 여기가 아니라 그쪽만 안다.
        /// </summary>
        public static string PreferCloudActive(string local, string cloud)
        {
            return string.IsNullOrEmpty(cloud) ? (local ?? "") : cloud;
        }
    }
}
