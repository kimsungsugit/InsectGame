using System;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 전투 중 곤충의 <b>외침</b> — 기술 이름 외치기, 맞았을 때 비명, 타격 의성어, 그리고 종별 울음 분류.
    /// 문구 표와 판정만 들고 있는 순수 코드다. 언제 띄울지는 아레나가, 그리기는 UI
    /// (<c>BattleShoutOverlay</c>)가 맡는다 — 효과 문구(<c>EffectTextEntry</c>)와 같은 분업이다.
    ///
    /// 예전엔 간부·수문장 NPC의 대사 말풍선 말고는 전투 내내 아무도 소리를 내지 않았다.
    /// 곤충이 기술을 쓰든 치명타를 맞든 화면 위쪽 문구 한 줄과 하단 설명 줄이 전부였다.
    /// </summary>
    public static class BattleShout
    {
        public enum Kind
        {
            /// <summary>기술 이름을 외친다 — 준비 동작과 함께, 시전자 머리 위.</summary>
            Callout,
            /// <summary>맞은 쪽의 비명 — 타격 직후, 대상 머리 위.</summary>
            Hurt,
            /// <summary>타격 의성어("콰광!!") — 타격 순간, 대상 몸통.</summary>
            Sound
        }

        /// <summary>종별 울음 계열 — 비명 문구와 울음 효과음이 같은 분류를 쓴다.</summary>
        public enum Cry { Beetle, Mantis, Flyer, Buzzer, Chirper, Crawler, Boss }

        public sealed class Entry
        {
            public string Text;
            public Kind Kind;
            /// <summary>
            /// 띄운 순간의 월드 위치. 모델을 따라가지 않는다 — 돌진하는 시전자를 따라 말풍선이 화면을
            /// 가로지르면 못 읽는다. 카메라가 움직여도 이 점을 매 프레임 다시 투영하므로 그 자리에 붙어 있다.
            /// </summary>
            public Vector3 WorldPoint;
            public Color Color;
            public float StartTime;
            public float Duration;
            /// <summary>의성어 기울기(도). 매번 같은 각이면 도장 찍은 듯 보인다.</summary>
            public float Tilt;
        }

        /// <summary>
        /// 곤충 ID → 울음 계열. ID는 <c>계열_수식어</c> 꼴이라(<c>mantis_ember</c>, <c>fly_hover</c>)
        /// <b>토막 단위로</b> 본다 — 부분 문자열로 보면 <c>mantis</c>에 <c>ant</c>가, <c>dragonfly</c>에
        /// <c>fly</c>가 걸린다. 구체적인 것(날개 큰 비행형)을 날벌레보다 먼저 본다.
        /// </summary>
        public static Cry CryFor(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId)) return Cry.Beetle;
            string[] tokens = speciesId.ToLowerInvariant().Split('_');
            if (Has(tokens, "mantis")) return Cry.Mantis;
            if (Has(tokens, "dragonfly") || Has(tokens, "damselfly") || Has(tokens, "butterfly")
                || Has(tokens, "moth") || Has(tokens, "firefly")) return Cry.Flyer;
            if (Has(tokens, "bee") || Has(tokens, "wasp") || Has(tokens, "hornet")
                || Has(tokens, "mosquito") || Has(tokens, "fly")) return Cry.Buzzer;
            if (Has(tokens, "cricket") || Has(tokens, "grasshopper") || Has(tokens, "katydid")
                || Has(tokens, "cicada") || Has(tokens, "locust")) return Cry.Chirper;
            if (Has(tokens, "spider") || Has(tokens, "centipede") || Has(tokens, "ant")
                || Has(tokens, "antlion") || Has(tokens, "caterpillar") || Has(tokens, "aphid")
                || Has(tokens, "stick") || Has(tokens, "strider") || Has(tokens, "earwig")) return Cry.Crawler;
            return Cry.Beetle;
        }

        /// <summary>기술 이름 외치기. 이름에 이미 느낌표가 있으면 겹치지 않게 정리한다.</summary>
        public static string Callout(string skillName)
        {
            string name = string.IsNullOrEmpty(skillName) ? string.Empty : skillName.Trim().TrimEnd('!', '！');
            return name.Length == 0 ? "간다!!" : name + "!!";
        }

        private static readonly string[][] HurtLines =
        {
            // Beetle
            new[] { "쿠웃!", "크읏!" },
            // Mantis
            new[] { "끼익!", "키잇!" },
            // Flyer
            new[] { "파닥!", "히잇!" },
            // Buzzer
            new[] { "비잉?!", "즈즛!" },
            // Chirper
            new[] { "찌익!", "찍!" },
            // Crawler
            new[] { "끽!", "키익!" },
            // Boss
            new[] { "그르륵!", "크르르!" },
        };

        private static readonly string[][] HeavyHurtLines =
        {
            new[] { "크아악!!", "쿠오오…!!" },
            new[] { "끼이익!!", "캬아악!!" },
            new[] { "히이익!!", "파다닥!!" },
            new[] { "비이잉!!", "즈즈즛!!" },
            new[] { "찌이익!!", "끼리릭!!" },
            new[] { "끼이익!!", "키에엑!!" },
            new[] { "그오오오!!", "크아아아!!" },
        };

        /// <summary>
        /// 큰 비명·센 의성어로 넘어가는 세기. 평타의 세기 천장(<c>BattleArenaController.HitCue.NormalCeiling</c>)은
        /// 이 아래다 — 그래서 큰 문구는 치명타·마무리(그리고 합체공격 일격처럼 호출부가 1을 넘기는 자리)만 받는다.
        /// 예전엔 평타도 최대 HP의 25~35%를 깎아 세기 0.6~0.9를 받았고, 매 타격이 "크아악!!"·"콰광!!"이었다.
        /// </summary>
        public const float HeavyWeight = 0.6f;

        /// <summary>
        /// 맞았을 때 비명. 세기가 크면(치명타·마무리) 길게 끈다. <paramref name="seed"/>로 고르므로
        /// 같은 장면은 같은 문구 — 재현 가능한 캡처를 위해서다.
        /// </summary>
        public static string Hurt(Cry cry, float impactWeight, int seed)
        {
            string[][] table = impactWeight >= HeavyWeight ? HeavyHurtLines : HurtLines;
            string[] row = table[Mathf.Clamp((int)cry, 0, table.Length - 1)];
            return row[Math.Abs(seed) % row.Length];
        }

        /// <summary>
        /// 타격 의성어. 속성마다 소리가 다르고, 세기가 크면 센 쪽을 쓴다.
        /// 표는 <see cref="InsectElement"/> 순서와 1:1이다(<c>BattleShoutTests</c>가 고정).
        /// </summary>
        public static string Sound(InsectElement element, float impactWeight)
        {
            bool heavy = impactWeight >= HeavyWeight;
            switch (element)
            {
                case InsectElement.Leaf: return heavy ? "파사사삭!!" : "사각!";
                case InsectElement.Water: return heavy ? "콰쏴아!!" : "촤악!";
                case InsectElement.Wind: return heavy ? "휘이잉!!" : "휙!";
                case InsectElement.Electric: return heavy ? "콰지직!!" : "파직!";
                case InsectElement.Earth: return heavy ? "쿠구궁!!" : "쿵!";
                case InsectElement.Poison: return heavy ? "부글부글!!" : "치익!";
                case InsectElement.Light: return heavy ? "파아앗!!" : "번쩍!";
                case InsectElement.Dark: return heavy ? "콰드득!!" : "스윽!";
                case InsectElement.Metal: return heavy ? "콰앙!!" : "깡!";
                case InsectElement.Bug:
                case InsectElement.None:
                default:
                    return heavy ? "콰광!!" : "퍽!";
            }
        }

        private static bool Has(string[] tokens, string word)
        {
            for (int i = 0; i < tokens.Length; i++)
                if (tokens[i] == word) return true;
            return false;
        }
    }
}
