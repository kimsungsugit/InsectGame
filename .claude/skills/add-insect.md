---
name: add-insect
description: 새 곤충 종을 데이터베이스에 추가합니다
user_invocable: true
args: "<곤충이름> <레어도> <속성>"
---

# 새 곤충 추가

InsectDatabase에 새 곤충 종을 추가합니다.

## 필요 정보
- **이름**: displayName (한글)
- **ID**: insectId (영문 소문자_스네이크)
- **레어도**: Common/Uncommon/Rare/Epic/Legendary
- **속성**: Bug/Poison/Water/Leaf/Wind/Electric/Earth/Light/Dark/Metal
- **기본 스탯**: baseHp, baseAtk, baseDef
- **스폰 조건**: 시간대, 날씨 (선택)
- **레벨 범위**: minLevel~maxLevel
- **스폰 가중치**: spawnWeight

## 절차
1. InsectDatabase SO 에셋에 엔트리 추가 (또는 코드에서 확장 DB 등록)
2. 스탯은 레어도 기준표에 맞춰 설정:
   - Common: HP 30-50, ATK 8-15, DEF 5-12
   - Uncommon: HP 40-65, ATK 12-20, DEF 8-16
   - Rare: HP 55-80, ATK 18-28, DEF 12-22
   - Epic: HP 70-100, ATK 25-38, DEF 18-30
   - Legendary: HP 90-130, ATK 35-50, DEF 25-40
3. InsectEntity.BuildModel()에 프로시저럴 모델 추가
4. InsectLoreBootstrapper에 도감 설명 추가
5. 스킬 배정 (기존 스킬 or 새 스킬)

## 체크리스트
- [ ] 기존 insectId와 중복 확인
- [ ] 스폰 조건이 다른 곤충과 겹치지 않는지 확인
- [ ] 스탯이 레어도 기준표 범위 내인지 확인
- [ ] 프로시저럴 모델 빌드 테스트
