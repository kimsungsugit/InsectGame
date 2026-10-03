# 스토리 영상 — 제작·삽입 단일 출처

스토리 시작·중간·끝에 넣는 **실제 영상 파일(mp4)** 11편의 샷 리스트·자막 큐·렌더(§4-1 실루엣 삽화)·
AI 프롬프트(§5, 나중에 바꿀 때)·인코딩·검증 절차.
자막 문구를 고치면 `Assets/Scripts/Story/StoryVideoLibrary.cs`도 함께 고친다(그쪽이 런타임 출처).

- 재생: `StoryVideoDirector` — 비트의 대사가 끝난 뒤(`StoryBeatCompleted`) 화면을 통째로 덮는다.
- 파일: `Assets/StreamingAssets/Video/<videoId에서 vid_ 제거>.mp4` — `VideoPlayer.url` 스트리밍(임포터를 안 탄다).
- 검증: `story_lint` 검사 25(ID·switch·파일 배치), `StoryVideoLibraryTests`(길이 상한·큐·금칙).

## 0. 오프닝 프롤로그 — 게임을 켜면 처음 보는 20초 (스토리 비트와 별개)

스토리 비트에 붙는 영상이 아니다. `OpeningSceneController`가 첫 실행(cold start)과 설정의 「오프닝 다시보기」에서
틀고, 내레이션 네 줄·타이틀·건너뛰기는 IMGUI로 얹는다. **원화는 기존 오프닝 일러스트 3장**
(`Resources/UI/Opening/opening_0{1,2,3}_{landscape,portrait}.jpg`)이다 — 새 그림 없이 움직임만 입혔다.

| 파일 | 만드는 스크립트 | 규격 |
|---|---|---|
| `StreamingAssets/Video/opening_prologue_landscape.mp4` | `python -X utf8 Tools/Video/opening_prologue.py landscape` | 1280×720 · 20s · 무음 |
| `StreamingAssets/Video/opening_prologue_portrait.mp4` | `python -X utf8 Tools/Video/opening_prologue.py portrait` | 720×1280 · 20s · 무음 |
| `Resources/Audio/Opening/opening_prologue.wav` | `python -X utf8 Tools/Video/opening_audio.py` | 44.1kHz 스테레오 · 20s |

세로 원화는 가로를 잘라 만든 그림이 아니라 **따로 구도를 잡은 그림**이라 영상도 방향별 두 편이다.
재생 중 방향이 바뀌면 갈아타지 않고 지금 영상을 가운데 잘라(cover) 계속 튼다.

| 시각 | 그림 | 내용 | 내레이션 |
|---|---|---|---|
| 0.0~9.6 | ① 밤숲 길 | 빛이 하나씩 꺼지고 숲이 가라앉는다. 4.3초부터 길 끝에 그물을 든 검은 코트가 서고, 남은 빛이 그물로 빨려 들어가 차갑게 번쩍이며 사라진다. 8.3~9.4 암전 | 곤충이 사라지고 있다. / 그리고 그것을 남김없이 거두려는 자들이 있다. |
| 9.8~15.0 | ② 파트너 | 어둠에서 떠오르고, 꺼졌던 그림 속 빛이 하나둘 돌아온다 | 사라지는 이름을 기록하는 일. |
| 14.0~15.0 | ②→③ | **맞춤 컷** — 두 그림의 눈 중점·간격을 재서 겹치는 순간 같은 자리·크기에 온다 | |
| 15.0~20.0 | ③ 빛나는 잎 | 16.0 곡의 강세에 빛이 번지고 타이틀. 19.2~20.0 페이드아웃 | 거기서부터 시작된다. |

- **소리**: 테마곡(10초, D음 드론, 6초에 강세)을 10.0초에 놓고, 앞 10초는 같은 D조 패드·바람·빛의 반짝임을
  합성했다. 빛이 꺼지는 만큼 반짝임이 잦아들고, 빛이 그물에 닿을 때마다 핑, 암전(9.36)에 낮은 붐.
  사건 시각은 `opening_audio.py`가 영상 스크립트를 모듈로 읽어 공유한다(같은 난수 스케줄).
- **시각의 단일 출처는 `OpeningSequenceState`다.** 두 스크립트의 상수가 거기와 같고, `OpeningSequenceTests`가
  내레이션이 암전·타이틀과 겹치지 않는지 고정한다. 한쪽을 바꾸면 셋을 함께 고친다.
