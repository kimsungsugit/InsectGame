# 스토리 영상 — 제작·삽입 단일 출처

스토리 시작·중간·끝에 넣는 **실제 영상 파일(mp4)** 11편의 샷 리스트·AI 프롬프트·자막 큐·인코딩·검증 절차.
자막 문구를 고치면 `Assets/Scripts/Story/StoryVideoLibrary.cs`도 함께 고친다(그쪽이 런타임 출처).

- 재생: `StoryVideoDirector` — 비트의 대사가 끝난 뒤(`StoryBeatCompleted`) 화면을 통째로 덮는다.
- 파일: `Assets/StreamingAssets/Video/<videoId에서 vid_ 제거>.mp4` — `VideoPlayer.url` 스트리밍(임포터를 안 탄다).
- 검증: `story_lint` 검사 25(ID·switch·파일 배치), `StoryVideoLibraryTests`(길이 상한·큐·금칙).

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

## 4-1. 지금 들어 있는 것은 자리표시다

`Assets/StreamingAssets/Video/`의 11편은 **AI 생성 전의 자리표시 애니매틱**이다 —
`.claude/scripts/story_video_placeholders.py`가 §4의 샷 길이와 챕터 팔레트로 만든 무자막 색면 영상
(회화풍 노이즈가 느리게 흐른다). 규격(720p/H.264 Main/≤2 Mbps/총 길이)은 최종본과 같아 재생 경로·
자막 타이밍·기기 확인을 먼저 할 수 있다. AI 영상이 나오면 **같은 파일명으로 덮어쓴다** — 코드는 안 바뀐다.

```
python -X utf8 .claude/scripts/story_video_placeholders.py <ffmpeg.exe> Assets/StreamingAssets/Video
```

## 5. 제작 절차 (편당)

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
   맞춘다(자리표시 스크립트가 그렇게 한다). 짧아지면 마지막 자막 큐가 영상 밖으로 나간다.
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
