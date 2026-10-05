namespace InsectGame.UI
{
    /// <summary>
    /// 한국어 조사 고르기 — <b>순수 계산</b>. 이름 끝 글자의 받침으로 이/가·을/를·은/는·와/과를 고른다.
    ///
    /// 화면 문구의 이름은 데이터가 정한다(상대 이름·곤충 이름). 조사를 한쪽으로 고정하면 절반은 틀리고, "이(가)"로 두면
    /// 아이가 읽다가 걸린다 — 전투 화면의 「집게가 지네를 내보냈다!」가 처음 쓰는 자리다.
    ///
    /// 끝의 닫는 괄호·따옴표·문장부호·공백은 건너뛰고 그 앞 글자를 본다("사마귀(이로치)" → 치). 숫자는 읽는 소리로 본다
    /// (1 일·3 삼·6 육·7 칠·8 팔·0 영은 받침, 2 이·4 사·5 오·9 구는 없음). 한글도 숫자도 아니면(영문 등) 모른다 —
    /// 그때는 "이(가)"처럼 둘 다 적는다(지어내지 않는다).
    /// </summary>
    public static class KoreanJosa
    {
        private const int HangulFirst = 0xAC00;
        private const int HangulLast = 0xD7A3;
        private const int FinalCount = 28;
        /// <summary>종성 ㄹ의 번호(가 + 8).</summary>
        private const int FinalRieul = 8;

        /// <summary>끝 글자에 받침이 있으면 true, 없으면 false, 모르면 null.</summary>
        public static bool? HasFinalConsonant(string word)
        {
            int final = FinalIndex(word);
            if (final == Unknown) return null;
            return final > 0;
        }

        /// <summary>이/가 — "집게가", "관장 하월이".</summary>
        public static string IGa(string word) => Pick(word, "이", "가", "이(가)");

        /// <summary>을/를 — "지네를", "사슴벌레를", "장수풍뎅이를".</summary>
        public static string EulReul(string word) => Pick(word, "을", "를", "을(를)");

        /// <summary>은/는.</summary>
        public static string EunNeun(string word) => Pick(word, "은", "는", "은(는)");

        /// <summary>와/과 — 받침이 있으면 "과".</summary>
        public static string WaGwa(string word) => Pick(word, "과", "와", "와(과)");

        /// <summary>단어 + 조사. <paramref name="josa"/>는 위 함수 중 하나.</summary>
        public static string With(string word, System.Func<string, string> josa)
        {
            string w = word ?? string.Empty;
            return josa != null ? w + josa(w) : w;
        }

        private const int Unknown = -1;

        private static string Pick(string word, string withFinal, string withoutFinal, string unknown)
        {
            int final = FinalIndex(word);
            if (final == Unknown) return unknown;
            return final > 0 ? withFinal : withoutFinal;
        }

        /// <summary>끝 글자의 종성 번호(0 = 받침 없음, 1~27), 모르면 <see cref="Unknown"/>.</summary>
        private static int FinalIndex(string word)
        {
            if (string.IsNullOrEmpty(word)) return Unknown;
            for (int i = word.Length - 1; i >= 0; i--)
            {
                char c = word[i];
                if (IsSkippable(c)) continue;
                if (c >= HangulFirst && c <= HangulLast) return (c - HangulFirst) % FinalCount;
                if (c >= '0' && c <= '9') return DigitFinal(c);
                return Unknown;
            }
            return Unknown;
        }

        // 숫자를 한자어 소리로 읽을 때의 받침(0 영 ㅇ, 1 일 ㄹ, 3 삼 ㅁ, 6 육 ㄱ, 7 칠 ㄹ, 8 팔 ㄹ).
        private static int DigitFinal(char digit)
        {
            switch (digit)
            {
                case '0': return 21;            // ㅇ
                case '1': return FinalRieul;    // ㄹ
                case '3': return 16;            // ㅁ
                case '6': return 1;             // ㄱ
                case '7': return FinalRieul;
                case '8': return FinalRieul;
                default: return 0;              // 2 이 · 4 사 · 5 오 · 9 구
            }
        }

        private static bool IsSkippable(char c)
        {
            switch (c)
            {
                case ' ': case '\t': case '\n':
                case ')': case ']': case '}': case '>':
                case '」': case '』': case '》': case '〉':
                case '"': case '\'': case '”': case '’':
                case '!': case '?': case '.': case ',': case '~': case '…':
                    return true;
                default:
                    return false;
            }
        }
    }
}