- **영상을 못 틀어도 오프닝은 선다.** 파일 없음·디코더 오류·준비 3초 초과·재생 중 1.2초 멈춤이면 같은 시계로
  정지 그림 3장을 넘긴다. 다시보기가 거는 `timeScale = 0`·`AudioListener.pause`에 멈추지 않도록 영상 시계는
  `UnscaledGameTime`이다.

## 0-1. 꿈 프롤로그 도입 — 경기장 입장 8초 (스토리 비트와 별개)

「챔피언의 꿈」(`DreamPrologueDirector`)의 첫 장면이다. 비트에 붙는 영상이 아니라 `DreamIntroVideo`가 직접 튼다.
규칙·실패 경로는 `.claude/rules/dream-prologue.md` 「도입 영상」.

| 파일 | 만드는 스크립트 | 규격 |
|---|---|---|
| `StreamingAssets/Video/dream_arena.mp4` | `python -X utf8 Tools/Video/dream_arena.py [--preview 1.0,5.0 \| --sheet]` | 1280×720 · 8s · 무음 · 약 1.6MB |
| `Resources/Audio/Dream/dream_arena.wav` | `python -X utf8 Tools/Video/dream_arena_audio.py` | 44.1kHz 스테레오 · 8s |

| 시각 | 장면 | 소리 |
|---|---|---|
| 0.0~3.0 | **선수 입장 터널** — 아치형 갈빗대가 바깥으로 날아가고(걷는 느낌) 끝의 빛이 점점 커진다. 그 빛을 향해 걷는 챔피언의 뒷모습. 2.35부터 빛이 차올라 흰 화면 | 먹먹한 환호(저역만)가 커진다 · 걸음마다 발소리와 울림 · 낮은 드론 · 2.0부터 상승음 |
| 3.0~3.9 | **흰 섬광 → 경기장** — 흰빛이 걷히며 관중석 전체·서치라이트·색종이가 드러난다 | 큰 '쿵' + 막혔던 환호가 한꺼번에 터진다(먹먹함이 걷힘) |
| 3.9~5.75 | 카메라가 맞대결 쪽으로 밀고 들어간다. 바닥 빛무리 속에 챔피언·파트너(헤라클레스)·맞은편 상대(태고의 비천룡) | 박수가 점점 촘촘해진다 · 휘파람 · 환호가 일렁이며 부푼다 |
| 5.2~6.0 | 서치라이트가 두 곤충으로 모인다. 챔피언이 팔을 든다 | 5.75 숨죽임(환호가 푹 꺼진다) |
| 6.1 | **맞대결 번쩍** — 빛이 한 번 터지고 화면이 흔들린다 | 가장 큰 타격 + 환호 정점 + 금속성 울림 |
| 7.2~8.0 | 암전 → 게임이 「챔피언 결정전」 카드를 얹는다 | 페이드아웃 |

- 화풍은 오프닝·스토리 영상과 같은 실루엣 삽화(`silhouette_kit`) — 사람은 뒷모습뿐이고 글자는 굽지 않는다.
- **세로 화면**은 가운데 약 32%(x 437~843)만 보인다. 맞대결 쌍(뿔 ↔ 머리)이 그 안에 들어가게 배치했다.
- 길이를 바꾸면 `T_TOTAL`(영상 스크립트)과 `DreamPrologueData.IntroSeconds`를 함께 — `DreamIntroVideoTests`가 헤더로 잡는다.

## 1. 제약 (전부 코드가 강제한다)

| 제약 | 값 | 강제 지점 |
|---|---|---|
| 길이 | **≤ 15초** (목표 10~15) | `StoryVideoLibraryTests` — `AutoUnfreezeTime(20) − 4 − 1`. 넘으면 재생 중 조작이 살아난다 |
| 해상도·코덱 | 1280×720, H.264 **Main** 프로파일, 24fps, ≤2 Mbps, AAC 96k, `+faststart` | minSdk 25 하드웨어 디코더. 편당 ≤4 MB, 11편 ≤45 MB |
| 화면비 | 16:9 마스터 1본. 세로 화면은 **중앙 cover-crop** | `UIHelper.CalculateCoverUv` — 피사체를 중앙 세로 안전영역(가로 폭의 가운데 56%)에 둔다 |
| 자막 | **영상에 굽지 않는다** — 게임이 IMGUI로 얹는다 | `StoryVideoLibrary` 큐. 프롬프트에 `no text, no letters, no captions` |
| 금칙 | 자막에 「무명」 없음 | `Library_Cues_NeverNameTheNameless` |
| 배타 | 같은 비트에 `cutsceneId`·`stageExitId`와 함께 두지 않는다 | `story_lint` 검사 13 |
| 시점 | 컷신은 **마무리**다 — 직전 대사를 되풀이하지 않고 "그 대사가 끝난 다음 화면에 남을 것"을 보여준다 | StoryBible 7-1의 프롤로그 사고 |
| 누락 | 파일이 없으면 경고 1줄 + 건너뜀(진행 유지) | `StoryVideoDirector.Play` / `errorReceived` |

