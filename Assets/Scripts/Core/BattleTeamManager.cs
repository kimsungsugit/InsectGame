using System;
using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    [Serializable]
    public class BattleTeamSave
    {
        public List<string> slotIds = new List<string>();
    }

    public class BattleTeamManager : MonoBehaviour, ICloudReloadable
    {
        public const int MaxSlots = GameConstants.Battle.MaxTeamSlots;

        [SerializeField] private PlayerInsectCollection collection;

        private BattleTeamSave saveData;

        public event Action TeamChanged;

        public int FilledSlots
        {
            get
            {
                if (saveData == null) return 0;
                int count = 0;
                foreach (var id in saveData.slotIds)
                    if (!string.IsNullOrEmpty(id)) count++;
                return count;
            }
        }

        private void Awake()
        {
            saveData = Load();
            while (saveData.slotIds.Count < MaxSlots)
                saveData.slotIds.Add(string.Empty);

            MigrateLegacySlots();
        }

        public string GetSlot(int index)
        {
            if (saveData == null || index < 0 || index >= MaxSlots) return null;
            string id = saveData.slotIds[index];
            return string.IsNullOrEmpty(id) ? null : id;
        }

        public bool SetSlot(int index, string instanceId)
        {
            if (index < 0 || index >= MaxSlots) return false;

            string normalizedId = collection != null ? collection.ResolveLegacyOrInstanceId(instanceId) : instanceId;

            if (!string.IsNullOrEmpty(normalizedId))
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (i != index && saveData.slotIds[i] == normalizedId)
                        return false;
                }
            }

            string newValue = normalizedId ?? string.Empty;
            // no-op 가드 — 같은 값 재설정 시 TeamChanged 발화 안 함.
            // 옛은 TutorialQuestManager가 TeamChanged 구독해 q_team 진행도 중복 가산 위험.
            if (saveData.slotIds[index] == newValue) return true;

            saveData.slotIds[index] = newValue;
            Save(saveData);
            TeamChanged?.Invoke();
            return true;
        }

        public bool RemoveSlot(int index)
        {
            return SetSlot(index, null);
        }

        public bool IsInTeam(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || saveData == null) return false;
            string normalizedId = collection != null ? collection.ResolveLegacyOrInstanceId(instanceId) : instanceId;
            return saveData.slotIds.Contains(normalizedId);
        }

        public List<string> GetAllSlots()
        {
            return new List<string>(saveData.slotIds);
        }

        public bool HasAnyInsect()
        {
            return FilledSlots > 0;
        }

        public void AutoWire(PlayerInsectCollection col)
        {
            if (collection == null) collection = col;
            MigrateLegacySlots();
            SeatFirstOwnedInsect();
            SubscribeEvents();
        }

        // 구독을 메서드로 뺀 것은 OnEnable에서 되살리기 위해서다 —
        // `-=` 뒤 `+=`라 중복 구독이 되지 않는다(rules/ui-layout.md의 구독 회귀 계열).
        private void SubscribeEvents()
        {
            if (collection == null) return;
            collection.InsectCaptured -= OnInsectObtained;
            collection.InsectCaptured += OnInsectObtained;
        }

        private void OnEnable() => SubscribeEvents();

        private void OnDisable()
        {
            if (collection != null) collection.InsectCaptured -= OnInsectObtained;
        }

        /// <summary>
        /// <b>첫 곤충은 자동으로 1번 슬롯에 앉는다.</b> 팀이 비어 있을 때만 — 그 뒤로는
        /// 플레이어가 편성한다.
        ///
        /// 이 규칙이 왜 여기 있나: 예전엔 <c>TutorialQuestManager.CompleteQuest</c>의 곤충 보상
        /// 블록 안에 있었는데, 첫 파트너 지급이 퀘스트에서 <c>ch1_intro</c> 비트로 옮겨가면서
        /// (그쪽은 <c>StoryDirector.GrantReward</c>가 처리한다) 그 코드가 통째로 죽었다 —
        /// 튜토리얼 퀘스트에 <c>rewardInsectId</c>가 하나도 남지 않았기 때문이다.
        ///
        /// 그래서 <b>배틀팀이 영원히 비었다</b>. <c>CaptureChoiceUI</c>의 [B] 배틀은
        /// <c>HasAnyInsect()</c>만 보고 버튼과 키 입력을 함께 막으므로(컬렉션 폴백이 없다),
        /// <c>q_battle</c>(첫 전투)에서 튜토리얼이 멈춘다. 팀 편성을 가르치는 <c>q_team</c>은
        /// 그보다 <b>세 단계 뒤</b>라 안내조차 없다. 스토리도 함께 멈춘다(<c>ch1_first_battle</c>·
        /// <c>ch1_guardian_call</c>이 BattleWin).
        ///
        /// 지급 경로(포획·전투·레이드·가챠·퀘스트·스토리) 어디에 붙여도 같은 일을 하므로
        /// <b>지급이 모이는 이벤트 한 곳</b>에 둔다 — 경로마다 복제하면 새 경로가 생길 때
        /// 또 빠뜨린다(도감 등록이 정확히 그렇게 6곳으로 흩어져 <c>dex_grant_lint</c>가 필요해졌다).
        /// </summary>
        private void OnInsectObtained(PlayerInsectData insect)
        {
            if (insect == null || string.IsNullOrEmpty(insect.instanceId)) return;
            if (HasAnyInsect()) return;
            SetSlot(0, insect.instanceId);
        }

        /// <summary>
        /// <b>곤충은 있는데 팀이 빈 세이브를 구제한다.</b> <see cref="OnInsectObtained"/>는
        /// <b>앞으로의 획득</b>만 덮는다 — 그 결함을 이미 겪은 세이브(첫 파트너를 받았는데
        /// 자동 배치 코드가 죽어 있던 빌드로 플레이한 계정)는 업데이트해도 팀이 그대로 비어
        /// 있고, <c>CaptureChoiceUI</c>의 [B]가 여전히 잠긴 채 <c>q_battle</c>에서 멈춘다.
        /// 새 곤충을 하나 더 잡으면 풀리지만, 그 포획을 하려면 전투를 지나야 하는 게 아니라서
        /// 운 좋게 벗어나는 것뿐이다.
        ///
        /// <b>TeamChanged를 발화하지 않는다</b> — 사용자 액션이 아니라 시스템 정리라
        /// <c>MigrateLegacySlots</c>와 같은 취급이다(팀 UI는 매 프레임 <c>GetAllSlots</c>로 읽는다).
        ///
        /// 팀을 일부러 비워 둔 플레이어는 다음 부팅에 1번 슬롯이 다시 채워진다. 빈 팀은 전투 자체를
        /// 막으므로 그 편이 낫다고 봤다.
        /// </summary>
        private void SeatFirstOwnedInsect()
        {
            if (collection == null || saveData == null) return;
            if (HasAnyInsect()) return;

            IReadOnlyList<PlayerInsectData> owned = collection.OwnedView;
            if (owned == null) return;

            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData d = owned[i];
                if (d == null || string.IsNullOrEmpty(d.instanceId)) continue;

                saveData.slotIds[0] = d.instanceId;
                Save(saveData);
                Debug.Log($"[BattleTeam] 빈 팀에 {d.instanceId} 자동 편성 — 곤충은 있는데 팀이 비어 있었다");
                return;
            }
        }

        // 클라우드 로드 후 battle_team.json을 다시 읽어 슬롯 갱신.
        // TeamChanged는 발화하지 않음 — TutorialQuestManager가 q_team 진행도를 잘못 가산하는 회귀 차단.
        // 팀 UI(IMGUI)는 매 프레임 GetAllSlots로 읽어 자동 반영.
        public void ReloadFromDisk()
        {
            saveData = Load();
            while (saveData.slotIds.Count < MaxSlots)
                saveData.slotIds.Add(string.Empty);
            MigrateLegacySlots();
            // 다른 기기의 곤충이 방금 내려왔는데 팀이 비어 있을 수 있다 — 컬렉션이
            // 이 클래스보다 **먼저** 등록돼 있어 여기서는 이미 갱신된 목록을 본다.
            SeatFirstOwnedInsect();
        }

        private void MigrateLegacySlots()
        {
            if (saveData == null || collection == null)
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < saveData.slotIds.Count; i++)
            {
                string resolved = collection.ResolveLegacyOrInstanceId(saveData.slotIds[i]);
                if (resolved != saveData.slotIds[i])
                {
                    saveData.slotIds[i] = resolved ?? string.Empty;
                    changed = true;
                }
            }

            if (changed)
            {
                Save(saveData);
                // 마이그레이션은 시스템 내부 정리 — 사용자 액션 아님. TeamChanged 발화 시 TutorialQuestManager가
                // q_team 진행도 자동 가산하는 회귀 차단. UI는 다음 사용자 SetSlot 또는 첫 OnGUI 호출에서 갱신.
            }
        }

        private BattleTeamSave Load()
        {
            string path = GetPath();
            if (!System.IO.File.Exists(path))
                return new BattleTeamSave();
            try
            {
                string json = System.IO.File.ReadAllText(path);
                return JsonUtility.FromJson<BattleTeamSave>(json) ?? new BattleTeamSave();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BattleTeamManager] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new BattleTeamSave();
            }
        }

        private void Save(BattleTeamSave data)
        {
            string json = JsonUtility.ToJson(data, true);
            AtomicFileWriter.WriteAllText(GetPath(), json);
        }

        private string GetPath()
        {
            return SaveScope.FilePath(GameConstants.SaveFiles.BattleTeam);
        }
    }
}
