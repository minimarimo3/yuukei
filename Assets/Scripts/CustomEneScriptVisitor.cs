using System;
using Antlr4.Runtime.Misc;
// ※名前空間は生成時に指定したもの（EneScriptRunner.cs に合わせます）
using EneScript; 

// 自動生成されたベースクラスを継承します
public class CustomEneScriptVisitor : EneScriptBaseVisitor<object>
{
    // UIへテキストを送るためのコールバック
    public Action<string> OnDialogueTextRead;

    // ANTLRで定義したセリフの構文規則（例: dialogue）に対応するVisitメソッドをオーバーライドします
    // ※「VisitDialogue」の部分は実際の.g4ファイルのルール名に合わせて変更してください
    public override object VisitDialogue([NotNull] EneScriptParser.DialogueContext context)
    {
        // セリフのテキストを取得します
        // （※必要に応じて、前後のかぎ括弧「」などをTrim等で削る処理をここに入れます）
        string text = context.GetText();

        // 抽出したテキストをコールバック経由で外部（Runner）へ送ります
        OnDialogueTextRead?.Invoke(text);

        // 子ノードの巡回を継続します
        return base.VisitDialogue(context);
    }
}