## 2. 공통 스타일 가이드 (프롬프트 접두)

```
STYLE: painterly storybook illustration in motion, hand-painted textures, soft volumetric light,
muted natural palette, gentle slow camera drift, 24fps, cinematic 16:9, subject centered in the
middle 56% of the frame (portrait-safe). No text, no letters, no captions, no logos.
No human faces shown frontally — silhouettes, backs, hands, objects only.
Cross-dissolve between shots, final shot fades to black.
```

- **인게임 로우폴리와 의도적으로 다른 삽화 톤** — 회상·삽화로 읽히게 한다. 캐릭터 얼굴을 안 잡으므로 인게임 모델과 어긋날 일이 없다.
- 챕터 팔레트: 초원 새벽 금빛 / 연못 청록 / 숲 이끼녹 / 습지 회갈 / 산 새벽보라 / 모래 황토 / 서릿길 청백 / 잿불 주홍 / 우듬지 신록 / 빈칸 무채색.
- 글씨가 필요한 소품(장부·석판·표찰)은 `illegible ancient marks`, `unreadable scribbles`로만.
- **그것(최종 보스)의 형체를 보여주지 않는다** — 빌린 곤충 실루엣이 겹쳐 흔들리는 그림자만. 이 11편에는 등장하지 않는다.
- 배경음: 무음 또는 앰비언트(바람·물·불똥)만. 테마곡은 넣지 않는다(저작권·용량).

## 3. 편성표

| # | 비트 | videoId | 파일 | 길이 |
|---|---|---|---|---|
| 1 | `ch1_intro` (시작, `cs_story_prologue` 대체) | `vid_ch1_prologue` | `ch1_prologue.mp4` | 12s |
| 2 | `ch2_watchers` | `vid_ch2_watchers` | `ch2_watchers.mp4` | 10s |
| 3 | `ch3_reach_forest` | `vid_ch3_scholar` | `ch3_scholar.mp4` | 12s |
| 4 | `ch4_harvest` | `vid_ch4_crates` | `ch4_crates.mp4` | 12s |
| 5 | `ch5_summit` | `vid_ch5_summit` | `ch5_summit.mp4` | 12s |
| 6 | `ch8_confront` | `vid_ch8_vault` | `ch8_vault.mp4` | 12s |
| 7 | `ch9_confront` | `vid_ch9_archive` | `ch9_archive.mp4` | 14s |
| 8 | `ch10_confront` | `vid_ch10_kiln` | `ch10_kiln.mp4` | 15s |
| 9 | `ch11_confront` | `vid_ch11_crown` | `ch11_crown.mp4` | 12s |
| 10 | `ch12_confront` | `vid_ch12_ledger` | `ch12_ledger.mp4` | 14s |
| 11 | `fin_epilogue` (끝) | `vid_fin_epilogue` | `fin_epilogue.mp4` | 15s |

제외: ch6·ch7·`fin_unnamed`·`fin_seal`은 프로시저럴 컷신이 이미 대치→해소로 짝을 이뤄 포화. `ch5_blocked`·`ch4_bond`·ch8의 등장 자체는 무대 연출(`StoryStageLibrary`)이 잡고 있으므로 영상은 **NPC 몸짓이 못 보여주는 것**(상자 안쪽·창고 규모·회상·붕괴)만 맡는다.

## 4. 편별 스토리보드

각 편: 직전 대사(왜 이 그림이 그 다음인가) → 샷(초) → 프롬프트 → 자막 큐(`at` / `dur` / 문구).
샷 길이 합 = 저작 길이. AI 도구는 샷 단위(4~8초 클립)로 생성하고 §5에서 이어붙인다.

### 1. `vid_ch1_prologue` — 12s · 초원 새벽 금빛
직전: 어르신이 첫 파트너와 은빛 그물을 주고 "풀밭으로 나가 보렴". 옛 컷신은 이 대사를 되풀이했다 — 되풀이하지 않는다.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.5 | 새벽 초원 광각, 이슬 맺힌 풀. 카메라가 천천히 옆으로 흐른다 |
| 2 | 3.0 | 풀잎 사이 곤충 서너 마리. 하나씩 안개처럼 흐려져 사라진다(디졸브) |
| 3 | 2.5 | 손에 든 은빛 그물 클로즈업, 역광 |
| 4 | 3.0 | 손등에 작은 곤충 한 마리가 앉는다. 페이드아웃 |

