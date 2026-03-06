/// <summary>
/// コンパイルシンボルに基づいてプラットフォーム固有の実装を生成するファクトリ。
/// Unity本体（MonoBehaviour群）は具体実装クラスに直接依存せず、
/// 本ファクトリ経由でインスタンスを取得する。
/// </summary>
public static class PlatformServiceFactory
{
    public static ICredentialStorage CreateCredentialStorage()
    {
#if UNITY_STANDALONE_WIN
        return new WindowsCredentialStorage();
#else
        return new DummyCredentialStorage();
#endif
    }

    public static IAppIntegration CreateAppIntegration()
    {
#if UNITY_STANDALONE_WIN
        return new WindowsAppIntegration();
#else
        return new DummyAppIntegration();
#endif
    }
}
