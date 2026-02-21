using UnityEngine;
using Antlr4.Runtime;
using EneScript; // 生成時に指定したパッケージ名
using System.IO;
using System;

public class EneScriptRunner : MonoBehaviour
{
    // Unityの画面（インスペクター）からテキストを入力できるようにする
    [TextArea(10, 20)]
    public string scriptContent = @"＃＃　PC整理イベント
※（ファイル（数　パス：「マイピクチャ」）＞＝１０）
　　「整理しましょうよ」
　　→終わり
";

    // VRMコントローラーへの参照を追加
    public SimpleVRMController vrmController;

    // VRMファイル名
    public string vrmFileName = @"C:\Users\minimarimo3\Downloads\AvatarSample_A.vrm";

    // async void に変更して非同期処理を待てるようにする
    async void Start()
    {
        // 1. まずVRMをロード
        // string path = Path.Combine(Application.streamingAssetsPath, vrmFileName);
        string path = @"C:\Users\minimarimo3\Downloads\AvatarSample_A.vrm";
        
        if (vrmController != null)
        {
            await vrmController.LoadVRM(path);
        }

        // 2. ロードが終わったら実行
        Debug.Log("--- EneScript 実行開始 ---");
        RunScript(scriptContent);
    }

    void RunScript(string text)
    {
        // 1. 文字列を読み込む（InputStream）
        var stream = new AntlrInputStream(text);

        // 2. 字句解析（Lexer）：文字を単語に分解
        var lexer = new EneScriptLexer(stream);
        var tokens = new CommonTokenStream(lexer);

        // 3. 構文解析（Parser）：文法を理解する
        var parser = new EneScriptParser(tokens);
        
        // 文法ファイルの開始ルール「file」を呼び出す
        var tree = parser.file();

        // 4. 実行（Visitor）：ツリーを巡回して処理を行う
        // コントローラーをExecutorに渡す！
        var visitor = new EneExecutor(vrmController);
        visitor.Visit(tree);
    }
}

// 実際にどう動くかを定義するクラス（Visitorを継承）
// ここに「マウスを無効化する」などの処理を書いていきます
// EneScriptRunner.cs の下の方にあるクラスです

public class EneExecutor : EneScriptBaseVisitor<object>
{
    private SimpleVRMController _vrm;
    private WindowsActions _windows;

    public EneExecutor(SimpleVRMController controller)
    {
        _vrm = controller;
        _windows = new WindowsActions();
    }

    // --- ① 基本的な動作 ---

    // 会話
    public override object VisitDialogue(EneScriptParser.DialogueContext context)
    {
        string rawText = context.STRING().GetText();
        string content = rawText.Substring(1, rawText.Length - 2); // 「」除去

        Debug.Log($"<color=cyan>【エネ】{content}</color>");
        if (_vrm != null) _vrm.Speak(content);
        return null;
    }

    // コマンド実行
    public override object VisitCommand(EneScriptParser.CommandContext context)
    {
        string commandName = context.ID().GetText();
        var args = new System.Collections.Generic.Dictionary<string, string>();
        
        foreach (var argCtx in context.arg())
        {
            string key = argCtx.ID() != null ? argCtx.ID().GetText() : "default";
            string val = argCtx.value().GetText();
            if (val.StartsWith("「")) val = val.Substring(1, val.Length - 2);
            args[key] = val;
        }

        switch (commandName)
        {
            case "マウス入力を無効化": _windows.SetInputState(false); break;
            case "マウス入力を有効化": _windows.SetInputState(true); break;
            case "アプリケーションを開く":
                if (args.ContainsKey("パス")) _windows.OpenApplication(args["パス"]);
                break;
            default: Debug.LogWarning($"不明なコマンド: {commandName}"); break;
        }
        return null;
    }

    // --- ② 脳の実装（条件分岐） ---

    public override object VisitLogic(EneScriptParser.LogicContext context)
    {
        // 1. 条件式（expression）を計算する
        // Visitの結果は object なので、bool に変換して判定
        bool condition = ConvertToBool(Visit(context.expression()));

        Debug.Log($"条件判定: {context.expression().GetText()} -> {condition}");

        if (condition)
        {
            // 条件OKなら中身を実行
            return Visit(context.statement());
        }
        else
        {
            // 条件NGなら、もし「→遷移先」があればそっちへ（今回は未実装）
            // 遷移先の実装はもう少し複雑になるので、まずは「実行しない」だけ実装
            return null; 
        }
    }

    // --- ③ 計算機能（比較・論理演算） ---

    // 比較（A ＞＝ B など）
    public override object VisitRelationalExpr(EneScriptParser.RelationalExprContext context)
    {
        // 左と右の値を計算
        var left = ConvertToDouble(Visit(context.expression(0)));
        var right = ConvertToDouble(Visit(context.expression(1)));
        string op = context.relationOp().GetText();

        switch (op)
        {
            case "＝": return left == right;
            case "！＝": return left != right;
            case "＜": return left < right;
            case "＜＝": return left <= right;
            case "＞": return left > right;
            case "＞＝": return left >= right;
        }
        return false;
    }

    // 論理演算（かつ / または）
    public override object VisitAndExpr(EneScriptParser.AndExprContext context)
    {
        bool left = ConvertToBool(Visit(context.expression(0)));
        bool right = ConvertToBool(Visit(context.expression(1)));
        return left && right;
    }

    public override object VisitOrExpr(EneScriptParser.OrExprContext context)
    {
        bool left = ConvertToBool(Visit(context.expression(0)));
        bool right = ConvertToBool(Visit(context.expression(1)));
        return left || right;
    }

    // カッコ（）
    public override object VisitParenExpr(EneScriptParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    // 値（数字や文字をそのまま返す）
    public override object VisitValueExpr(EneScriptParser.ValueExprContext context)
    {
        return Visit(context.value());
    }
    
    public override object VisitValue(EneScriptParser.ValueContext context)
    {
        if (context.NUMBER() != null) return double.Parse(context.NUMBER().GetText());
        if (context.STRING() != null)
        {
            string s = context.STRING().GetText();
            return s.Substring(1, s.Length - 2);
        }
        return context.GetText();
    }

    // --- ④ 感覚の実装（関数） ---

    public override object VisitFuncCall(EneScriptParser.FuncCallContext context)
    {
        string funcName = context.ID().GetText();
        
        // 引数の解析（簡易版）
        string argValue = "";
        if (context.funcArg().Length > 0)
        {
            // 最初の引数の値だけ取る（簡易実装）
            var valCtx = context.funcArg(0).value();
            object val = Visit(valCtx);
            argValue = val.ToString();
        }

        Debug.Log($"関数呼び出し: {funcName} (引数: {argValue})");

        switch (funcName)
        {
            case "現在時刻":
                // 1800 (18:00) のような数値を返す
                return double.Parse(DateTime.Now.ToString("HHmm"));

            case "ファイル":
                // 引数がフォルダパスなら、その中のファイル数を返す
                if (Directory.Exists(argValue))
                {
                    return (double)Directory.GetFiles(argValue).Length;
                }
                // テスト用に、パスが存在しなくても適当な数を返しておく
                return 0.0;
        }
        return 0.0;
    }

    // --- ヘルパー関数（型変換） ---

    private double ConvertToDouble(object obj)
    {
        try { return Convert.ToDouble(obj); }
        catch { return 0; }
    }

    private bool ConvertToBool(object obj)
    {
        try { return Convert.ToBoolean(obj); }
        catch { return false; }
    }
}