```
[STYLE] Shot 1: wide dawn meadow with dew on tall grass, warm golden backlight, slow lateral drift.
Shot 2: macro of three small beetles among grass blades, one by one they dissolve into faint mist and vanish, quiet.
Shot 3: close-up of a hand holding a silver butterfly net, rim-lit by sunrise.
Shot 4: a tiny longhorn beetle lands on the back of a hand, gentle focus pull, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 0.8 | 3.2 | 풀밭은 이렇게 넓은데, 움직이는 것이 눈에 잘 띄지 않는다. |
| 5.2 | 2.8 | 잡는 법은 몸이 먼저 익혔다. 그리고 이제 혼자가 아니다. |
| 8.6 | 3.0 | 이 아이와 함께라면, 그 이유를 찾을 수 있을지도 모른다. |

### 2. `vid_ch2_watchers` — 10s · 연못 청록
직전: 검은 옷의 사내가 종만 묻고 "곧 전부 장부에 오를 테니". 라온 "이름도 안 밝히고".
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 연못 수면의 반영, 잠자리 한 마리가 스친다 |
| 2 | 4.0 | 갈대 너머 검은 옷 실루엣(뒷모습 반쯤)이 작은 수첩에 무언가 적는다 |
| 3 | 3.0 | 돌아서 안개 속으로 걸어 들어가 사라진다. 페이드아웃 |

```
[STYLE] Shot 1: still pond surface with teal reflections, a dragonfly skims across, soft ripples.
Shot 2: through tall reeds, a figure in a long black coat seen from behind at three-quarter angle, writing in a small notebook with unreadable scribbles, face never visible.
Shot 3: the figure turns away and walks into low mist until it dissolves, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.0 | 3.0 | 이름은 묻지 않았다. 종만 물었다. |
| 5.6 | 3.4 | …곧 전부 장부에 오를 테니. |

### 3. `vid_ch3_scholar` — 12s · 숲 이끼녹
직전: 세라 합류. "봉인에 금이 간 것과 무관하지 않아요… 기록 하나하나가 열쇠일지도".
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 숲 동굴 벽, 이끼 사이 고대 각인(판독 불가 문양) 클로즈업 |
| 2 | 3.0 | 손끝이 문양을 훑는다. 먼지가 떨어진다 |
| 3 | 3.0 | 문양 한 줄이 희미하게 빛났다가 잦아든다 |
| 4 | 3.0 | 동굴 밖 숲 너머, 먼 산등성이에 유적의 윤곽이 실루엣으로. 페이드아웃 |

```
[STYLE] Shot 1: mossy cave wall inside a forest, ancient carved glyphs (illegible ancient marks), green filtered light.
Shot 2: a scholar's fingertips trace the carving, dust falling, shallow depth of field.
Shot 3: one line of glyphs glows faintly amber then fades.
Shot 4: view from the cave mouth over the forest canopy toward a distant ruin silhouette on a ridge, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.0 | 3.0 | 유적 밖에서 나온 첫 각인이었다. |
| 5.0 | 3.0 | 봉인에 금이 갔다. 사라지는 것들은 그 틈으로 빠져나가고 있다. |
| 8.6 | 3.0 | 기록 하나하나가, 그 균열을 메우는 열쇠일지도 모른다. |

### 4. `vid_ch4_crates` — 12s · 습지 회갈
직전: 사내가 상자를 내밀다 들킨다. "많은 건 세어도 티가 안 나거든". 세라 "상자가 열 개는 넘어 보였어요". 무대 연출이 "들킨 몸짓"을 이미 잡았으므로 영상은 **상자 안쪽**을 맡는다.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 안개 낀 습지, 물 위 안개가 낮게 흐른다 |
| 2 | 3.5 | 반쯤 잠긴 나무 상자들이 줄지어. 카메라가 줄을 따라 이동 |
| 3 | 3.0 | 상자 틈으로 더듬이·날개가 미세하게 움직인다(매크로) |
| 4 | 2.5 | 뚜껑에 못 박힌 표찰, 글씨는 판독 불가. 페이드아웃 |

```
[STYLE] Shot 1: misty marsh at dusk, low fog drifting over dark water, grey-brown palette.
Shot 2: a long row of half-submerged wooden crates, camera tracks slowly along the line.
Shot 3: macro through a gap in a crate: antennae and wing edges moving slightly in darkness.
Shot 4: a nailed paper tag on a crate lid with unreadable scribbles, water dripping, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.2 | 3.0 | 많은 건 세어도 티가 안 난다 — 그래서 여기부터라고 했다. |
| 5.4 | 2.6 | 상자는 열 개가 넘었다. |
| 8.6 | 3.0 | 저게 다 살아 있는 것이라면. |

