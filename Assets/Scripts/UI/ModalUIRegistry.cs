using System.Collections.Generic;

namespace InsectGame.UI
{
    public interface IModalUI
    {
        bool IsOpen { get; }
        void CloseModal();
    }

    /// <summary>
    /// 활성 모달 UI 스택 관리. PlayerMovement / ESC 핸들러 / 입력 차단 판단에 사용.
    /// - Register: 최근 열린 UI를 스택 최상위로
    /// - Unregister: 닫힐 때 제거
    /// - HandleEscape: 최상위 모달만 닫음 (중복 닫힘 방지)
    /// </summary>
    public static class ModalUIRegistry
    {
        private static readonly List<IModalUI> stack = new List<IModalUI>();

        public static void Register(IModalUI ui)
        {
            if (ui == null) return;
            stack.Remove(ui); // 중복 제거 후 최상위에 추가
            stack.Add(ui);
        }

        public static void Unregister(IModalUI ui)
        {
            if (ui == null) return;
            stack.Remove(ui);
        }

        public static bool IsAnyOpen()
        {
            // 죽은 참조(파괴된 MonoBehaviour) 정리
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i] == null || !stack[i].IsOpen) stack.RemoveAt(i);
            }
            return stack.Count > 0;
        }

        public static IModalUI TopModal
        {
            get
            {
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    if (stack[i] != null && stack[i].IsOpen) return stack[i];
                    stack.RemoveAt(i);
                }
                return null;
            }
        }

        /// <summary>ESC 처리 — 최상위 모달 한 개만 닫음.</summary>
        public static bool HandleEscape()
        {
            IModalUI top = TopModal;
            if (top == null) return false;
            top.CloseModal();
            return true;
        }
    }
}
