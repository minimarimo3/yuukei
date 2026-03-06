// ==========================================================================
// DaihonRunner.cs
// 台本テキストのパース → インタープリタ実行を 1 メソッドにまとめるファサード。
// ==========================================================================

using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Cysharp.Threading.Tasks;
using Daihon;
using UnityEngine;

namespace Daihon.Unity
{
    /// <summary>
    /// DaihonScript を Unity 上で簡単に実行するためのファサードクラス。
    /// パース → 構文木の生成 → インタープリタ実行 を一括で行います。
    /// </summary>
    public static class DaihonRunner
    {
        /// <summary>
        /// 台本テキストをパースして実行します。
        /// </summary>
        /// <param name="scriptText">台本テキスト（.daihon ファイルの内容）</param>
        /// <param name="actionHandler">セリフ表示・関数呼び出しを処理する UniTask ベースのハンドラー</param>
        /// <param name="variableStore">変数ストア</param>
        /// <returns>実行完了を待機する UniTask</returns>
        public static async UniTask RunAsync(
            string scriptText,
            IUniTaskActionHandler actionHandler,
            IVariableStore variableStore)
        {
            if (string.IsNullOrEmpty(scriptText))
            {
                Debug.LogWarning("[DaihonRunner] 台本テキストが空です。");
                return;
            }

            // 末尾に改行がなければ付加（文法が NEWLINE を文の区切りとして要求するため）
            if (!scriptText.EndsWith("\n"))
                scriptText += "\n";

            // レキサー・パーサーの初期化
            var inputStream = new AntlrInputStream(scriptText);
            var lexer = new DaihonLexer(inputStream);
            var errors = new List<string>();

            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new UnityErrorListener(errors));

            var tokenStream = new CommonTokenStream(lexer);
            var parser = new DaihonParser(tokenStream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new UnityErrorListener(errors));

            // パース
            var tree = parser.file();

            // パースエラーチェック
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Debug.LogError($"[DaihonRunner] パースエラー: {error}");
                return;
            }

            // UniTask → Task アダプターを挟んでインタープリタ実行
            var adapter = new UniTaskActionAdapter(actionHandler);
            var visitor = new DaihonScriptVisitor(adapter, variableStore);

            try
            {
                await visitor.VisitFile(tree);
            }
            catch (DaihonRuntimeException ex)
            {
                Debug.LogError($"[DaihonRunner] 実行時エラー: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DaihonRunner] 予期しないエラー: {ex}");
            }
        }

        /// <summary>
        /// TextAsset から台本を読み込んで実行します。
        /// </summary>
        public static UniTask RunAsync(
            TextAsset scriptAsset,
            IUniTaskActionHandler actionHandler,
            IVariableStore variableStore)
        {
            if (scriptAsset == null)
            {
                Debug.LogError("[DaihonRunner] TextAsset が null です。");
                return UniTask.CompletedTask;
            }

            return RunAsync(scriptAsset.text, actionHandler, variableStore);
        }

        // ================================================================
        // ANTLR エラーリスナー（Unity Console への出力用）
        // ================================================================

        private sealed class UnityErrorListener : IAntlrErrorListener<IToken>, IAntlrErrorListener<int>
        {
            private readonly List<string> _errors;

            public UnityErrorListener(List<string> errors) => _errors = errors;

            public void SyntaxError(
                System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
                int line, int charPositionInLine, string msg, RecognitionException e)
            {
                _errors.Add($"line {line}:{charPositionInLine} {msg}");
            }

            public void SyntaxError(
                System.IO.TextWriter output, IRecognizer recognizer, int offendingSymbol,
                int line, int charPositionInLine, string msg, RecognitionException e)
            {
                _errors.Add($"line {line}:{charPositionInLine} {msg}");
            }
        }
    }
}