### 5. `vid_ch5_summit` — 12s · 산 새벽보라
직전: 세라 "안개 너머로 빛나는 게 고대 유적… 이제 마지막 장만 남았어요". 카메라 없이는 성립하지 않는 장면.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 능선 위 두 실루엣의 뒷모습, 바람에 옷자락 |
| 2 | 3.0 | 구름이 천천히 갈라진다 |
| 3 | 3.5 | 안개 아래 골짜기에 고대 유적이 희미하게 빛난다 |
| 4 | 2.5 | 유적으로 아주 느린 줌. 페이드아웃 |

```
[STYLE] Shot 1: two silhouettes seen from behind on a windy mountain ridge at dawn, violet-blue sky.
Shot 2: clouds slowly parting below the ridge.
Shot 3: far below in the mist, an ancient stone ruin glowing faintly amber.
Shot 4: very slow push-in toward the ruin, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.4 | 3.0 | 여기까지 온 채집가는 처음이라고 했다. |
| 5.6 | 3.0 | 안개 너머로 빛나는 것 — 모든 사라짐의 근원. |
| 9.0 | 2.6 | 이제 마지막 장만 남았다. |

### 6. `vid_ch8_vault` — 12s · 모래 황토
직전: 집게 "빈칸을 메우는 거다… 전부, 지금 당장" / 세라 "몇이나 살아 있죠?" / "…장부에는 올라가 있다". 등장은 무대 연출이 맡았으므로 영상은 **창고의 규모**.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 모래에 반쯤 묻힌 창고 입구, 안으로 들어가는 카메라 |
| 2 | 3.5 | 천장까지 쌓인 상자들, 먼지 속 광선 |
| 3 | 3.0 | 상자 줄을 따라 이동. 몇몇은 뚜껑이 열린 채 비어 있다 |
| 4 | 2.5 | 상자 하나가 조용히 멈춰 있다(움직임 없음). 페이드아웃 |

```
[STYLE] Shot 1: entrance of a storehouse half-buried in sand dunes, camera moves inside, ochre light.
Shot 2: crates stacked to the ceiling, dust motes in slanted light beams.
Shot 3: tracking along rows of crates, some lids open and empty.
Shot 4: one closed crate, completely still, no movement inside, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.0 | 3.2 | 빈칸을 메우기 위해 — 전부, 지금 당장. |
| 5.4 | 2.8 | 그 안의 아이들은 지금 몇이나 살아 있을까. |
| 8.8 | 2.8 | …장부에는 올라가 있다. |

### 7. `vid_ch9_archive` — 14s · 서릿길 청백
직전: 저울 "이 서고 목록을 만든 게 누구였더라?" / 세라 "…저였어요" / "무엇이 달라졌지?". 무대에는 배우가 저울 하나뿐이라 세라를 세우지 못했다 — 영상이 그 결손을 맡는다.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 얼음 벽 속에 봉인된 곤충들, 청백 광 |
| 2 | 3.0 | 얼음 벽 앞 두 실루엣이 마주 선다(옆모습, 얼굴 없음) |
| 3 | 4.0 | 얼음 벽에 비친 회상: 젊은 손이 목록을 적는다(판독 불가), 따뜻한 색 |
| 4 | 4.0 | 현재의 손이 그 반영 위를 덮는다. 페이드아웃 |

