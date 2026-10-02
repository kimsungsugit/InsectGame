using System;
using System.Globalization;
using System.Text;

namespace InsectGame.Core
{
    /// <summary>
    /// Firestore REST 문서(<c>{"fields":{"키":{"stringValue":"…"}}}</c>)에서 최상위 필드 값을 꺼내는 간이 파서.
    ///
    /// <b>공백에 관대하다.</b> 예전 구현은 <c>"키":{"stringValue":"</c>라는 <b>공백 없는</b> 마커를 통째로 찾았는데,
    /// REST 응답이 줄바꿈·들여쓰기된 형식(<c>"키": {</c> + 줄바꿈)으로 오면 한 필드도 못 찾고 전부 기본값이 된다.
    /// 여기서는 키를 찾은 뒤 구조 문자 사이의 공백을 건너뛴다 — 두 형식 모두 읽는다.
    ///
    /// 이스케이프도 한 글자씩 푼다. 예전의 순차 <c>Replace</c>는 원문의 <c>\</c> 뒤 <c>n</c>을 줄바꿈으로 잘못 되돌렸다.
    /// </summary>
    public static class FirestoreDocParser
    {
        /// <summary>문자열 필드. 없으면 false이고 <paramref name="value"/>는 빈 문자열.</summary>
        public static bool TryGetString(string json, string fieldName, out string value)
        {
            value = string.Empty;
            int at = FindTypedValue(json, fieldName, "stringValue");
            if (at < 0) return false;
            return TryReadJsonString(json, at, out value);
        }

        /// <summary>정수 필드(<c>integerValue</c>는 문자열로 온다). 없거나 숫자가 아니면 false.</summary>
        public static bool TryGetInt(string json, string fieldName, out int value)
        {
            value = 0;
            int at = FindTypedValue(json, fieldName, "integerValue");
            if (at < 0) return false;
            if (!TryReadJsonString(json, at, out string text)) return false;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)) return false;
            value = parsed > int.MaxValue ? int.MaxValue : parsed < int.MinValue ? int.MinValue : (int)parsed;
            return true;
        }

        /// <summary>
        /// <c>"필드": { "타입": "</c>까지 맞는 자리를 찾아, 값 문자열을 여는 따옴표의 인덱스를 준다. 없으면 -1.
        /// 문자열 값 <b>안에</b> 든 같은 이름(이스케이프된 <c>\"필드\"</c>)은 앞 글자가 역슬래시라 걸러진다.
        /// </summary>
        private static int FindTypedValue(string json, string fieldName, string typeName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return -1;
            string key = "\"" + fieldName + "\"";
            int from = 0;
            while (from < json.Length)
            {
                int idx = json.IndexOf(key, from, StringComparison.Ordinal);
                if (idx < 0) return -1;
                from = idx + key.Length;
                if (idx > 0 && json[idx - 1] == '\\') continue;

                int p = SkipWhitespace(json, from);
                if (!Expect(json, ref p, ':')) continue;
                p = SkipWhitespace(json, p);
                if (!Expect(json, ref p, '{')) continue;
                p = SkipWhitespace(json, p);

                string typeKey = "\"" + typeName + "\"";
                if (string.CompareOrdinal(json, p, typeKey, 0, typeKey.Length) != 0) continue;
                p += typeKey.Length;
                p = SkipWhitespace(json, p);
                if (!Expect(json, ref p, ':')) continue;
                p = SkipWhitespace(json, p);
                if (p < json.Length && json[p] == '"') return p;
            }
            return -1;
        }

        private static int SkipWhitespace(string s, int p)
        {
            while (p < s.Length && (s[p] == ' ' || s[p] == '\n' || s[p] == '\r' || s[p] == '\t')) p++;
            return p;
        }

        private static bool Expect(string s, ref int p, char c)
        {
            if (p >= s.Length || s[p] != c) return false;
            p++;
            return true;
        }

        /// <summary><paramref name="openQuote"/>의 따옴표에서 시작하는 JSON 문자열을 읽어 이스케이프를 푼다.</summary>
        private static bool TryReadJsonString(string json, int openQuote, out string value)
        {
            value = string.Empty;
            var sb = new StringBuilder();
            int i = openQuote + 1;
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (i >= json.Length) return false;
                char e = json[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > json.Length
                            || !ushort.TryParse(json.Substring(i, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out ushort code))
                            return false;
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: sb.Append(e); break;   // \" \\ \/
                }
            }
            return false;   // 닫는 따옴표 없이 끝났다
        }
    }
}
