using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 통합 캐릭터 포트레이트 렌더러.
    /// LoginUI, CharacterViewerUI, CharacterOutfitUI 등 모든 곳에서 동일한 캐릭터를 그립니다.
    /// 약 6.4등신 슬림·갸름형 — 필드 3D 캐릭터(BuildPlayerVisual)와 동일한 비례.
    /// </summary>
    public static class CharacterPortraitRenderer
    {
        // 비례 단일 소스 — Draw/DrawArmsAsSkin/DrawBackpackWithSlot 모두 이 헬퍼로 비례 계산.
        // 향후 비례 변경은 이 한 곳만 수정하면 자동 동기.
        public struct Proportions
        {
            public float s, headW, headH, neckH, bodyW, bodyH, legW, legH, armW, armH, shoeH;
            public float headY, bodyTop, legTop, footBottom;
        }

        public static Proportions CalculateProportions(float cy, float scale, int gender)
        {
            Proportions p;
            p.s = scale;
            // 귀여운 치비(~3.3등신) — 필드 3D(PlayerVisualBuilder)와 동일 톤. 옛 6.8등신 사실 비례
            // (head 30 / body 62 / leg 95)에서 머리 크게·몸통·팔다리 짧게·목 최소화로 전환.
            // 합계 ≈ 137, 머리 42 → 약 3.3등신. 필드와 창 실루엣 정합 유지.
            p.headW = 40f * scale;
            p.headH = 42f * scale;
            p.neckH = 2f * scale;
            p.bodyW = (gender == 1) ? 38f * scale : 42f * scale;
            p.bodyH = 40f * scale;
            p.legW = 18f * scale;
            p.legH = 40f * scale;
            p.armW = 15f * scale;
            p.armH = 34f * scale;
            p.shoeH = 13f * scale;
            const float hatPad = 6f;
            p.headY = cy - (p.headH + p.neckH + p.bodyH + p.legH + p.shoeH) * 0.5f + hatPad * scale;
            p.bodyTop = p.headY + p.headH + p.neckH;
            p.legTop = p.bodyTop + p.bodyH;
            p.footBottom = p.legTop + p.legH + p.shoeH;
            return p;
        }

        public static void Draw(float cx, float cy, float scale,
            int gender, int skinColorIdx, int hairColorIdx, int hairStyle, int faceType,
            Color topColor, Color bottomColor, Color shoeColor, Color hatColor,
            float swayX = 0f, bool drawDefaultPack = true)
        {
            Color skin = GetSkinColor(skinColorIdx);
            Color hair = GetHairColor(hairColorIdx);

            // 공용 비례 헬퍼 — DrawArmsAsSkin/DrawBackpackWithSlot/DrawOutfitAccessories와 자동 동기.
            Proportions p = CalculateProportions(cy, scale, gender);
            float s = p.s;
            float headW = p.headW, headH = p.headH, neckH = p.neckH;
            float bodyW = p.bodyW, bodyH = p.bodyH;
            float legW = p.legW, legH = p.legH;
            float armW = p.armW, armH = p.armH;
            float shoeH = p.shoeH;
            float headY = p.headY, bodyTop = p.bodyTop, legTop = p.legTop, footBottom = p.footBottom;

            // === 그림자 ===
            GUI.color = new Color(0f, 0f, 0f, 0.18f);
            DrawRect(cx - bodyW * 0.55f + swayX, footBottom + 2f * s, bodyW * 1.1f, 5f * s);
            GUI.color = Color.white;

            // === 신발 (footHalfGap 0.18 → 0.28: 옛 0.18은 신발 폭보다 좁아 겹쳐 보임) ===
            float footHalfGap = bodyW * 0.28f;
            DrawCol(shoeColor, cx - footHalfGap - legW * 0.5f - 2f * s + swayX, legTop + legH, legW + 4f * s, shoeH);
            DrawCol(shoeColor, cx + footHalfGap - legW * 0.5f - 2f * s + swayX, legTop + legH, legW + 4f * s, shoeH);

            // === 다리 (길고 슬림) ===
            DrawCol(bottomColor, cx - footHalfGap - legW * 0.5f + swayX, legTop, legW, legH);
            DrawCol(bottomColor, cx + footHalfGap - legW * 0.5f + swayX, legTop, legW, legH);

            // === 몸통 ===
            DrawCol(topColor, cx - bodyW * 0.5f + swayX, bodyTop, bodyW, bodyH);
            Color collarCol = Color.Lerp(topColor, Color.white, 0.3f);
            DrawCol(collarCol, cx - 7f * s + swayX, bodyTop + 2f * s, 14f * s, 6f * s);

            // === 팔 ===
            DrawCol(topColor, cx - bodyW * 0.5f - armW + swayX, bodyTop + 4f * s, armW, armH);
            DrawCol(topColor, cx + bodyW * 0.5f + swayX, bodyTop + 4f * s, armW, armH);
            // 손 (피부)
            float handH = 9f * s;
            DrawCol(skin, cx - bodyW * 0.5f - armW + swayX, bodyTop + 4f * s + armH, armW, handH);
            DrawCol(skin, cx + bodyW * 0.5f + swayX, bodyTop + 4f * s + armH, armW, handH);

            // === 배낭 (등 뒤 — 오른쪽으로 살짝 비집고 보임) ===
            // DrawWithOutfit은 drawDefaultPack=false로 호출 + Backpack 슬롯 색을 자체 그림.
            // 기존 호출자(DrawForCreation 등)는 default true라 호환 유지.
            if (drawDefaultPack)
            {
                Color packCol = new Color(1f, 0.65f, 0.2f);
                DrawCol(packCol, cx + bodyW * 0.36f + swayX, bodyTop + 8f * s, 14f * s, bodyH * 0.55f);
                DrawCol(new Color(packCol.r * 0.7f, packCol.g * 0.7f, packCol.b * 0.7f),
                    cx + bodyW * 0.3f + swayX, bodyTop + 2f * s, 3f * s, bodyH * 0.45f);
            }

            // === 목 ===
            DrawCol(skin, cx - neckH * 0.7f + swayX, headY + headH, neckH * 1.4f, neckH);

            // === 머리 베이스 (갸름 오발) ===
            float headX = cx - headW * 0.5f + swayX;
            // 살짝 어두운 윤곽선
            DrawCol(new Color(skin.r * 0.85f, skin.g * 0.85f, skin.b * 0.85f),
                headX - 1f * s, headY - 1f * s, headW + 2f * s, headH + 2f * s);
            DrawCol(skin, headX, headY, headW, headH);

            // === 머리카락 ===
            DrawHair(headX, headY, headW, headH, s, gender, hairStyle, hair);

            // === 눈 (큰 치비 비례 — headW의 0.30배, 필드 3D 큰 눈과 정합. 옛 7×8 고정은 큰 머리에 콩알눈) ===
            float eyeW = headW * 0.30f;
            float eyeH = headH * 0.30f;
            float eyeY = headY + headH * 0.40f;
            float eyeLX = headX + headW * 0.16f;
            float eyeRX = headX + headW * 0.54f;

            DrawCol(Color.white, eyeLX, eyeY, eyeW, eyeH);
            DrawCol(Color.white, eyeRX, eyeY, eyeW, eyeH);
            Color pupilCol = new Color(0.1f, 0.08f, 0.05f);
            // 동공도 눈 비례(필드 동공/눈비 0.6~0.65 정합) — 큰 눈 안 큰 동공
            float pupilW = eyeW * 0.62f;
            float pupilH = eyeH * 0.66f;
            DrawCol(pupilCol, eyeLX + (eyeW - pupilW) * 0.5f, eyeY + (eyeH - pupilH) * 0.6f, pupilW, pupilH);
            DrawCol(pupilCol, eyeRX + (eyeW - pupilW) * 0.5f, eyeY + (eyeH - pupilH) * 0.6f, pupilW, pupilH);
            // 하이라이트 (눈 비례)
            float hlS = eyeW * 0.28f;
            DrawCol(new Color(1f, 1f, 1f, 0.9f), eyeLX + eyeW * 0.5f, eyeY + eyeH * 0.18f, hlS, hlS);
            DrawCol(new Color(1f, 1f, 1f, 0.9f), eyeRX + eyeW * 0.5f, eyeY + eyeH * 0.18f, hlS, hlS);

            // 눈썹 (headH 비례 두께·간격)
            Color browCol = new Color(hair.r * 0.6f + 0.05f, hair.g * 0.6f + 0.05f, hair.b * 0.6f + 0.05f);
            float browH = headH * 0.05f;
            float browGap = eyeH * 0.35f;
            DrawCol(browCol, eyeLX - 1f * s, eyeY - browGap, eyeW + 2f * s, browH);
            DrawCol(browCol, eyeRX - 1f * s, eyeY - browGap, eyeW + 2f * s, browH);

            // 코 (작은 점, headH 비례 절대위치 — 큰 머리에서 중앙에 뜨지 않게)
            Color noseCol = new Color(skin.r * 0.92f, skin.g * 0.88f, skin.b * 0.85f);
            float noseW = headW * 0.07f;
            DrawCol(noseCol, cx - noseW * 0.5f + swayX, headY + headH * 0.60f, noseW, headH * 0.08f);

            // 입 (표정별, headW/headH 비례 위치 — 얼굴 하단 1/4)
            Color mouthCol = new Color(0.85f, 0.4f, 0.35f);
            float mouthW = headW * (0.18f + faceType * 0.05f);
            float mouthH = (faceType == 1) ? headH * 0.07f : headH * 0.05f;
            float mouthY = headY + headH * 0.74f;
            DrawCol(mouthCol, cx - mouthW * 0.5f + swayX, mouthY, mouthW, mouthH);
            if (faceType == 0 || faceType == 1)
                DrawCol(new Color(1f, 0.7f, 0.7f, 0.5f), cx - mouthW * 0.3f + swayX, mouthY + mouthH, mouthW * 0.6f, headH * 0.035f);

            // 볼터치 + 속눈썹 (여자) — headW/headH 비례
            if (gender == 1)
            {
                Color blush = new Color(1f, 0.55f, 0.55f, 0.4f);
                float blushW = headW * 0.16f;
                float blushH = headH * 0.10f;
                float blushY = eyeY + eyeH + headH * 0.05f;
                DrawCol(blush, eyeLX - 1f * s, blushY, blushW, blushH);
                DrawCol(blush, eyeRX + eyeW - blushW + 1f * s, blushY, blushW, blushH);
                DrawCol(pupilCol, eyeLX - 1f * s, eyeY - 0.5f * s, eyeW + 2f * s, headH * 0.03f);
                DrawCol(pupilCol, eyeRX - 1f * s, eyeY - 0.5f * s, eyeW + 2f * s, headH * 0.03f);
            }

            // 귀 (headW/headH 비례)
            float earW = headW * 0.08f;
            float earH = headH * 0.20f;
            float earY = eyeY + eyeH * 0.2f;
            DrawCol(skin, headX - earW * 0.6f, earY, earW, earH);
            DrawCol(skin, headX + headW - earW * 0.4f, earY, earW, earH);

            // === 모자 ===
            if (hatColor.a > 0.01f)
            {
                // 챙
                DrawCol(hatColor, headX - 4f * s, headY - 1f * s, headW + 8f * s, 3f * s);
                // 본체
                Color hatDark = new Color(hatColor.r * 0.85f, hatColor.g * 0.85f, hatColor.b * 0.85f);
                DrawCol(hatDark, headX + 1f * s, headY - headH * 0.20f, headW - 2f * s, headH * 0.22f);
            }

            // === 채집봉 (오른쪽 어깨 너머) ===
            Color netCol = new Color(0.2f, 0.12f, 0.06f);
            float netX = cx + bodyW * 0.5f + armW + 2f * s + swayX;
            float netTop = headY - 8f * s;
            DrawCol(netCol, netX, netTop, 3f * s, legH + bodyH * 0.4f);
            // 링
            Color ringCol = new Color(0.9f, 0.9f, 0.85f);
            float ringW = 18f * s;
            float ringH = 14f * s;
            DrawCol(ringCol, netX - 8f * s, netTop - 4f * s, ringW, 2.5f * s);
            DrawCol(ringCol, netX - 8f * s, netTop - 4f * s, 2.5f * s, ringH);
            DrawCol(ringCol, netX + 8f * s, netTop - 4f * s, 2.5f * s, ringH);

            GUI.color = Color.white;
        }

        private static void DrawHair(float headX, float headY, float headW, float headH, float s, int gender, int style, Color hair)
        {
            switch (style)
            {
                case 0: // 짧은 머리
                    DrawCol(hair, headX, headY - headH * 0.10f, headW, headH * 0.32f);
                    DrawCol(hair, headX - headW * 0.02f, headY + 3f * s, headW * 0.13f, headH * 0.45f);
                    DrawCol(hair, headX + headW - headW * 0.11f, headY + 3f * s, headW * 0.13f, headH * 0.45f);
                    if (gender == 1)
                        DrawCol(hair, headX + 2f * s, headY + headH * 0.1f, headW - 4f * s, 5f * s);
                    break;
                case 1: // 중간 머리
                    DrawCol(hair, headX, headY - headH * 0.10f, headW, headH * 0.34f);
                    DrawCol(hair, headX - 2.5f * s, headY + 4f * s, 5f * s, headH * 0.7f);
                    DrawCol(hair, headX + headW - 2.5f * s, headY + 4f * s, 5f * s, headH * 0.7f);
                    DrawCol(hair, headX + 2f * s, headY - 1f * s, headW * 0.5f, 5f * s);
                    break;
                case 2: // 긴 머리
                    DrawCol(hair, headX, headY - headH * 0.10f, headW, headH * 0.36f);
                    DrawCol(hair, headX - 3f * s, headY + 4f * s, 6f * s, headH + 18f * s);
                    DrawCol(hair, headX + headW - 3f * s, headY + 4f * s, 6f * s, headH + 18f * s);
                    DrawCol(new Color(hair.r * 0.85f, hair.g * 0.85f, hair.b * 0.85f),
                        headX + 2f * s, headY + headH * 0.5f, headW - 4f * s, 14f * s);
                    DrawCol(hair, headX + 3f * s, headY - 1f * s, headW * 0.55f, 6f * s);
                    if (gender == 1)
                    {
                        DrawCol(hair, headX + 1f * s, headY + headH * 0.4f, 3f * s, headH);
                        DrawCol(hair, headX + headW - 4f * s, headY + headH * 0.4f, 3f * s, headH);
                    }
                    break;
                case 3: // 올림 머리
                    DrawCol(hair, headX + 1f * s, headY - 4f * s, headW - 2f * s, 10f * s);
                    if (gender == 0)
                    {
                        DrawCol(hair, headX + headW * 0.3f, headY - 12f * s, headW * 0.18f, 10f * s);
                        DrawCol(hair, headX + headW * 0.55f, headY - 9f * s, headW * 0.15f, 8f * s);
                    }
                    else
                    {
                        DrawCol(hair, headX + headW * 0.22f, headY - 14f * s, headW * 0.56f, 12f * s);
                        DrawCol(new Color(0.9f, 0.3f, 0.4f),
                            headX + headW * 0.28f, headY - 8f * s, headW * 0.44f, 3f * s);
                        DrawCol(hair, headX + 3f * s, headY + headH * 0.08f, headW - 6f * s, 5f * s);
                        DrawCol(hair, headX - 1f * s, headY + headH * 0.3f, 3f * s, 12f * s);
                        DrawCol(hair, headX + headW - 2f * s, headY + headH * 0.3f, 3f * s, 12f * s);
                    }
                    break;
            }
        }

        /// <summary>LoginUI용: 선택 옵션 기반 그리기</summary>
        public static void DrawForCreation(float cx, float cy, float scale,
            int gender, int skinColorIdx, int hairColorIdx, int hairStyle, int faceType, int outfitIdx)
        {
            Color[] outfitColors =
            {
                new Color(0.2f, 0.4f, 0.85f),
                new Color(0.9f, 0.9f, 0.92f),
                new Color(0.7f, 0.25f, 0.25f)
            };
            Color topCol = outfitColors[Mathf.Clamp(outfitIdx, 0, 2)];
            Color bottomCol = new Color(0.18f, 0.22f, 0.28f);
            Color shoeCol = new Color(0.2f, 0.12f, 0.06f);
            Color hatCol = new Color(0f, 0f, 0f, 0f);

            Draw(cx, cy, scale, gender, skinColorIdx, hairColorIdx, hairStyle, faceType,
                topCol, bottomCol, shoeCol, hatCol);
        }

        // OutfitChanged 이벤트 기반 캐시 — 매 OnGUI 호출 시 PlayerPrefs 5회 + GetEquipped 8회를
        // 60FPS×13회/초=780회/초 차단. 의상 변경 시 InvalidateCache()가 호출되면 다음 OnGUI에 재캐싱.
        // 캐릭터 외형(LoginUI에서 Gender/Hair 등 PlayerPrefs 변경)은 OutfitChanged 발화 안 되므로
        // LoginUI가 외형 변경 후 InvalidateCache() 직접 호출 필요.
        private struct OutfitCache
        {
            public int gender, skinIdx, hairColorIdx, hairStyle, faceType;
            public Color topCol, outerCol, bottomCol, shoeCol;
            public string hatId, bagId, toolId, accId;
        }
        private static OutfitCache cache;
        private static bool cacheValid;
        // 구독한 매니저 인스턴스 추적 — 씬 전환으로 매니저가 새 인스턴스로 재생성되면 재구독.
        // static 클래스는 OnDisable 없어 -=가 어렵지만, 매니저 destroy시 옛 ref는 GC.
        private static CharacterOutfitManager subscribedMgr;

        /// <summary>OutfitChanged 발화 시 다음 OnGUI에 캐시 재구축 트리거.</summary>
        public static void InvalidateCache()
        {
            cacheValid = false;
        }

        private static void EnsureSubscribed()
        {
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr == null) return;
            if (mgr == subscribedMgr) return;
            // 매니저가 바뀌었으면 옛 구독 해제 후 새로 구독.
            if (subscribedMgr != null) subscribedMgr.OutfitChanged -= InvalidateCache;
            mgr.OutfitChanged += InvalidateCache;
            subscribedMgr = mgr;
            cacheValid = false; // 새 매니저 → 캐시 무효
        }

        private static void RefreshCache()
        {
            cache.gender = PlayerPrefs.GetInt("InsectGame.Character.Gender", 0);
            cache.skinIdx = PlayerPrefs.GetInt("InsectGame.Character.SkinColor", 0);
            cache.hairColorIdx = PlayerPrefs.GetInt("InsectGame.Character.HairColor", 0);
            cache.hairStyle = PlayerPrefs.GetInt("InsectGame.Character.HairStyle", 0);
            cache.faceType = PlayerPrefs.GetInt("InsectGame.Character.FaceType", 0);

            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            cache.topCol = GetEquipColor(mgr, OutfitSlot.Top, new Color(0.98f, 0.96f, 0.92f));
            cache.outerCol = GetEquipColor(mgr, OutfitSlot.Outerwear, new Color(0f, 0f, 0f, 0f));
            cache.bottomCol = GetEquipColor(mgr, OutfitSlot.Bottom, new Color(0.18f, 0.22f, 0.28f));
            cache.shoeCol = GetEquipColor(mgr, OutfitSlot.Shoes, new Color(0.2f, 0.12f, 0.06f));
            cache.hatId = GetEquippedItemId(mgr, OutfitSlot.Hat);
            cache.bagId = GetEquippedItemId(mgr, OutfitSlot.Backpack);
            cache.toolId = GetEquippedItemId(mgr, OutfitSlot.Tool);
            cache.accId = GetEquippedItemId(mgr, OutfitSlot.Accessory);
            cacheValid = true;
        }

        /// <summary>CharacterViewerUI용: 장착 의상 기반 그리기</summary>
        public static void DrawWithOutfit(float cx, float cy, float scale, float swayX = 0f)
        {
            EnsureSubscribed();
            if (!cacheValid) RefreshCache();

            int gender = cache.gender;
            int skinIdx = cache.skinIdx;
            int hairColorIdx = cache.hairColorIdx;
            int hairStyle = cache.hairStyle;
            int faceType = cache.faceType;
            Color topCol = cache.topCol;
            Color outerCol = cache.outerCol;
            Color bottomCol = cache.bottomCol;
            Color shoeCol = cache.shoeCol;
            string hatId = cache.hatId;
            string bagId = cache.bagId;
            string toolId = cache.toolId;
            string accId = cache.accId;
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            // hat_none 이거나 기본 캡이 아니면 Draw 내부 모자는 그리지 않고 형태 분기로 덮어그림
            bool customHat = !string.IsNullOrEmpty(hatId) && hatId != "hat_cap";
            Color hatCol = customHat ? new Color(0f, 0f, 0f, 0f)
                                     : GetEquipColor(mgr, OutfitSlot.Hat, new Color(0f, 0f, 0f, 0f));

            // Outerwear가 장착되고 outer_none(alpha 0)이 아니면 몸통은 자켓 색으로 표시.
            // outer_none/미장착이면 셔츠(Top) 색이 몸통에 보임 — PlayerVisualBuilder.ApplyToCharacter와 정합.
            bool outerEquipped = outerCol.a > 0.01f;
            Color bodyDisplayCol = outerEquipped ? outerCol : topCol;

            // drawDefaultPack=false: Draw 내부 하드코딩 주황 배낭 비활성. 자체 슬롯 색으로 그림.
            Draw(cx, cy, scale, gender, skinIdx, hairColorIdx, hairStyle, faceType,
                bodyDisplayCol, bottomCol, shoeCol, hatCol, swayX, drawDefaultPack: false);

            // outer_none 시 팔=피부색 오버레이 (실제 캐릭터 ApplyToCharacter의 ArmL/R=skinColor와 정합).
            // 자켓 입을 때 Draw가 자켓 색으로 그린 팔이 보이는 게 정상.
            if (!outerEquipped)
            {
                DrawArmsAsSkin(cx, cy, scale, gender, skinIdx, swayX);
            }

            // Backpack 슬롯 색으로 배낭 그림 (미장착이면 안 그림 — 실제 캐릭터 ApplyToCharacter와 정합)
            DrawBackpackWithSlot(cx, cy, scale, gender, swayX, mgr);

            // 의상별 형태 분기 (모자/도구 마커 + Accessory 오버레이)
            DrawOutfitAccessories(cx, cy, scale, swayX, mgr, hatId, bagId, toolId, accId);
        }

        private static void DrawBackpackWithSlot(float cx, float cy, float scale, int gender, float swayX, CharacterOutfitManager mgr)
        {
            if (mgr == null) return;
            OutfitItem bag = mgr.GetEquipped(OutfitSlot.Backpack);
            if (bag == null || bag.primaryColor.a < 0.01f) return; // 미장착/투명 → 안 그림

            // 공용 비례 헬퍼 — Draw와 자동 동기
            Proportions p = CalculateProportions(cy, scale, gender);
            Color packCol = bag.primaryColor;
            DrawCol(packCol, cx + p.bodyW * 0.36f + swayX, p.bodyTop + 8f * p.s, 14f * p.s, p.bodyH * 0.55f);
            DrawCol(new Color(packCol.r * 0.7f, packCol.g * 0.7f, packCol.b * 0.7f),
                cx + p.bodyW * 0.3f + swayX, p.bodyTop + 2f * p.s, 3f * p.s, p.bodyH * 0.45f);
        }

        private static void DrawArmsAsSkin(float cx, float cy, float scale, int gender, int skinIdx, float swayX)
        {
            // 공용 비례 헬퍼 — Draw와 자동 동기
            Proportions p = CalculateProportions(cy, scale, gender);
            Color skinCol = GetSkinColor(skinIdx);
            DrawCol(skinCol, cx - p.bodyW * 0.5f - p.armW + swayX, p.bodyTop + 4f * p.s, p.armW, p.armH);
            DrawCol(skinCol, cx + p.bodyW * 0.5f + swayX, p.bodyTop + 4f * p.s, p.armW, p.armH);
        }

        private static Color GetEquipColor(CharacterOutfitManager mgr, OutfitSlot slot, Color fallback)
        {
            if (mgr == null) return fallback;
            OutfitItem item = mgr.GetEquipped(slot);
            return item != null ? item.primaryColor : fallback;
        }

        private static Color GetEquipSecondaryColor(CharacterOutfitManager mgr, OutfitSlot slot, Color fallback)
        {
            if (mgr == null) return fallback;
            OutfitItem item = mgr.GetEquipped(slot);
            return item != null && item.secondaryColor.a > 0.01f ? item.secondaryColor : fallback;
        }

        // 캐릭터 미리보기 옆에 도구 silhouette 표시 — CharacterOutfitManager.ApplyToolShape 분기와 정합.
        // 좌표: 오른팔 옆(cx + bodyW*0.5 + armW + offset). 필드 손 ±0.34 위치 시각 매칭.
        private static void DrawCharacterTool(float cx, float swayX, float bodyW, float headY, float headH, float s,
            string id, Color c, Color sec)
        {
            float toolX = cx + bodyW * 0.5f + 8f * s + swayX; // 손 중앙(짧은 치비 팔 armW≈15s의 절반)
            float armBaseY = headY + headH + 4f * s; // 어깨 부근 시작
            // 손 부근 위치 (DrawWithOutfit의 손 좌표와 정합: bodyTop + 4s + armH)
            // 치비: 짧아진 팔(armH 58→34)에 맞춰 손 위치 동기(옛 58은 손보다 ~24s 아래 허공).
            // 실제 손 = bodyTop+4s+armH ≈ armBaseY+36s (neckH 2 포함).
            float handY = armBaseY + 36f * s;

            if (id.Contains("gun") || id.Contains("blaster") || id.Contains("tranq"))
            {
                // 총 — 박스 본체 + 총구
                DrawCol(c, toolX - 6f * s, handY - 4f * s, 18f * s, 8f * s);
                DrawCol(c, toolX - 2f * s, handY + 4f * s, 5f * s, 7f * s);
                DrawCol(sec, toolX + 12f * s, handY - 3f * s, 4f * s, 6f * s);
            }
            else if (id.Contains("wand"))
            {
                // 지팡이 — 가는 막대 + 오브
                DrawCol(c, toolX - 1f * s, handY - 20f * s, 2.5f * s, 28f * s);
                DrawCol(sec, toolX - 5f * s, handY - 28f * s, 12f * s, 12f * s);
            }
            else if (id.Contains("lasso"))
            {
                // 올가미 — 짧은 핸들 + 큰 링 (사각 윤곽)
                DrawCol(c, toolX, handY - 4f * s, 3f * s, 14f * s);
                Color rope = sec.a > 0.01f ? sec : new Color(0.9f, 0.85f, 0.6f);
                DrawCol(rope, toolX - 4f * s, handY - 20f * s, 16f * s, 2f * s);
                DrawCol(rope, toolX - 4f * s, handY - 6f * s, 16f * s, 2f * s);
                DrawCol(rope, toolX - 4f * s, handY - 20f * s, 2f * s, 14f * s);
                DrawCol(rope, toolX + 10f * s, handY - 20f * s, 2f * s, 14f * s);
            }
            else if (id.Contains("shuriken"))
            {
                // 수리검 — 십자 별
                DrawCol(c, toolX - 7f * s, handY - 1f * s, 14f * s, 3f * s);
                DrawCol(c, toolX - 1f * s, handY - 7f * s, 3f * s, 14f * s);
                DrawCol(sec, toolX - 2f * s, handY - 2f * s, 4f * s, 4f * s);
            }
            else if (id.Contains("cutlass") || id.Contains("sword"))
            {
                // 검 — 박스 손잡이 + 긴 칼날
                DrawCol(c, toolX, handY - 2f * s, 4f * s, 10f * s);
                Color blade = sec.a > 0.01f ? sec : new Color(0.85f, 0.85f, 0.9f);
                DrawCol(blade, toolX + 0.5f * s, handY - 30f * s, 3f * s, 28f * s);
            }
            else if (id.Contains("web_shooter"))
            {
                // 발사기 — 손목 박스 + 발사구
                DrawCol(c, toolX - 4f * s, handY - 4f * s, 12f * s, 8f * s);
                Color noz = sec.a > 0.01f ? sec : new Color(0.5f, 0.5f, 0.5f);
                DrawCol(noz, toolX + 8f * s, handY - 2f * s, 4f * s, 4f * s);
            }
            else if (id.Contains("magnify"))
            {
                // 돋보기 — 가는 막대 + 원형 렌즈 (사각 윤곽)
                DrawCol(c, toolX, handY + 6f * s, 3f * s, 14f * s);
                DrawCol(c, toolX - 5f * s, handY - 8f * s, 14f * s, 2f * s);
                DrawCol(c, toolX - 5f * s, handY + 4f * s, 14f * s, 2f * s);
                DrawCol(c, toolX - 5f * s, handY - 8f * s, 2f * s, 12f * s);
                DrawCol(c, toolX + 7f * s, handY - 8f * s, 2f * s, 12f * s);
                Color lens = sec.a > 0.01f ? sec : new Color(0.7f, 0.9f, 1f);
                DrawCol(lens, toolX - 3f * s, handY - 6f * s, 10f * s, 8f * s);
            }
            else if (id.Contains("camera"))
            {
                // 카메라 — 박스 본체 + 렌즈
                DrawCol(c, toolX - 6f * s, handY - 5f * s, 16f * s, 10f * s);
                Color lens = sec.a > 0.01f ? sec : new Color(0.3f, 0.3f, 0.35f);
                DrawCol(lens, toolX - 1f * s, handY - 2f * s, 6f * s, 6f * s);
                DrawCol(new Color(0.95f, 0.95f, 0.85f), toolX + 7f * s, handY - 4f * s, 2f * s, 2f * s);
            }
            else
            {
                // 기본 잠자리채 — 막대 + 위쪽 디스크
                DrawCol(c, toolX, handY - 8f * s, 3f * s, 20f * s);
                Color ring = sec.a > 0.01f ? sec : new Color(0.95f, 0.92f, 0.88f);
                DrawCol(ring, toolX - 6f * s, handY - 20f * s, 16f * s, 2f * s);
                DrawCol(ring, toolX - 6f * s, handY - 6f * s, 16f * s, 2f * s);
                DrawCol(ring, toolX - 6f * s, handY - 20f * s, 2f * s, 14f * s);
                DrawCol(ring, toolX + 8f * s, handY - 20f * s, 2f * s, 14f * s);
            }
        }

        private static string GetEquippedItemId(CharacterOutfitManager mgr, OutfitSlot slot)
        {
            if (mgr == null) return "";
            OutfitItem item = mgr.GetEquipped(slot);
            return item != null ? item.itemId : "";
        }

        // Draw 호출 후 호출. 특수 itemId에만 형태를 덮어 그림.
        private static void DrawOutfitAccessories(float cx, float cy, float scale, float swayX,
            CharacterOutfitManager mgr, string hatId, string bagId, string toolId, string accId = "")
        {
            // 공용 비례 헬퍼 — Draw와 자동 동기
            int gender = PlayerPrefs.GetInt("InsectGame.Character.Gender", 0);
            Proportions p = CalculateProportions(cy, scale, gender);
            float s = p.s;
            float headW = p.headW, headH = p.headH, neckH = p.neckH;
            float bodyW = p.bodyW, bodyH = p.bodyH;
            float headY = p.headY, bodyTop = p.bodyTop;
            float headX = cx - headW * 0.5f + swayX;

            // 모자 형태 분기
            if (!string.IsNullOrEmpty(hatId) && hatId != "hat_cap" && hatId != "hat_none")
            {
                Color hatCol = GetEquipColor(mgr, OutfitSlot.Hat, new Color(0.9f, 0.6f, 0.3f));
                Color hatDark = new Color(hatCol.r * 0.7f, hatCol.g * 0.7f, hatCol.b * 0.7f);

                if (hatId == "hat_wizard")
                {
                    // 마법사 모자: 삼각형 (cube 3개로 근사)
                    DrawCol(hatCol, headX - 4f * s, headY - 3f * s, headW + 8f * s, 3f * s);
                    DrawCol(hatCol, headX + 3f * s, headY - 9f * s, headW - 6f * s, 6f * s);
                    DrawCol(hatCol, headX + 9f * s, headY - 15f * s, headW - 18f * s, 6f * s);
                    DrawCol(new Color(1f, 0.9f, 0.3f), headX + headW * 0.45f, headY - 19f * s, 4f * s, 4f * s);
                }
                else if (hatId == "hat_straw")
                {
                    // 밀짚모자: 매우 넓은 챙
                    DrawCol(hatCol, headX - 10f * s, headY - 1f * s, headW + 20f * s, 4f * s);
                    DrawCol(hatDark, headX + 2f * s, headY - 7f * s, headW - 4f * s, 7f * s);
                }
                else if (hatId == "hat_beanie")
                {
                    // 비니: 챙 없는 둥근 모자
                    DrawCol(hatCol, headX, headY - 6f * s, headW, 8f * s);
                    DrawCol(hatDark, headX, headY - 1f * s, headW, 2f * s);
                }
                else
                {
                    // 기본 형태 (캡 스타일)
                    DrawCol(hatCol, headX - 4f * s, headY - 1f * s, headW + 8f * s, 3f * s);
                    DrawCol(hatDark, headX + 1f * s, headY - headH * 0.20f, headW - 2f * s, headH * 0.22f);
                }
            }

            // 가방 마커 (bag_science는 큰 가방)
            if (bagId == "bag_science")
            {
                Color bagCol = new Color(0.4f, 0.5f, 0.7f);
                DrawCol(bagCol, cx + bodyW * 0.42f + swayX, bodyTop + 14f * s, 18f * s, bodyH * 0.55f);
                DrawCol(new Color(0.9f, 0.9f, 0.95f), cx + bodyW * 0.5f + swayX, bodyTop + 20f * s, 6f * s, 6f * s);
            }

            // 도구 마커 — 캐릭터 미리보기에 9종 도구 모두 표시.
            // 옛은 tool_magnify만 처리 → 총/지팡이/올가미/검 등 장착해도 미리보기에 안 보였음.
            // 좌표: 오른팔 옆 (cx + bodyW*0.5 + armW + 일부). 필드 ApplyToolShape의 손 ±0.34 위치와 정합.
            if (!string.IsNullOrEmpty(toolId) && toolId != "tool_none")
            {
                Color toolPrimary = GetEquipColor(mgr, OutfitSlot.Tool, new Color(0.6f, 0.4f, 0.2f));
                Color toolSec = GetEquipSecondaryColor(mgr, OutfitSlot.Tool, new Color(0.95f, 0.92f, 0.88f));
                if (toolPrimary.a > 0.01f)
                {
                    DrawCharacterTool(cx, swayX, bodyW, headY, headH, s, toolId, toolPrimary, toolSec);
                }
            }

            // ── Accessory (PlayerVisualBuilder.ApplyAccessory와 동일 분기) ──
            if (!string.IsNullOrEmpty(accId) && accId != "acc_none")
            {
                Color accCol = GetEquipColor(mgr, OutfitSlot.Accessory, new Color(0.1f, 0.1f, 0.1f));
                if (accCol.a > 0.01f)
                {
                    if (accId.Contains("glasses") || accId.Contains("visor") || accId.Contains("eyepatch"))
                    {
                        // 안경/바이저/안대 — 큰 치비 눈(headW*0.30)을 덮도록 비례. 기본 얼굴 눈 위치와 정합.
                        float eyeY_local = headY + headH * 0.40f;
                        float eyeLX_local = headX + headW * 0.16f;
                        float eyeRX_local = headX + headW * 0.54f;
                        float lensW = headW * 0.32f;
                        float lensH = headH * 0.30f;
                        DrawCol(accCol, eyeLX_local, eyeY_local, lensW, lensH);
                        // eyepatch는 한쪽만
                        if (!accId.Contains("eyepatch"))
                            DrawCol(accCol, eyeRX_local, eyeY_local, lensW, lensH);
                        // 브릿지(코받침) — 두 렌즈 사이
                        DrawCol(accCol, headX + headW * 0.46f, eyeY_local + lensH * 0.4f, headW * 0.08f, headH * 0.05f);
                    }
                    else if (accId.Contains("necklace") || accId.Contains("pendant")
                          || accId.Contains("orb") || accId.Contains("crystal_orb"))
                    {
                        // 목걸이 — 목 아래 작은 사각/원
                        float neckY = headY + headH + 1f * s;
                        DrawCol(accCol, cx - 3f * s + swayX, neckY + 2f * s, 6f * s, 4f * s);
                    }
                    else
                    {
                        // 스카프/배지/엠블럼/완장 등 기타 — 가슴팍 작은 사각형
                        float chestY = headY + headH + neckH + 6f * s;
                        DrawCol(accCol, cx - 5f * s + swayX, chestY, 10f * s, 6f * s);
                    }
                }
            }
        }

        public static Color GetSkinColor(int idx)
        {
            Color[] colors =
            {
                new Color(1.0f, 0.87f, 0.75f),
                new Color(0.9f, 0.75f, 0.6f),
                new Color(0.65f, 0.5f, 0.35f),
                new Color(0.4f, 0.28f, 0.18f)
            };
            return colors[Mathf.Clamp(idx, 0, colors.Length - 1)];
        }

        public static Color GetHairColor(int idx)
        {
            Color[] colors =
            {
                new Color(0.12f, 0.08f, 0.05f),
                new Color(0.35f, 0.2f, 0.1f),
                new Color(0.85f, 0.7f, 0.3f),
                new Color(0.6f, 0.15f, 0.1f),
                new Color(0.2f, 0.15f, 0.35f),
                new Color(0.15f, 0.3f, 0.5f)
            };
            return colors[Mathf.Clamp(idx, 0, colors.Length - 1)];
        }

        private static void DrawCol(Color col, float x, float y, float w, float h)
        {
            GUI.color = col;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        }

        private static void DrawRect(float x, float y, float w, float h)
        {
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        }

        /// <summary>
        /// 아이템 카드 미리보기 — 100×100 영역에 슬롯/itemId별 실제 형태를 그림.
        /// 옛 GetSlotSymbol은 "^" "T" 같은 텍스트라 어떤 아이템인지 알 수 없었음.
        /// 호출자는 색 배경 + 테두리만 그린 뒤 이 메서드로 형태 오버레이.
        /// </summary>
        public static void DrawItemPreview(Rect r, OutfitSlot slot, string itemId, Color primary, Color secondary)
        {
            if (primary.a < 0.01f)
            {
                // 색 없음 = none 슬롯, "---"로 처리는 호출자
                return;
            }
            string id = itemId ?? "";
            Color prevCol = GUI.color;
            GUI.color = Color.white;

            switch (slot)
            {
                case OutfitSlot.Hat: DrawHatPreview(r, id, primary, secondary); break;
                case OutfitSlot.Top: DrawTopPreview(r, primary, secondary); break;
                case OutfitSlot.Bottom: DrawBottomPreview(r, primary); break;
                case OutfitSlot.Outerwear: DrawOuterwearPreview(r, primary, secondary); break;
                case OutfitSlot.Shoes: DrawShoesPreview(r, primary); break;
                case OutfitSlot.Backpack: DrawBackpackPreview(r, primary, secondary); break;
                case OutfitSlot.Tool: DrawToolPreview(r, id, primary, secondary); break;
                case OutfitSlot.Accessory: DrawAccessoryPreview(r, id, primary); break;
            }

            GUI.color = prevCol;
        }

        private static void DrawHatPreview(Rect r, string id, Color c, Color sec)
        {
            float cx = r.x + r.width * 0.5f;
            float top = r.y + r.height * 0.25f;
            float headW = r.width * 0.5f;
            float headH = r.height * 0.5f;
            Color skin = new Color(0.95f, 0.82f, 0.68f);
            // 머리 베이스 (참고용)
            DrawCol(skin, cx - headW * 0.5f, top, headW, headH);
            Color dark = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f);
            if (id.Contains("wizard"))
            {
                DrawCol(c, cx - headW * 0.6f, top, headW * 1.2f, r.height * 0.06f);
                DrawCol(c, cx - headW * 0.4f, top - r.height * 0.12f, headW * 0.8f, r.height * 0.12f);
                DrawCol(c, cx - headW * 0.2f, top - r.height * 0.24f, headW * 0.4f, r.height * 0.12f);
                DrawCol(new Color(1f, 0.9f, 0.3f), cx - r.width * 0.04f, top - r.height * 0.32f, r.width * 0.08f, r.height * 0.08f);
            }
            else if (id.Contains("straw"))
            {
                DrawCol(c, cx - headW * 0.85f, top + r.height * 0.02f, headW * 1.7f, r.height * 0.07f);
                DrawCol(dark, cx - headW * 0.42f, top - r.height * 0.13f, headW * 0.85f, r.height * 0.15f);
            }
            else if (id.Contains("beanie"))
            {
                DrawCol(c, cx - headW * 0.55f, top - r.height * 0.13f, headW * 1.1f, r.height * 0.18f);
                DrawCol(dark, cx - headW * 0.55f, top - r.height * 0.02f, headW * 1.1f, r.height * 0.04f);
            }
            else
            {
                // 기본 캡
                DrawCol(c, cx - headW * 0.6f, top, headW * 1.2f, r.height * 0.06f);
                DrawCol(dark, cx - headW * 0.5f, top - r.height * 0.15f, headW, r.height * 0.15f);
            }
        }

        private static void DrawTopPreview(Rect r, Color c, Color sec)
        {
            float cx = r.x + r.width * 0.5f;
            float top = r.y + r.height * 0.2f;
            float bodyW = r.width * 0.55f;
            float bodyH = r.height * 0.55f;
            DrawCol(c, cx - bodyW * 0.5f, top, bodyW, bodyH);
            // 칼라
            Color collar = Color.Lerp(c, Color.white, 0.3f);
            DrawCol(collar, cx - bodyW * 0.18f, top, bodyW * 0.36f, r.height * 0.08f);
            // 소매
            DrawCol(c, cx - bodyW * 0.7f, top + r.height * 0.05f, bodyW * 0.2f, bodyH * 0.5f);
            DrawCol(c, cx + bodyW * 0.5f, top + r.height * 0.05f, bodyW * 0.2f, bodyH * 0.5f);
        }

        private static void DrawBottomPreview(Rect r, Color c)
        {
            float cx = r.x + r.width * 0.5f;
            float top = r.y + r.height * 0.2f;
            float legW = r.width * 0.18f;
            float legH = r.height * 0.6f;
            DrawCol(c, cx - legW - r.width * 0.04f, top, legW, legH);
            DrawCol(c, cx + r.width * 0.04f, top, legW, legH);
            // 허리 밴드
            Color dark = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f);
            DrawCol(dark, cx - legW * 1.3f - r.width * 0.04f, top, legW * 2.6f + r.width * 0.08f, r.height * 0.06f);
        }

        private static void DrawOuterwearPreview(Rect r, Color c, Color sec)
        {
            float cx = r.x + r.width * 0.5f;
            float top = r.y + r.height * 0.18f;
            float bodyW = r.width * 0.6f;
            float bodyH = r.height * 0.6f;
            // 자켓 외피
            DrawCol(c, cx - bodyW * 0.5f, top, bodyW, bodyH);
            // V넥 (셔츠 영역)
            Color innerCol = sec.a > 0.01f ? sec : Color.Lerp(c, Color.white, 0.5f);
            DrawCol(innerCol, cx - bodyW * 0.12f, top, bodyW * 0.24f, bodyH * 0.4f);
            // 단추 2개
            Color btn = new Color(0.2f, 0.2f, 0.2f);
            DrawCol(btn, cx - r.width * 0.02f, top + bodyH * 0.45f, r.width * 0.04f, r.width * 0.04f);
            DrawCol(btn, cx - r.width * 0.02f, top + bodyH * 0.65f, r.width * 0.04f, r.width * 0.04f);
        }

        private static void DrawShoesPreview(Rect r, Color c)
        {
            float cx = r.x + r.width * 0.5f;
            float top = r.y + r.height * 0.35f;
            float shoeW = r.width * 0.30f;
            float shoeH = r.height * 0.25f;
            // 두 신발 옆모습 (앞코 + 발등)
            DrawCol(c, cx - shoeW - r.width * 0.06f, top + shoeH * 0.6f, shoeW, shoeH * 0.6f);
            DrawCol(c, cx + r.width * 0.06f, top + shoeH * 0.6f, shoeW, shoeH * 0.6f);
            // 발등 (살짝 위로)
            DrawCol(c, cx - shoeW * 0.9f - r.width * 0.06f, top + shoeH * 0.2f, shoeW * 0.7f, shoeH * 0.4f);
            DrawCol(c, cx + r.width * 0.06f + shoeW * 0.2f, top + shoeH * 0.2f, shoeW * 0.7f, shoeH * 0.4f);
            // 끈
            Color dark = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f);
            DrawCol(dark, cx - shoeW * 0.7f - r.width * 0.06f, top + shoeH * 0.3f, shoeW * 0.5f, r.height * 0.02f);
            DrawCol(dark, cx + r.width * 0.06f + shoeW * 0.3f, top + shoeH * 0.3f, shoeW * 0.5f, r.height * 0.02f);
        }

        private static void DrawBackpackPreview(Rect r, Color c, Color sec)
        {
            float cx = r.x + r.width * 0.5f;
            float top = r.y + r.height * 0.18f;
            float bagW = r.width * 0.55f;
            float bagH = r.height * 0.6f;
            // 본체
            DrawCol(c, cx - bagW * 0.5f, top, bagW, bagH);
            // 상단 손잡이
            Color dark = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f);
            DrawCol(dark, cx - bagW * 0.15f, top - r.height * 0.06f, bagW * 0.3f, r.height * 0.06f);
            // 앞 주머니
            Color front = sec.a > 0.01f ? sec : Color.Lerp(c, Color.white, 0.25f);
            DrawCol(front, cx - bagW * 0.3f, top + bagH * 0.5f, bagW * 0.6f, bagH * 0.3f);
            // 어깨끈 2줄
            DrawCol(dark, cx - bagW * 0.45f, top, r.width * 0.04f, bagH);
            DrawCol(dark, cx + bagW * 0.45f - r.width * 0.04f, top, r.width * 0.04f, bagH);
        }

        private static void DrawToolPreview(Rect r, string id, Color c, Color sec)
        {
            float cx = r.x + r.width * 0.5f;
            float cy = r.y + r.height * 0.5f;
            float w = r.width;
            float h = r.height;
            if (id.Contains("gun") || id.Contains("blaster") || id.Contains("tranq"))
            {
                // 총 — 가로 본체 + 손잡이
                DrawCol(c, cx - w * 0.3f, cy - h * 0.05f, w * 0.6f, h * 0.18f);
                DrawCol(c, cx - w * 0.15f, cy + h * 0.13f, w * 0.12f, h * 0.25f);
                // 총구
                DrawCol(sec.a > 0.01f ? sec : Color.gray, cx + w * 0.28f, cy - h * 0.02f, w * 0.06f, h * 0.12f);
            }
            else if (id.Contains("wand"))
            {
                // 지팡이 — 세로 긴 막대 + 오브
                DrawCol(c, cx - w * 0.04f, cy - h * 0.15f, w * 0.08f, h * 0.5f);
                Color orb = sec.a > 0.01f ? sec : new Color(0.6f, 0.85f, 1f);
                DrawCol(orb, cx - w * 0.12f, cy - h * 0.32f, w * 0.24f, h * 0.18f);
            }
            else if (id.Contains("lasso"))
            {
                // 올가미 — 짧은 핸들 + 큰 링
                DrawCol(c, cx - w * 0.03f, cy - h * 0.05f, w * 0.06f, h * 0.25f);
                Color rope = sec.a > 0.01f ? sec : new Color(0.9f, 0.85f, 0.6f);
                // 링 (사각 테두리로 근사)
                float ringX = cx - w * 0.2f;
                float ringY = cy - h * 0.3f;
                float ringW = w * 0.4f;
                float ringH = h * 0.25f;
                DrawCol(rope, ringX, ringY, ringW, h * 0.04f);
                DrawCol(rope, ringX, ringY + ringH - h * 0.04f, ringW, h * 0.04f);
                DrawCol(rope, ringX, ringY, w * 0.05f, ringH);
                DrawCol(rope, ringX + ringW - w * 0.05f, ringY, w * 0.05f, ringH);
            }
            else if (id.Contains("shuriken"))
            {
                // 수리검 — 십자 별
                float armW = w * 0.4f;
                float armH = h * 0.1f;
                DrawCol(c, cx - armW * 0.5f, cy - armH * 0.5f, armW, armH);
                DrawCol(c, cx - armH * 0.5f, cy - armW * 0.5f, armH, armW);
                // 대각 (보조색)
                Color cross = sec.a > 0.01f ? sec : Color.Lerp(c, Color.white, 0.3f);
                DrawCol(cross, cx - w * 0.06f, cy - h * 0.06f, w * 0.12f, h * 0.12f);
            }
            else if (id.Contains("cutlass") || id.Contains("sword"))
            {
                // 검 — 긴 칼날 + 가드 + 손잡이
                DrawCol(c, cx - w * 0.04f, cy - h * 0.4f, w * 0.08f, h * 0.55f);
                Color guard = sec.a > 0.01f ? sec : new Color(0.7f, 0.55f, 0.2f);
                DrawCol(guard, cx - w * 0.18f, cy + h * 0.15f, w * 0.36f, h * 0.05f);
                DrawCol(guard, cx - w * 0.05f, cy + h * 0.2f, w * 0.1f, h * 0.2f);
            }
            else if (id.Contains("web_shooter"))
            {
                // 발사기 — 손목 박스 + 발사구
                DrawCol(c, cx - w * 0.18f, cy - h * 0.1f, w * 0.36f, h * 0.22f);
                Color nozzle = sec.a > 0.01f ? sec : Color.gray;
                DrawCol(nozzle, cx + w * 0.18f, cy - h * 0.04f, w * 0.12f, h * 0.1f);
            }
            else if (id.Contains("magnify"))
            {
                // 돋보기 — 원형 렌즈(사각 근사) + 핸들
                float lensSize = w * 0.4f;
                DrawCol(c, cx - lensSize * 0.5f, cy - lensSize * 0.5f - h * 0.05f, lensSize, lensSize);
                Color glass = sec.a > 0.01f ? sec : new Color(0.7f, 0.9f, 1f, 0.7f);
                DrawCol(glass, cx - lensSize * 0.35f, cy - lensSize * 0.35f - h * 0.05f, lensSize * 0.7f, lensSize * 0.7f);
                DrawCol(c, cx + lensSize * 0.3f, cy + lensSize * 0.3f - h * 0.05f, w * 0.08f, h * 0.25f);
            }
            else if (id.Contains("camera"))
            {
                // 카메라 — 박스 본체 + 렌즈 + 플래시
                DrawCol(c, cx - w * 0.25f, cy - h * 0.15f, w * 0.5f, h * 0.3f);
                Color lens = sec.a > 0.01f ? sec : new Color(0.3f, 0.3f, 0.35f);
                DrawCol(lens, cx - w * 0.08f, cy - h * 0.05f, w * 0.16f, h * 0.16f);
                // 플래시
                DrawCol(new Color(0.95f, 0.95f, 0.85f), cx + w * 0.15f, cy - h * 0.13f, w * 0.05f, h * 0.04f);
            }
            else
            {
                // 기본 잠자리채 — 핸들 + 위쪽 링
                DrawCol(c, cx - w * 0.03f, cy - h * 0.05f, w * 0.06f, h * 0.45f);
                Color ring = sec.a > 0.01f ? sec : new Color(0.95f, 0.92f, 0.88f);
                float ringX = cx - w * 0.18f;
                float ringY = cy - h * 0.4f;
                float ringW = w * 0.36f;
                float ringH = h * 0.18f;
                DrawCol(ring, ringX, ringY, ringW, h * 0.03f);
                DrawCol(ring, ringX, ringY + ringH - h * 0.03f, ringW, h * 0.03f);
                DrawCol(ring, ringX, ringY, w * 0.04f, ringH);
                DrawCol(ring, ringX + ringW - w * 0.04f, ringY, w * 0.04f, ringH);
            }
        }

        private static void DrawAccessoryPreview(Rect r, string id, Color c)
        {
            float cx = r.x + r.width * 0.5f;
            float cy = r.y + r.height * 0.5f;
            float w = r.width;
            float h = r.height;
            if (id.Contains("glasses") || id.Contains("visor") || id.Contains("eyepatch"))
            {
                // 안경 — 동그란 렌즈 2개 + 다리
                float lensW = w * 0.25f;
                float lensH = h * 0.22f;
                DrawCol(c, cx - w * 0.3f, cy - lensH * 0.5f, lensW, lensH);
                if (!id.Contains("eyepatch"))
                    DrawCol(c, cx + w * 0.05f, cy - lensH * 0.5f, lensW, lensH);
                // 브릿지
                DrawCol(c, cx - w * 0.05f, cy - h * 0.03f, w * 0.1f, h * 0.04f);
            }
            else if (id.Contains("necklace") || id.Contains("pendant") || id.Contains("orb") || id.Contains("crystal_orb"))
            {
                // 목걸이 — 줄 + 펜던트
                DrawCol(Color.Lerp(c, Color.white, 0.3f), cx - w * 0.25f, cy - h * 0.2f, w * 0.5f, h * 0.04f);
                DrawCol(c, cx - w * 0.1f, cy - h * 0.05f, w * 0.2f, h * 0.25f);
            }
            else if (id.Contains("scarf") || id.Contains("muffler") || id.Contains("bandana"))
            {
                // 스카프 — 목 둘레 띠 + 매듭
                DrawCol(c, cx - w * 0.35f, cy - h * 0.1f, w * 0.7f, h * 0.18f);
                DrawCol(Color.Lerp(c, Color.black, 0.2f), cx - w * 0.06f, cy + h * 0.05f, w * 0.12f, h * 0.2f);
            }
            else
            {
                // 배지/엠블럼/완장 등 기본 — 가슴팍 작은 큐브 + 핀
                DrawCol(c, cx - w * 0.18f, cy - h * 0.18f, w * 0.36f, h * 0.36f);
                DrawCol(Color.Lerp(c, Color.white, 0.5f), cx - w * 0.08f, cy - h * 0.08f, w * 0.16f, h * 0.16f);
            }
        }
    }
}