```
[STYLE] Shot 1: insects preserved inside a wall of clear blue ice, cold white-blue light.
Shot 2: two silhouettes facing each other in profile in front of the ice wall, faces never visible.
Shot 3: a reflection in the ice: a young hand writing a long list with unreadable marks, warm sepia tint, memory-like.
Shot 4: a present-day gloved hand presses over the reflection, covering it, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.0 | 3.0 | 이 서고의 목록을 만든 것은 누구였나. |
| 5.0 | 2.6 | …나였다. 부정하지 않는다. |
| 8.4 | 2.4 | 얼음은 멈춰 세울 뿐이다. 자라지도, 죽지도 못하게. |
| 11.2 | 2.5 | 달라진 것은 속도가 아니라 방향이다. |

### 8. `vid_ch10_kiln` — 15s · 잿불 주홍
직전: 갱도 붕괴·라온 부상·먹의 구조가 **지문 한 줄**로만 처리된 비트. 이 편이 가장 크게 이득을 본다. 무대 연출은 먹의 조용한 등장만 맡는다.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 잿불 갱도 내부, 불똥이 떠다닌다 |
| 2 | 3.5 | 들보가 무너진다, 화면 흔들림과 먼지 |
| 3 | 3.5 | 검은 소매의 손이 재 속에서 다른 팔을 붙잡아 끌어낸다 |
| 4 | 2.5 | 재 위에 놓인 상자 하나 |
| 5 | 2.5 | 들려 있던 두꺼운 장부가 천천히 덮인다. 페이드아웃 |

```
[STYLE] Shot 1: inside a smoldering mine tunnel, ember sparks drifting, deep orange-red light.
Shot 2: wooden beams collapse, camera shake, dust and sparks burst.
Shot 3: a black-sleeved hand grips another arm and pulls it out of ash and rubble, no faces.
Shot 4: a single wooden crate set down on grey ash, embers glowing around it.
Shot 5: a thick ledger held open in a hand slowly closes, unreadable marks on pages, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 3.6 | 2.6 | 상자는 다 못 꺼냈다. 이 하나뿐이다. |
| 7.4 | 3.4 | 나는 이름을 적는 사람이지, 가두는 사람이 아니었다. |
| 11.6 | 2.8 | 여기서부턴, 둘이다. |

### 9. `vid_ch11_crown` — 12s · 우듬지 신록
직전: 세라 "울타리를 하나만 친 게 아니었어요… 두 칸을 남겼어요. 우리한테요". 무대 연출은 세라가 앞서 올라가 위를 가리키는 것까지 — 영상은 **가리킨 곳**.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 거대수 꼭대기, 신록 사이 빛 |
| 2 | 3.0 | 나무껍질에 새겨진 문양 클로즈업(ch3 각인과 같은 형태) |
| 3 | 3.0 | 시선이 아래로 내려가 멀리 유리온실(꽃밭)이 반짝인다 |
| 4 | 3.0 | 나무와 온실이 한 화면에, 둘 다 살아 있다. 페이드아웃 |

```
[STYLE] Shot 1: crown of a giant tree, fresh green leaves, sunlight flickering through.
Shot 2: close-up of ancient glyphs carved into the bark, same illegible marks as a ruin.
Shot 3: camera tilts down over the canopy to a distant glass greenhouse glinting in a flower field.
Shot 4: wide frame holding both the great tree and the greenhouse, both alive and green, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.0 | 3.0 | 울타리는 하나가 아니었다. |
| 5.0 | 3.2 | 실패할 것을 알면서 울타리를 치고, 실패한 뒤를 위해 두 칸을 남겼다. |
| 9.0 | 2.6 | 우리에게. |

### 10. `vid_ch12_ledger` — 14s · 빈칸 무채색
직전: 하월 "삼천 종… 절반이 창고에서 죽었다… 빈칸을 메우겠다고 빈칸을 만들었어… 길을 비켜 주마". 무대 연출은 몸짓 없는 등장뿐.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 빈 석판들이 원형으로 둘러선 방, 회색 |
| 2 | 3.5 | 벽면을 가득 채운 장부 선반(글씨 판독 불가) |
| 3 | 3.5 | 선반 절반이 먼지에 덮여 빛바래 있다 |
| 4 | 4.0 | 노인의 뒷모습이 옆으로 비켜서고, 안쪽 어둠으로 시선이 간다. 페이드아웃 |

```
[STYLE] Shot 1: a circular chamber of blank standing stone slabs, desaturated grey palette.
Shot 2: walls lined floor to ceiling with shelves of ledgers, spines with unreadable marks.
Shot 3: half of the shelves covered in dust, pages faded and grey.
Shot 4: an old man seen from behind steps aside from a doorway, camera looks past him into deep darkness, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.0 | 2.8 | 삼천 종. 삼십 년. |
| 4.8 | 2.8 | 그중 절반이 창고에서 죽었다. |
| 8.4 | 3.0 | 빈칸을 메우겠다고, 빈칸을 만들었다. |
| 11.6 | 2.0 | 길이 열렸다. 안쪽에서 그것이 기다린다. |

