/// <summary>
/// OSのセキュアストアへのアクセスを抽象化するインターフェース。
/// 実装クラスは PlatformServiceFactory で生成する。
/// </summary>
public interface ICredentialStorage
{
    /// <summary>指定キーで文字列を保存する</summary>
    void Save(string key, string value);

    /// <summary>指定キーの文字列を取得する。存在しない場合は null を返す</summary>
    string Load(string key);

    /// <summary>指定キーのエントリを削除する。存在しない場合は何もしない</summary>
    void Delete(string key);
}
