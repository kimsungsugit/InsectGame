#if UNITY_EDITOR
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="FirestoreDocParser"/> — 클라우드 복원이 읽는 Firestore REST 문서 파서.
    /// 예전 구현은 공백 없는 마커만 찾아서, 응답이 줄바꿈·들여쓰기돼 오면 전 필드가 기본값이 됐다
    /// (레벨 0·캔디 0으로 복원). 두 형식을 함께 고정한다.
    /// </summary>
    [TestFixture]
    public class FirestoreDocParserTests
    {
        private const string Compact =
            "{\"fields\":{\"displayName\":{\"stringValue\":\"탐험가\"},\"playerLevel\":{\"integerValue\":\"27\"}," +
            "\"islandData\":{\"stringValue\":\"{\\\"sizeLevel\\\":2,\\\"owned\\\":[]}\"}}}";

        private const string Pretty =
            "{\n  \"name\": \"projects/p/databases/(default)/documents/users/u1\",\n  \"fields\": {\n" +
            "    \"displayName\": {\n      \"stringValue\": \"탐험가\"\n    },\n" +
            "    \"playerLevel\": {\n      \"integerValue\": \"27\"\n    },\n" +
            "    \"islandData\": {\n      \"stringValue\": \"{\\\"sizeLevel\\\":2,\\\"owned\\\":[]}\"\n    }\n  },\n" +
            "  \"createTime\": \"2026-01-01T00:00:00Z\"\n}";

        [TestCase(Compact)]
        [TestCase(Pretty)]
        public void TryGetString_ReadsBothFormats(string json)
        {
            Assert.IsTrue(FirestoreDocParser.TryGetString(json, "displayName", out string name));
            Assert.AreEqual("탐험가", name);
        }

        [TestCase(Compact)]
        [TestCase(Pretty)]
        public void TryGetInt_ReadsBothFormats(string json)
        {
            Assert.IsTrue(FirestoreDocParser.TryGetInt(json, "playerLevel", out int level));
            Assert.AreEqual(27, level);
        }

        [TestCase(Compact)]
        [TestCase(Pretty)]
        public void TryGetString_NestedJsonBlob_IsUnescaped(string json)
        {
            Assert.IsTrue(FirestoreDocParser.TryGetString(json, "islandData", out string blob));
            Assert.AreEqual("{\"sizeLevel\":2,\"owned\":[]}", blob);
        }

        [Test]
        public void MissingField_ReturnsFalseAndEmpty()
        {
            Assert.IsFalse(FirestoreDocParser.TryGetString(Compact, "nope", out string s));
            Assert.AreEqual(string.Empty, s);
            Assert.IsFalse(FirestoreDocParser.TryGetInt(Compact, "nope", out int i));
            Assert.AreEqual(0, i);
        }

        [Test]
        public void WrongType_IsNotRead()
        {
            // 정수 필드를 문자열로, 문자열 필드를 정수로 읽지 않는다.
            Assert.IsFalse(FirestoreDocParser.TryGetString(Compact, "playerLevel", out _));
            Assert.IsFalse(FirestoreDocParser.TryGetInt(Compact, "displayName", out _));
        }

        [Test]
        public void FieldNameInsideABlob_IsNotMistakenForTheField()
        {
            // 곤충 블롭 안에 "gems"라는 키가 있어도(이스케이프돼 있다) 최상위 gems로 읽으면 안 된다.
            string json = "{\"fields\":{\"ownedInsects\":{\"stringValue\":\"{\\\"gems\\\":{\\\"integerValue\\\":\\\"999\\\"}}\"}," +
                          "\"gems\":{\"integerValue\":\"12\"}}}";
            Assert.IsTrue(FirestoreDocParser.TryGetInt(json, "gems", out int gems));
            Assert.AreEqual(12, gems);
        }

        [Test]
        public void Escapes_AreDecodedCharacterByCharacter()
        {
            // 원문이 `\` 뒤에 `n`인 경우(JSON으로는 `\\n`) — 순차 Replace는 이걸 줄바꿈으로 잘못 되돌렸다.
            string json = "{\"fields\":{\"a\":{\"stringValue\":\"x\\\\ny\"},\"b\":{\"stringValue\":\"l1\\nl2\\t\\u00e9\\/\"}}}";
            Assert.IsTrue(FirestoreDocParser.TryGetString(json, "a", out string a));
            Assert.AreEqual("x\\ny", a);
            Assert.IsTrue(FirestoreDocParser.TryGetString(json, "b", out string b));
            Assert.AreEqual("l1\nl2\té/", b);
        }

        [Test]
        public void EmptyString_IsReadAsEmpty()
        {
            string json = "{\"fields\":{\"activeQuest\":{\"stringValue\":\"\"}}}";
            Assert.IsTrue(FirestoreDocParser.TryGetString(json, "activeQuest", out string s));
            Assert.AreEqual(string.Empty, s);
        }

        [Test]
        public void NegativeAndHugeIntegers_AreHandled()
        {
            string json = "{\"fields\":{\"a\":{\"integerValue\":\"-1\"},\"b\":{\"integerValue\":\"99999999999\"},\"c\":{\"integerValue\":\"x\"}}}";
            Assert.IsTrue(FirestoreDocParser.TryGetInt(json, "a", out int a));
            Assert.AreEqual(-1, a);
            Assert.IsTrue(FirestoreDocParser.TryGetInt(json, "b", out int b));
            Assert.AreEqual(int.MaxValue, b);
            Assert.IsFalse(FirestoreDocParser.TryGetInt(json, "c", out _));
        }

        [Test]
        public void NullOrEmptyInput_DoesNotThrow()
        {
            Assert.IsFalse(FirestoreDocParser.TryGetString(null, "a", out _));
            Assert.IsFalse(FirestoreDocParser.TryGetString("", "a", out _));
            Assert.IsFalse(FirestoreDocParser.TryGetString("{\"fields\":{\"a\":{\"stringValue\":\"unterminated", "a", out _));
        }
    }
}
#endif
