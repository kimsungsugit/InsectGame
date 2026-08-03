namespace InsectGame.Opening
{
    public enum OpeningPlaybackRequest
    {
        ColdStart,
        ManualReplay
    }

    /// <summary>
    /// 앱을 시작할 때마다 오프닝을 재생하며 수동 replay도 항상 허용한다.
    /// </summary>
    public sealed class OpeningAutoPlayPolicy
    {
        public bool TryBegin(OpeningPlaybackRequest request)
        {
            return true;
        }
    }
}
