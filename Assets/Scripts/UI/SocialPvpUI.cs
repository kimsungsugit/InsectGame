using System;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class SocialPvpUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private SocialPvpManager manager;

        private enum Page { Friends, Ranked, Battle }
        private Page page;
        private bool isOpen;
        private string friendCodeInput = string.Empty;
        private readonly Vector2[] pageScrollPositions = new Vector2[3];
        private readonly UIDirectScroll[] pageDirectScrolls =
        {
            new UIDirectScroll(),
            new UIDirectScroll(),
            new UIDirectScroll()
        };
        private readonly float[] pageContentHeights = new float[3];

        // 매치 상태 전이 감지용 — OnStateChanged가 폴링마다 호출되므로 edge를 직접 기억한다.
        private string lastMatchId = string.Empty;
        private string lastMatchStatus = string.Empty;

        // 팀 유효성 캐시 — HasValidTeam이 비싸서 매 OnGUI 호출하면 안 된다(RefreshTeamState 참조).
        private bool teamReady;
        private string teamReason = string.Empty;
        private float nextTeamCheck;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle cardStyle;
        private GUIStyle centeredStyle;
        private GUIStyle smallStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle skillButtonStyle;
        private GUIStyle textFieldStyle;
        private Texture2D cardTexture;

        public bool IsOpen => isOpen;

        private void OnEnable()
        {
            if (manager != null) manager.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (manager != null) manager.StateChanged -= OnStateChanged;
            ResetAllPageScrolls();
            ModalUIRegistry.Unregister(this);
        }

        public void AutoWire(SocialPvpManager socialManager)
        {
            if (manager == socialManager) return;
            if (isActiveAndEnabled && manager != null) manager.StateChanged -= OnStateChanged;
            manager = socialManager;
            if (isActiveAndEnabled && manager != null) manager.StateChanged += OnStateChanged;
        }

        public void Toggle()
        {
            isOpen = !isOpen;
            ResetAllPageScrolls();
            if (isOpen)
            {
                ModalUIRegistry.Register(this);
                if (manager != null) manager.RefreshAll();
            }
            else ModalUIRegistry.Unregister(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            ResetAllPageScrolls();
            ModalUIRegistry.Unregister(this);
        }

        private void OnStateChanged()
        {
            // "새로 active가 된 순간"에만 배틀 탭으로 보낸다.
            // 옛 코드는 level-trigger라 매치 중 폴링(2.5초)마다 page를 Battle로 되돌렸고,
            // 그동안 친구/랭크 탭에 머무를 수 없었다(친구 요청 수락·랭킹 확인 불가).
            PvpMatchState match = manager != null ? manager.CurrentMatch : null;
            string id = match != null ? match.matchId : string.Empty;
            string status = match != null ? match.status : string.Empty;

            bool becameActive = status == "active"
                && (id != lastMatchId || lastMatchStatus != "active");
            bool matchContextChanged = id != lastMatchId || status != lastMatchStatus;

            lastMatchId = id;
            lastMatchStatus = status;

            if (becameActive)
                SetPage(Page.Battle);
            else if (matchContextChanged && page == Page.Battle)
                ResetPageScroll(Page.Battle);
        }

        /// <summary>
        /// 팀 유효성을 0.5초 간격으로만 재계산해 캐시한다.
        /// </summary>
        /// <remarks>
        /// HasValidTeam은 BuildTeamSnapshot을 돌려 List·배열을 여러 개 할당하고(전부 class라
        /// 진짜 힙 할당이다) 팀이 미완성이면 예외까지 던진다(스택 트레이스 캡처).
        /// OnGUI는 프레임당 Layout+Repaint로 두 번 이상 호출되므로 매 호출 재계산은 낭비다.
        /// 팀 편성은 이 화면 밖에서 바뀌므로 0.5초 지연은 체감되지 않는다.
        /// </remarks>
        private void RefreshTeamState()
        {
            if (manager == null)
            {
                teamReady = false;
                teamReason = "프로필 동기화 필요";
                return;
            }
            if (Time.unscaledTime < nextTeamCheck) return;
            nextTeamCheck = Time.unscaledTime + 0.5f;
            teamReady = manager.HasValidTeam(out teamReason);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            titleStyle.normal.textColor = new Color(1f, 0.84f, 0.3f);
            sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 31, fontStyle = FontStyle.Bold };
            sectionStyle.normal.textColor = new Color(0.45f, 0.85f, 1f);
            centeredStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true };
            smallStyle.normal.textColor = new Color(0.75f, 0.78f, 0.84f);
            // 기본 라벨/버튼/입력필드도 세로 모바일에서 읽히도록 캐시 스타일로 폰트만 키움
            // (색상은 skin 기본값 그대로 복사 — 변경 없음). 매 프레임 new 금지 캐시 패턴.
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 23 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            skillButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip,
                padding = new RectOffset(10, 10, 8, 8)
            };
            textFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 23 };
            cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 12, 12) };
            cardTexture = new Texture2D(1, 1);
            cardTexture.SetPixel(0, 0, new Color(0.08f, 0.11f, 0.17f, 0.96f));
            cardTexture.Apply();
            cardStyle.normal.background = cardTexture;
        }

        private void OnGUI()
        {
            if (!isOpen) return;
            EnsureStyles();
            UIScale.Begin();
            Rect panel = UISafeLayout.CenteredPanel(1120f, 850f);
            float width = panel.width;
            float height = panel.height;
            float x = panel.x;
            float y = panel.y;
            GUI.color = new Color(0.025f, 0.04f, 0.075f, 0.98f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Rect contentArea = new Rect(x + 18f, y + 12f, width - 36f, height - 24f);
            GUILayout.BeginArea(contentArea);
            DrawHeader();
            DrawTabs();
            GUILayout.Space(8f);
            DrawScrollablePage(contentArea);
            DrawStatus();
            GUILayout.EndArea();
            UIScale.End();
        }

        private void DrawHeader()
        {
            float buttonH = UIScale.IsMobileLayout ? 60f : 48f;
            GUILayout.BeginHorizontal();
            GUILayout.Label(UIScale.IsMobileLayout ? "친구 · 3:3 배틀" : "FRIENDS & 3:3 BATTLE",
                titleStyle, GUILayout.Height(UIScale.IsMobileLayout ? 66f : 56f));
            if (GUILayout.Button("새로고침", buttonStyle, GUILayout.Width(160f), GUILayout.Height(buttonH)) && manager != null)
            {
                ResetPageScroll(page);
                manager.RefreshAll();
            }
            if (GUILayout.Button("X", buttonStyle, GUILayout.Width(64f), GUILayout.Height(buttonH))) CloseModal();
            GUILayout.EndHorizontal();
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            DrawTab("친구", Page.Friends);
            DrawTab("랭크전", Page.Ranked);
            string battleLabel = manager != null && manager.CurrentMatch != null ? "배틀 ●" : "배틀";
            DrawTab(battleLabel, Page.Battle);
            GUILayout.EndHorizontal();
        }

        private void DrawTab(string text, Page target)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = page == target ? new Color(0.2f, 0.55f, 0.85f) : new Color(0.18f, 0.2f, 0.25f);
            if (GUILayout.Button(text, buttonStyle, GUILayout.Height(UIScale.IsMobileLayout ? 60f : 48f)))
                SetPage(target);
            GUI.backgroundColor = old;
        }

        private void SetPage(Page target)
        {
            if (page == target) return;
            page = target;
            ResetPageScroll(target);
        }

        private void ResetAllPageScrolls()
        {
            for (int i = 0; i < pageScrollPositions.Length; i++)
                ResetPageScroll((Page)i);
        }

        private void ResetPageScroll(Page target)
        {
            int index = (int)target;
            if (index < 0 || index >= pageScrollPositions.Length) return;
            pageScrollPositions[index] = Vector2.zero;
            pageContentHeights[index] = 0f;
            pageDirectScrolls[index].Reset();
        }

        /// <summary>직전 Repaint에 잰 페이지별 스크롤뷰 영역(패널 로컬 좌표). 터치 드래그 판정에만 쓴다.</summary>
        private readonly Rect[] pageViewports = new Rect[3];

        /// <summary>
        /// 페이지 콘텐츠를 스크롤 영역에 그린다.
        ///
        /// <b>레이아웃 스크롤뷰(<c>GUILayout.BeginScrollView</c>)를 쓴다.</b> 한때
        /// <c>GUI.BeginScrollView</c> + 그 안의 <c>GUILayout.BeginArea</c>로 좌표계를 직접 리셋했는데,
        /// <c>DrawPanel</c>이 이미 <c>GUILayout.BeginArea(contentArea)</c>를 열어 둔 상태라 <b>Area 중첩</b>이
        /// 됐다. Unity는 Area 중첩을 지원하지 않는다 — 레이아웃 그룹 스택이 어긋나 <b>탭 버튼은
        /// 그려지는데 그 아래 내용이 하나도 나오지 않는다</b>. <c>CashShopUI</c>가 같은 형태로 실제
        /// 증상을 냈고(상점 3탭 전부 빈칸), 같은 커밋이 이쪽에도 같은 구조를 심었다.
        /// </summary>
        private void DrawScrollablePage(Rect contentArea)
        {
            int index = (int)page;

            // 터치 드래그(UIDirectScroll)는 **화면 좌표** 뷰포트가 필요한데 레이아웃 스크롤뷰는
            // 자기 Rect를 돌려주지 않는다. 직전 Repaint에 재둔 값을 쓴다 — 한 프레임 늦지만
            // 패널 크기는 매 프레임 바뀌지 않는다.
            Vector2 position = pageScrollPositions[index];
            Rect measured = pageViewports[index];
            if (measured.height > 1f)
            {
                Rect directViewport = new Rect(
                    contentArea.x + measured.x,
                    contentArea.y + measured.y,
                    measured.width,
                    measured.height);
                pageDirectScrolls[index].Handle(
                    ref position,
                    directViewport,
                    Mathf.Max(measured.height, pageContentHeights[index]),
                    UIScale.IsMobileLayout ? 72f : 52f);
            }

            position = GUILayout.BeginScrollView(
                position,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            GUILayout.BeginVertical();
            if (page == Page.Friends) DrawFriends();
            else if (page == Page.Ranked) DrawRanked();
            else DrawBattle();
            GUILayout.EndVertical();
            // 콘텐츠 높이 — 위 수직 그룹의 Rect가 곧 그려진 높이다(터치 드래그의 스크롤 한계용).
            if (Event.current.type == EventType.Repaint)
                pageContentHeights[index] = GUILayoutUtility.GetLastRect().height;

            GUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
                pageViewports[index] = GUILayoutUtility.GetLastRect();

            pageScrollPositions[index] = position;
        }

        private void DrawFriends()
        {
            if (manager == null) return;
            SocialPvpState state = manager.State;
            GUILayout.Label("내 친구 코드", sectionStyle);
            GUILayout.BeginVertical(cardStyle);
            string ownCode = state.profile != null ? state.profile.friendCode : "동기화 필요";
            GUILayout.Label(ownCode, titleStyle, GUILayout.Height(56f));
            GUILayout.Label("상대방에게 이 코드를 알려주거나, 아래에 상대 코드를 입력하세요.", centeredStyle);
            GUILayout.BeginHorizontal();
            friendCodeInput = GUILayout.TextField(friendCodeInput, 12, textFieldStyle, GUILayout.Height(50f));
            GUI.enabled = !manager.IsBusy && !string.IsNullOrWhiteSpace(friendCodeInput);
            if (GUILayout.Button("친구 요청", buttonStyle, GUILayout.Width(170f), GUILayout.Height(50f)))
            {
                manager.SendFriendRequest(friendCodeInput);
                friendCodeInput = string.Empty;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            FriendRequestSnapshot[] requests = state.incomingRequests ?? Array.Empty<FriendRequestSnapshot>();
            if (requests.Length > 0)
            {
                GUILayout.Space(12f);
                GUILayout.Label($"받은 친구 요청 ({requests.Length})", sectionStyle);
                foreach (FriendRequestSnapshot request in requests)
                {
                    if (request == null) continue;
                    GUILayout.BeginHorizontal(cardStyle);
                    GUILayout.Label($"{request.displayName}  Lv.{request.level}", labelStyle, GUILayout.Width(560f), GUILayout.Height(50f));
                    GUI.enabled = !manager.IsBusy;
                    if (GUILayout.Button("수락", buttonStyle, GUILayout.Width(120f), GUILayout.Height(50f)))
                        manager.RespondFriendRequest(request.requestId, true);
                    if (GUILayout.Button("거절", buttonStyle, GUILayout.Width(120f), GUILayout.Height(50f)))
                        manager.RespondFriendRequest(request.requestId, false);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            PvpChallengeSnapshot[] challenges = state.incomingChallenges ?? Array.Empty<PvpChallengeSnapshot>();
            if (challenges.Length > 0)
            {
                GUILayout.Space(12f);
                GUILayout.Label("친선 배틀 도전", sectionStyle);
                foreach (PvpChallengeSnapshot challenge in challenges)
                {
                    if (challenge == null) continue;
                    GUILayout.BeginHorizontal(cardStyle);
                    GUILayout.Label($"{challenge.displayName} · {challenge.rank} {challenge.rating}", labelStyle, GUILayout.Width(560f), GUILayout.Height(50f));
                    GUI.enabled = !manager.IsBusy;
                    if (GUILayout.Button("대결", buttonStyle, GUILayout.Width(120f), GUILayout.Height(50f)))
                        manager.RespondChallenge(challenge.challengeId, true);
                    if (GUILayout.Button("거절", buttonStyle, GUILayout.Width(120f), GUILayout.Height(50f)))
                        manager.RespondChallenge(challenge.challengeId, false);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(12f);
            PvpProfileSnapshot[] friends = state.friends ?? Array.Empty<PvpProfileSnapshot>();
            GUILayout.Label($"친구 ({friends.Length})", sectionStyle);
            if (friends.Length == 0) GUILayout.Label("아직 등록된 친구가 없습니다.", centeredStyle, GUILayout.Height(70f));
            foreach (PvpProfileSnapshot friend in friends)
            {
                if (friend == null) continue;
                GUILayout.BeginHorizontal(cardStyle);
                GUILayout.BeginVertical();
                GUILayout.Label($"{friend.displayName}  Lv.{friend.level}", labelStyle, GUILayout.Height(34f));
                GUILayout.Label($"{friend.rank} · {friend.rating}점 · {friend.wins}승 {friend.losses}패", smallStyle);
                GUILayout.EndVertical();
                GUI.enabled = !manager.IsBusy;
                if (GUILayout.Button("3:3 도전", buttonStyle, GUILayout.Width(170f), GUILayout.Height(56f)))
                    manager.ChallengeFriend(friend.uid);
                if (GUILayout.Button("삭제", buttonStyle, GUILayout.Width(100f), GUILayout.Height(56f)))
                    manager.RemoveFriend(friend.uid);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            PvpProfileSnapshot[] blocked = state.blockedUsers ?? Array.Empty<PvpProfileSnapshot>();
            if (blocked.Length > 0)
            {
                GUILayout.Space(18f);
                GUILayout.Label($"차단 관리 ({blocked.Length})", sectionStyle);
                foreach (PvpProfileSnapshot user in blocked)
                {
                    if (user == null) continue;
                    GUILayout.BeginHorizontal(cardStyle);
                    GUILayout.Label($"{user.displayName}  ·  채팅/친구/대전 차단됨",
                        labelStyle, GUILayout.Width(680f), GUILayout.Height(50f));
                    GUI.enabled = !manager.IsBusy;
                    if (GUILayout.Button("차단 해제", buttonStyle, GUILayout.Width(150f), GUILayout.Height(50f)))
                        manager.UnblockUser(user.uid);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawRanked()
        {
            if (manager == null) return;
            SocialPvpState state = manager.State;
            PvpProfileSnapshot profile = state.profile;
            GUILayout.BeginVertical(cardStyle);
            if (profile != null)
            {
                GUILayout.Label($"{profile.rank}  {profile.rating} RP", titleStyle, GUILayout.Height(56f));
                GUILayout.Label($"전적 {profile.wins}승 {profile.losses}패", centeredStyle);
            }
            else GUILayout.Label("프로필 동기화 필요", centeredStyle, GUILayout.Height(70f));
            GUILayout.Space(8f);
            RefreshTeamState();
            GUILayout.Label(teamReady ? "3:3 팀 준비 완료" : teamReason, centeredStyle);
            if (state.queued)
            {
                // 취소는 팀 상태와 무관해야 한다(CancelQueue는 팀을 쓰지 않는다).
                // 옛 코드는 `GUI.enabled = !IsBusy && teamReady`가 이 버튼까지 덮어,
                // 큐 대기 중 곤충을 방출하거나 편성을 풀면 teamReady=false가 되면서
                // 취소가 죽었다. queued는 서버 권위값이라 재접속해도 유지되므로
                // 큐에서 영영 빠져나올 수 없었다.
                GUI.enabled = !manager.IsBusy;
                GUI.backgroundColor = new Color(0.65f, 0.25f, 0.2f);
                if (GUILayout.Button("매칭 취소", buttonStyle, GUILayout.Height(60f))) manager.CancelQueue();
            }
            else
            {
                GUI.enabled = !manager.IsBusy && teamReady;
                GUI.backgroundColor = new Color(0.2f, 0.55f, 0.85f);
                if (GUILayout.Button("등급별 3:3 매칭 시작", buttonStyle, GUILayout.Height(60f))) manager.QueueRanked();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            if (state.queued) GUILayout.Label("비슷한 레이팅의 상대를 서버에서 찾는 중…", centeredStyle);
            GUILayout.EndVertical();

            GUILayout.Space(16f);
            GUILayout.Label("랭킹 TOP 20", sectionStyle);
            PvpProfileSnapshot[] board = state.leaderboard ?? Array.Empty<PvpProfileSnapshot>();
            for (int i = 0; i < board.Length; i++)
            {
                PvpProfileSnapshot entry = board[i];
                if (entry == null) continue;
                GUILayout.BeginHorizontal(cardStyle);
                GUILayout.Label($"#{i + 1}", labelStyle, GUILayout.Width(80f));
                GUILayout.Label(entry.displayName, labelStyle, GUILayout.Width(380f));
                GUILayout.Label(entry.rank, labelStyle, GUILayout.Width(180f));
                GUILayout.Label($"{entry.rating} RP", labelStyle, GUILayout.Width(150f));
                GUILayout.Label($"{entry.wins}승 {entry.losses}패", labelStyle);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawBattle()
        {
            if (manager == null || manager.CurrentMatch == null)
            {
                GUILayout.Label("진행 중인 배틀이 없습니다.", centeredStyle, GUILayout.Height(180f));
                GUILayout.Label("친구 탭에서 친선전을 신청하거나 랭크 매칭을 시작하세요.", centeredStyle);
                return;
            }
            PvpMatchState match = manager.CurrentMatch;
            string uid = AuthManager.Instance != null ? AuthManager.Instance.UserId : string.Empty;
            bool ownIsFirst = match.player1 != null && match.player1.uid == uid;
            PvpProfileSnapshot own = ownIsFirst ? match.player1 : match.player2;
            PvpProfileSnapshot enemy = ownIsFirst ? match.player2 : match.player1;
            PvpInsectSnapshot[] ownTeam = ownIsFirst ? match.team1 : match.team2;
            PvpInsectSnapshot[] enemyTeam = ownIsFirst ? match.team2 : match.team1;
            int ownActive = ownIsFirst ? match.active1 : match.active2;
            int enemyActive = ownIsFirst ? match.active2 : match.active1;

            GUILayout.Label(match.mode == "ranked" ? "RANKED 3:3" : "FRIEND 3:3", titleStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(own != null ? own.displayName : "나", centeredStyle, GUILayout.Width(510f));
            GUILayout.Label(enemy != null ? enemy.displayName : "상대", centeredStyle, GUILayout.Width(510f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawTeamColumn(ownTeam, ownActive, true);
            DrawTeamColumn(enemyTeam, enemyActive, false);
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            if (match.status == "finished")
            {
                string result = match.winnerUid == uid ? "승리" : "패배";
                GUILayout.Label(result, titleStyle, GUILayout.Height(64f));
            }
            else
            {
                bool ownTurn = match.turnUid == uid;
                GUILayout.Label(ownTurn ? "내 턴 — 행동을 선택하세요" : "상대 턴 — 서버 응답 대기 중", centeredStyle, GUILayout.Height(46f));
                GUI.enabled = ownTurn && !manager.IsBusy;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("기본 공격", buttonStyle, GUILayout.Height(56f))) manager.SubmitBasicAttack();
                PvpInsectSnapshot active = GetMember(ownTeam, ownActive);
                PvpSkillSnapshot[] skills = active != null && active.skills != null
                    ? active.skills : Array.Empty<PvpSkillSnapshot>();
                bool mobile = UIScale.IsMobileLayout;
                skillButtonStyle.fontSize = mobile ? 27 : 25;
                float skillButtonH = SkillUILayout.GetTouchHeight(mobile, 82f, 92f);
                for (int i = 0; i < skills.Length; i++)
                {
                    int index = i;
                    PvpSkillSnapshot skill = skills[i];
                    string effect = skill != null && skill.effectType == 1 ? "공격 강화"
                        : skill != null && skill.effectType == 2 ? "공격 약화"
                        : skill != null ? $"위력 {skill.power}" : string.Empty;
                    string label = skill != null ? $"{skill.displayName}\n{effect}" : "-";
                    if (GUILayout.Button(label, skillButtonStyle, GUILayout.Height(skillButtonH)))
                        manager.SubmitSkill(index);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("교체", labelStyle, GUILayout.Width(80f));
                if (ownTeam != null)
                {
                    for (int i = 0; i < ownTeam.Length; i++)
                    {
                        PvpInsectSnapshot member = ownTeam[i];
                        GUI.enabled = ownTurn && !manager.IsBusy && i != ownActive && member != null && member.hp > 0;
                        int slot = i;
                        if (GUILayout.Button(member != null ? member.displayName : "-", buttonStyle, GUILayout.Height(56f)))
                            manager.SwitchInsect(slot);
                    }
                }
                GUI.enabled = true;
                if (GUILayout.Button("기권", buttonStyle, GUILayout.Width(110f), GUILayout.Height(56f))) manager.Surrender();
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }

            GUILayout.Space(12f);
            GUILayout.Label("배틀 로그", sectionStyle);
            string[] logs = match.log ?? Array.Empty<string>();
            foreach (string line in logs) GUILayout.Label("• " + line, smallStyle);
        }

        private void DrawTeamColumn(PvpInsectSnapshot[] team, int active, bool own)
        {
            GUILayout.BeginVertical(GUILayout.Width(520f));
            if (team != null)
            {
                for (int i = 0; i < team.Length; i++)
                {
                    PvpInsectSnapshot member = team[i];
                    if (member == null) continue;
                    GUILayout.BeginVertical(cardStyle, GUILayout.Height(116f));
                    string marker = i == active ? "▶ " : "";
                    GUILayout.Label($"{marker}{member.displayName}  Lv.{member.level}", labelStyle);
                    float ratio = member.maxHp > 0 ? Mathf.Clamp01(member.hp / (float)member.maxHp) : 0f;
                    Rect bar = GUILayoutUtility.GetRect(470f, 22f);
                    GUI.color = new Color(0.18f, 0.2f, 0.24f);
                    GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = ratio > 0.35f ? new Color(0.25f, 0.8f, 0.4f) : new Color(0.9f, 0.3f, 0.25f);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUILayout.Label(own ? $"HP {member.hp}/{member.maxHp}" : $"HP {Mathf.RoundToInt(ratio * 100f)}%", smallStyle);
                    GUILayout.EndVertical();
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawStatus()
        {
            if (manager == null) return;
            if (manager.IsBusy) GUILayout.Label("서버와 동기화 중…", centeredStyle, GUILayout.Height(36f));
            else if (!string.IsNullOrEmpty(manager.LastError))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.45f);
                GUILayout.Label(manager.LastError, centeredStyle, GUILayout.Height(36f));
                GUI.color = old;
            }
        }

        private static PvpInsectSnapshot GetMember(PvpInsectSnapshot[] team, int index)
        {
            return team != null && index >= 0 && index < team.Length ? team[index] : null;
        }
    }
}
