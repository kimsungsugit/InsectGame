namespace InsectGame.Opening
{
    /// <summary>게임 플레이 중 오프닝을 안전하게 다시 재생하는 서비스.</summary>
    public interface IOpeningReplayService
    {
        bool CanReplay { get; }
        bool TryReplay();
    }
}
