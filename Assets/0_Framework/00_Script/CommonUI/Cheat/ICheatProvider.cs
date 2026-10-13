namespace O2un.UI
{
    /// <summary>
    /// 치트 창의 한 단락. 프레임워크·게임이 DI 에 <c>As&lt;ICheatProvider&gt;()</c> 로 등록하면 치트 창이 등록 순서대로 모아 그린다.
    /// 규칙은 구현체가 아는 서비스에 두고, 여기서는 버튼과 상태 줄만 놓는다.
    /// </summary>
    public interface ICheatProvider
    {
        string Title { get; }
        void Build(CheatPage page);
    }
}
