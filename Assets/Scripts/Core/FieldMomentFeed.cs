using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>필드 소식의 종류 — 화면이 색과 기호를 고른다.</summary>
    public enum FieldMomentKind
    {
        /// <summary>캐릭터 레벨이 올랐다.</summary>
        LevelUp,
        /// <summary>이야기·내기에서 보상을 받았다.</summary>
        Reward,
        /// <summary>라온과의 내기 — 시작, 라온의 포획, 승패.</summary>
        Rival,
        /// <summary>근처에 색다른 개체가 나타났다.</summary>
        Discovery,
    }

    public readonly struct FieldMoment
    {
        public readonly FieldMomentKind Kind;
        public readonly string Title;
        public readonly string Body;

        public FieldMoment(FieldMomentKind kind, string title, string body)
        {
            Kind = kind;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }
    }

    /// <summary>
    /// 필드 위에 잠깐 떠서 "방금 무슨 일이 있었는지"를 알리는 소식의 대기열.
    ///
    /// 도입부가 조용했던 이유 하나가 이것이다 — 이야기 보상은 <c>StoryDirector.GrantReward</c>가 말없이 넣었고
    /// 캐릭터 레벨이 올라도 HUD 숫자만 바뀌었다. 무언가 받았거나 달라진 순간을 한 줄로 띄운다.
    ///
    /// <b>이벤트가 아니라 대기열이다.</b> 넣는 쪽(스토리·내기·조우)과 그리는 쪽(<c>FieldMomentsUI</c>)이 서로를
    /// 모르고, 화면은 구독 없이 꺼내 가기만 한다 — UI 루트가 꺼졌다 켜질 때 구독이 사라지는 계열
    /// (rules/ui-layout.md)에 걸리지 않고, 대사·영상이 화면을 덮은 동안 들어온 소식도 그 뒤에 차례로 뜬다.
    /// </summary>
    public class FieldMomentFeed : MonoBehaviour
    {
        /// <summary>쌓아 둘 수 있는 소식 수. 넘치면 가장 오래된 것을 버린다 — 한참 지난 소식이 줄줄이 뜨는 것보다 낫다.</summary>
        public const int MaxPending = 6;

        private readonly Queue<FieldMoment> pending = new Queue<FieldMoment>();
        private ItemDatabase itemDatabase;

        public int PendingCount => pending.Count;

        public void AutoWire(ItemDatabase items)
        {
            if (itemDatabase == null) itemDatabase = items;
        }

        public void Push(FieldMomentKind kind, string title, string body = null)
        {
            if (string.IsNullOrEmpty(title)) return;
            while (pending.Count >= MaxPending) pending.Dequeue();
            pending.Enqueue(new FieldMoment(kind, title, body));
        }

        public bool TryDequeue(out FieldMoment moment)
        {
            if (pending.Count == 0)
            {
                moment = default;
                return false;
            }
            moment = pending.Dequeue();
            return true;
        }

        /// <summary>아이템 표시명(없으면 ID 그대로). 소식 문구를 만드는 쪽이 DB를 따로 들지 않게 여기서 풀어 준다.</summary>
        public string ItemName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return string.Empty;
            ItemData item = itemDatabase != null ? itemDatabase.FindById(itemId) : null;
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : itemId;
        }
    }
}