### 11. `vid_fin_epilogue` — 15s · 초원 새벽 금빛(1편과 수미상관)
직전: 세라의 후일담 5줄(하월·라온·먹·"빈칸은 사라지지 않았어요"·"계속 만나요"). 몽타주. 마지막 컷에는 자막을 두지 않는다.
| 샷 | 초 | 그림 |
|---|---|---|
| 1 | 3.0 | 노인의 뒷모습이 들판에서 손을 펴 곤충 하나를 놓아준다 |
| 2 | 3.0 | 초원의 두 실루엣(한쪽은 팔을 감싼 채) |
| 3 | 3.0 | 새 장부에 펜이 움직인다 — 이름 칸만 있고 수량 칸이 없다(판독 불가) |
| 4 | 3.0 | 빈 석판 하나가 여전히 비어 있다 |
| 5 | 3.0 | 새벽 초원, 그물 없이 손을 뻗는 손. 곤충이 다가온다. 페이드아웃 |

```
[STYLE] Shot 1: an old man seen from behind in a field opens his palm and releases a beetle into the air.
Shot 2: two silhouettes walking through a dawn meadow, one with an arm in a sling.
Shot 3: a pen writing in a fresh ledger with a single column of unreadable marks, warm lamplight.
Shot 4: one blank standing stone slab in grey light, still empty.
Shot 5: dawn meadow, a bare hand reaching out without a net, a small insect approaching it, fade to black.
```
| at | dur | 자막 |
|---|---|---|
| 1.0 | 3.0 | 장부는 전부 넘겨졌다. 이름들을 한 종씩 찾아다니는 데 남은 평생이 걸릴 것이다. |
| 4.8 | 2.6 | 새 장부에는 수량 칸이 없다. |
| 8.0 | 2.8 | 빈칸은 사라지지 않았다. 종이 사라지면 자리는 또 생긴다. |
| 11.2 | 2.6 | 그러니까 계속 만나요. 한 마리씩, 계속. |

## 4-1. 지금 들어 있는 것 — 실루엣 삽화 영상 (2026-09-28)

`Assets/StreamingAssets/Video/`의 11편은 **Python으로 그린 실루엣 삽화 영상**이다. §4의 샷 리스트·길이·
팔레트를 그대로 따르고, 자막 큐도 그 시각에 맞춰져 있다(총 길이 = 샷 합, 디졸브 0.6초는 뒤 샷을 앞당겨 겹친다).
옛 색면 자리표시(`.claude/scripts/story_video_placeholders.py`)를 대체했다.

```
python -X utf8 Tools/Video/story_silhouettes.py                      # 11편 전부(약 30분)
python -X utf8 Tools/Video/story_silhouettes.py ch9_archive          # 한 편
python -X utf8 Tools/Video/story_silhouettes.py ch9_archive --preview 1.5,8.0   # 프레임만 PNG로(Artifacts/story-silhouettes-preview/)
```

| 파일 | 맡는 것 |
|---|---|
| `Tools/Video/silhouette_kit.py` | 부품 — 2배 슈퍼샘플 마스크, 하늘·안개·빛살·입자, 능선·봉우리·숲띠, 인물(뒷모습·옆모습, 채집망·가방·챙 모자), 옆모습 손·주먹, 곤충, 상자·장부·긁적임·새김 문양, 물 반영 |
| `Tools/Video/sil_act1.py` | ch1~ch5 샷 |
| `Tools/Video/sil_act2.py` | ch8~ch12·종장 샷 |
| `Tools/Video/story_silhouettes.py` | 편성(파일명·샷 길이·색조)·디졸브·페이드·인코딩 |

**화풍**: 겹겹의 실루엣(먼 것일수록 옅고 하늘색에 섞인다) + 역광 테두리 + 빛 번짐·빛살·안개·떠다니는 입자.
인물은 뒷모습·옆모습 실루엣뿐이라 얼굴이 없고, 글씨는 판독 불가 긁적임뿐이다(§2 스타일 가이드와 같은 규칙).
인게임 로우폴리와 일부러 다른 삽화 톤이다 — 회상·삽화로 읽힌다.

**§4 샷 리스트와 다르게 푼 자리**(그림으로 읽히게 하려고 바꾼 것 — 자막·길이는 그대로):
- 인물 표지 — 주인공은 어깨에 채집망, 세라는 긴 머리와 가방, 명부회는 챙 넓은 모자. 실루엣만으로 누군지 알게 한다.
- ch2 잠자리는 수면이 아니라 **밝은 수평선 앞**을 스친다 — 어두운 물 위에서는 막대처럼 보였다.
- ch9 "마주 선 옆모습"은 코끝·턱을 붙여 옆모습으로 읽히게 했다. 저울은 명부회 모자를 쓴다.
- ch3·ch11의 새김 문양은 **같은 씨앗**(`GLYPH_SEED`)으로 그린다 — "유적과 같은 문양"이 실제로 같은 모양이다.
- 종장 둘째 샷의 두 실루엣은 팔을 감싼 라온과 검은 코트(먹)다.

