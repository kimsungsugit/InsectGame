---
name: impact-analysis
description: 코드 변경의 시스템 영향도를 분석합니다
argument-hint: "<변경대상 파일 또는 클래스명>"
---

# 영향도 분석

지정된 파일/클래스의 변경이 프로젝트 전체에 미치는 영향을 분석합니다.

## 분석 항목

1. **직접 참조**: `/find-refs`로 해당 클래스를 직접 사용하는 파일 목록 수집
2. **이벤트 구독자**: 해당 클래스의 `event Action<T>` 이벤트를 구독하는 파일
3. **AutoWire 연결**: `PlaySceneBootstrap.cs`에서 어떤 시스템에 연결되는지
4. **세이브 영향**: 세이브 파일 호환성 변경 여부 (GameConstants.SaveFiles 참조)
5. **UI 영향**: 관련 UI 화면 목록
6. **Bootstrap 순서**: 초기화 순서 변경 필요 여부
7. **에이전트 담당**: `agent-coordination.md` 참조하여 어떤 에이전트가 관여하는지

## 절차
1. 먼저 Grep으로 클래스/심볼의 모든 참조를 수집
2. 참조를 카테고리별로 분류 (직접호출, 이벤트, AutoWire, Inspector)
3. Bootstrap 등록 위치 확인
4. 세이브 관련 필드 변경 여부 판단
5. 결과를 트리 형태로 출력

## 출력 형식
```
[변경 대상]
├─ 직접 참조: N개 파일
│   ├─ file1.cs:line (사용 방식)
│   └─ file2.cs:line (사용 방식)
├─ 이벤트 영향: N개 구독자
├─ AutoWire 연결: Bootstrap 등록 위치
├─ 세이브 영향: 있음/없음 (영향 시 마이그레이션 필요 여부)
├─ UI 영향: N개 화면
├─ 담당 에이전트: [에이전트명]
└─ 위험도: 낮음/중간/높음
```
