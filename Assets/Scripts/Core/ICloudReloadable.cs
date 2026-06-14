namespace InsectGame.Core
{
    /// <summary>
    /// 클라우드 로드(ApplySaveData)로 디스크/PlayerPrefs가 갱신된 뒤, 인메모리 캐시를
    /// 다시 읽어들여야 하는 시스템이 구현. CloudSaveManager가 적용 직후 일괄 호출한다.
    ///
    /// 배경: 게임 시스템은 부트스트랩(Awake)에서 한 번 로드 → 캐시한다. 로그인 후의
    /// 클라우드 로드는 그 이후라, 파일/PlayerPrefs만 덮어쓰면 인메모리는 옛 값(특히 다른
    /// 기기 첫 로그인 시 곤충/팀/도감/지역/의상이 앱 재시작 전까지 비어 보임). 이 훅으로 해소.
    /// </summary>
    public interface ICloudReloadable
    {
        /// <summary>디스크/PlayerPrefs에서 상태를 다시 읽어 인메모리 캐시를 갱신하고
        /// 필요 시 변경 이벤트를 발화(IMGUI는 자동 갱신되나 캐시 의존 UI를 위해).</summary>
        void ReloadFromDisk();
    }
}