**고르며 버린 것**(실측 — 시안 프레임을 보고 고쳤다): 사인파 능선은 물결로 읽혀 중점 변위 봉우리로,
대칭 신전은 은행 아이콘처럼 읽혀 한쪽이 무너진 유적으로, 막대 팔·사다리꼴 몸통은 로봇처럼 읽혀 곡선 어깨·
굽은 팔꿈치로, 손등이 보이는 네모 손은 장갑처럼 읽혀 옆모습 손으로 바꿨다.

나중에 AI 영상(§5)이 나오면 **같은 파일명으로 덮어쓴다** — 코드는 안 바뀐다.

## 5. AI 영상으로 바꿀 때 — 제작 절차 (편당)

1. §4의 프롬프트로 **샷 단위** 생성(도구: Veo / Sora / Runway 등). 샷당 2~3안 뽑아 고른다.
   고를 때 볼 것: 글자가 찍히지 않았는가 / 얼굴이 정면으로 나오지 않았는가 / 피사체가 중앙 56% 안에 있는가.
2. 이어붙이기 + 디졸브 + 페이드아웃 + 인코딩(ffmpeg, 두 샷 예시):
   ```
   ffmpeg -i s1.mp4 -i s2.mp4 -filter_complex \
     "[0:v]scale=1280:720,fps=24,setsar=1[a];[1:v]scale=1280:720,fps=24,setsar=1[b];\
      [a][b]xfade=transition=dissolve:duration=0.6:offset=2.9[v];[v]fade=t=out:st=9.4:d=0.6[vo]" \
     -map "[vo]" -an -c:v libx264 -profile:v main -level 4.0 -pix_fmt yuv420p \
     -b:v 1800k -maxrate 2000k -bufsize 4000k -movflags +faststart ch2_watchers.mp4
   ```
   앰비언트를 넣으면 `-an` 대신 `-c:a aac -b:a 96k`. `offset` = 앞 샷 길이 − 디졸브 길이.
   **디졸브는 총 길이를 그만큼 깎는다** — 샷을 디졸브 길이만큼 길게 뽑아 합이 `expectedDuration`과 같게
   맞춘다(실루엣 렌더러가 그렇게 한다). 짧아지면 마지막 자막 큐가 영상 밖으로 나간다.
3. 길이·크기 확인 → `expectedDuration`과 자막 큐가 실제 길이 안인지 맞춘다:
   ```
   ffprobe -v error -show_entries format=duration:stream=codec_name,profile,width,height -of default=nw=1 ch2_watchers.mp4
   ls -l ch2_watchers.mp4    # ≤ 4 MB
   ```
4. `Assets/StreamingAssets/Video/`에 배치 → `python -X utf8 .claude/scripts/story_lint.py`(검사 25의 WARN이 줄어든다).

## 6. 검증

- **정적**: `story_lint`(검사 13·25) → `ci_check.py`.
- **단위**: PlayMode 러너 — `StoryVideoLibraryTests`(길이 상한·큐 정렬·금칙). 배치모드에는 디코더가 없어 재생 자체는 못 본다.
- **발화**: `StoryBeatWalkthrough -walkMode campaign` — videoId 비트 다음 비트가 정상 도달하는지. 에디터에 파일이 없으면 `[StoryVideo] 파일이 없다` 경고 1줄 뒤 즉시 `Stop()`이라 진행이 산다.
- **기기**(subst ASCII 드라이브에서 `AndroidReleaseBuilder.BuildDeviceApkFromCommandLine`):
  1. APK 크기 증분 ≤ 50 MB.
  2. ch2(연못 NpcTalk)·ch10(갱도 SubAreaEnter)·에필로그(NpcTalk) 3편: 첫 프레임 지연 < 1s, 세로 화면 crop 구도, 자막 타이밍.
  3. 건너뛰기 버튼·Back키 → 즉시 조작 복구, 카메라 정상.
  4. `BattleWin` 직후 발화(전투 결과 화면 뒤에 재생되는지 — 지연 큐).
  5. 재생 중 홈 → 복귀 시 이어서 재생, 다시 홈 → 재생 끝.
  6. 오프닝 다시보기 중 발화 없음(`playUiRoot`가 꺼져도 `World/` 아래라 살아 있고, 모달 가드가 막는다).
  7. 파일 하나를 지운 빌드: 경고 1줄 + 다음 비트 정상.
