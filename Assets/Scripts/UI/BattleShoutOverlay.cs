using System.Collections.Generic;
using InsectGame.Battle;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 전투 외침 그리기 — 기술 이름 외치기(말풍선), 맞은 쪽 비명, 타격 의성어("콰광!!").
    /// 목록과 타이밍의 주인은 <see cref="BattleArenaController"/>이고 여기선 그리기만 한다
    /// (<see cref="BattleEffectTextOverlay"/>와 같은 분업 — 아레나는 UI를 부를 수 없다).
    ///
    /// 위치는 외친 순간의 월드 점을 매 프레임 다시 투영한다. 연출 카메라가 움직여도 말풍선이
    /// 그 곤충 머리 위에 붙어 있고, 돌진하는 시전자를 따라 화면을 가로지르지는 않는다.
    /// 시계는 실제 시간이다 — 히트스톱 중에도 의성어가 튀어나오는 게 보여야 한다.
    /// </summary>
    public static class BattleShoutOverlay
    {
        private static GUIStyle calloutStyle;
        private static GUIStyle hurtStyle;
        private static GUIStyle soundStyle;
        private static readonly GUIContent measure = new GUIContent();

        private const int CalloutFont = 40;
        private const int HurtFont = 32;
        private const int SoundFont = 62;
        private const int HeavySoundFont = 80;
        private const float CalloutMaxWidth = 760f;
        /// <summary>상단 HP 상자 높이만큼 비운다(가상 px).</summary>
        private const float HudClearance = 140f;

        /// <summary>
        /// 호출부는 <c>UIScale.Begin()</c>과 <c>UIScale.End()</c> <b>사이</b>에서 부른다 — 가상 캔버스 좌표다.
        /// </summary>
        public static void Draw(BattleArenaController arena)
        {
            if (arena == null || !arena.IsActive) return;
            IReadOnlyList<BattleShout.Entry> shouts = arena.GetActiveShouts();
            if (shouts == null || shouts.Count == 0) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            EnsureStyles();

            float now = Time.unscaledTime;
            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;
            // 의성어를 먼저(아래) 그린다 — 큰 의성어가 위에 오면 말풍선의 기술 이름·비명이 가려진다.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < shouts.Count; i++)
                {
                    BattleShout.Entry e = shouts[i];
                    if (e == null) continue;
                    bool soundPass = e.Kind == BattleShout.Kind.Sound;
                    if (soundPass != (pass == 0)) continue;
                    float age = now - e.StartTime;
                    if (age < 0f || age >= e.Duration) continue;
                    Vector3 sp = cam.WorldToScreenPoint(e.WorldPoint);
                    if (sp.z <= 0f) continue;
                    Vector2 p = new Vector2(
                        sp.x / Mathf.Max(1, Screen.width) * UIScale.VirtualScreenWidth,
                        (1f - sp.y / Mathf.Max(1, Screen.height)) * UIScale.VirtualScreenHeight);
                    float fade = Mathf.Clamp01((e.Duration - age) / 0.25f);
                    switch (e.Kind)
                    {
                        case BattleShout.Kind.Callout: DrawCallout(p, e, age, fade); break;
                        case BattleShout.Kind.Hurt: DrawHurt(p, e, age, fade); break;
                        default: DrawSound(p, e, age, fade); break;
                    }
                    GUI.matrix = prevMatrix;
                }
            }
            GUI.color = prevColor;
        }

        /// <summary>기술 이름 외치기 — 머리 위 말풍선이 튀어나와(0.12초 과장 후 정착) 꼬리로 곤충을 가리킨다.</summary>
        private static void DrawCallout(Vector2 anchor, BattleShout.Entry e, float age, float fade)
        {
            UITheme theme = UITheme.Instance;
            measure.text = e.Text;
            Vector2 size = calloutStyle.CalcSize(measure);
            float w = Mathf.Min(CalloutMaxWidth, size.x + 56f);
            const float h = 72f;
            Rect content = UISafeLayout.Content;
            float x = Mathf.Clamp(anchor.x - w * 0.5f, content.xMin, content.xMax - w);
            // 위쪽은 HP 상자 아래까지만 — 카메라가 움직이면 머리 위 점이 화면 위로 밀려 올라간다.
            float y = Mathf.Clamp(anchor.y - 34f - h, content.yMin + HudClearance, content.yMax - h);
            Rect box = new Rect(x, y, w, h);

            float pop = age < 0.12f ? Mathf.Lerp(0.45f, 1.12f, age / 0.12f)
                : age < 0.2f ? Mathf.Lerp(1.12f, 1f, (age - 0.12f) / 0.08f) : 1f;
            TransformAround(new Vector2(box.center.x, box.yMax), 0f, pop);

            Color accent = e.Color;
            Color bg = theme.surfaceBase;
            bg.a = 0.94f * fade;
            accent.a = fade;
            // 꼬리 먼저 — 카드가 뿌리를 덮는다. 곤충 쪽으로 좁아지는 쐐기.
            Vector2 tailFrom = new Vector2(Mathf.Clamp(anchor.x, box.xMin + 28f, box.xMax - 28f), box.yMax - 4f);
            Vector2 tailTo = new Vector2(anchor.x, Mathf.Clamp(anchor.y - 10f, box.yMax + 10f, box.yMax + 46f));
            const int Steps = 6;
            float stepH = (tailTo.y - tailFrom.y) / Steps;
            for (int k = 0; k < Steps; k++)
            {
                float t = (float)k / Steps;
                float cx = Mathf.Lerp(tailFrom.x, tailTo.x, t);
                float tw = Mathf.Lerp(22f, 5f, t);
                UISurface.Flat(new Rect(cx - tw * 0.5f, tailFrom.y + stepH * k, tw, stepH + 1f), accent);
            }
            GUI.color = new Color(1f, 1f, 1f, fade);
            UISurface.Card(box, bg, accent);
            UISurface.Flat(new Rect(box.x + UITheme.Radius.Card, box.y + 4f, box.width - UITheme.Radius.Card * 2f, 4f), accent);

            Color text = Color.Lerp(e.Color, Color.white, 0.55f);
            text.a = fade;
            calloutStyle.normal.textColor = text;
            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(box.x + 18f, box.y + 4f, box.width - 36f, box.height - 6f), e.Text, calloutStyle);
        }

        /// <summary>비명 — 머리 위에서 부르르 떨다가 떠오르며 사라진다.</summary>
        private static void DrawHurt(Vector2 anchor, BattleShout.Entry e, float age, float fade)
        {
            float jitter = age < 0.22f ? Mathf.Sin(age * 90f) * 5f * (1f - age / 0.22f) : 0f;
            float rise = Mathf.Min(1f, age / e.Duration) * 26f;
            Rect content = UISafeLayout.Content;
            float x = Mathf.Clamp(anchor.x - 160f + jitter, content.xMin, content.xMax - 320f);
            float y = Mathf.Clamp(anchor.y - 30f - 48f - rise, content.yMin + HudClearance, content.yMax - 48f);
            float pop = age < 0.08f ? Mathf.Lerp(1.5f, 1f, age / 0.08f) : 1f;
            Rect r = new Rect(x, y, 320f, 48f);
            TransformAround(r.center, 0f, pop);
            DrawOutlined(r, e.Text, hurtStyle, new Color(1f, 1f, 1f, fade), new Color(0.05f, 0.04f, 0.08f, 0.9f * fade), 3f);
        }

        /// <summary>
        /// 타격 의성어 — 크게 찍혔다가(과장 1.8배 → 1배, 0.09초) 기울어진 채 남는다. 굵은 어두운 외곽선이
        /// 섬광·임팩트 위에서도 글자를 세운다.
        /// </summary>
        private static void DrawSound(Vector2 anchor, BattleShout.Entry e, float age, float fade)
        {
            bool heavy = e.Text.EndsWith("!!");
            soundStyle.fontSize = heavy ? HeavySoundFont : SoundFont;
            float pop = age < 0.09f ? Mathf.Lerp(1.8f, 1f, age / 0.09f) : 1f + 0.04f * Mathf.Sin(age * 20f) * Mathf.Exp(-age * 6f);
            Rect r = new Rect(anchor.x - 260f, anchor.y + 6f, 520f, heavy ? 110f : 90f);
            TransformAround(r.center, e.Tilt, pop);
            Color fill = e.Color;
            fill.a = fade;
            DrawOutlined(r, e.Text, soundStyle, fill, new Color(0.06f, 0.04f, 0.09f, 0.95f * fade), heavy ? 5f : 4f);
        }

        /// <summary>
        /// 가상 캔버스 좌표의 한 점을 중심으로 돌리고 키운다. <c>GUIUtility.RotateAroundPivot</c>은 피벗을
        /// <b>스케일 전 화면 좌표</b>로 해석해서, <c>UIScale</c>이 1이 아닌 화면에선 도형이 엉뚱한 자리로
        /// 날아갔다(말풍선 꼬리가 말풍선에서 떨어진 노란 막대로 그려졌다 — 1280×720 실측).
        /// 현재 행렬 **뒤에** 곱하면 변환이 가상 좌표에서 일어난다.
        /// </summary>
        private static void TransformAround(Vector2 pivot, float angle, float scale)
        {
            GUI.matrix = GUI.matrix
                * Matrix4x4.TRS(new Vector3(pivot.x, pivot.y, 0f), Quaternion.Euler(0f, 0f, angle), new Vector3(scale, scale, 1f))
                * Matrix4x4.TRS(new Vector3(-pivot.x, -pivot.y, 0f), Quaternion.identity, Vector3.one);
        }

        private static readonly Vector2[] OutlineDirs =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.71f, 0.71f), new Vector2(-0.71f, 0.71f), new Vector2(0.71f, -0.71f), new Vector2(-0.71f, -0.71f)
        };

        private static void DrawOutlined(Rect r, string text, GUIStyle style, Color fill, Color outline, float thickness)
        {
            GUI.color = Color.white;
            style.normal.textColor = outline;
            for (int i = 0; i < OutlineDirs.Length; i++)
            {
                Vector2 d = OutlineDirs[i] * thickness;
                GUI.Label(new Rect(r.x + d.x, r.y + d.y, r.width, r.height), text, style);
            }
            style.normal.textColor = fill;
            GUI.Label(r, text, style);
        }

        private static void EnsureStyles()
        {
            if (calloutStyle != null) return;
            calloutStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = CalloutFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            hurtStyle = new GUIStyle(calloutStyle) { fontSize = HurtFont };
            soundStyle = new GUIStyle(calloutStyle) { fontSize = SoundFont, fontStyle = FontStyle.BoldAndItalic };
        }
    }
}
