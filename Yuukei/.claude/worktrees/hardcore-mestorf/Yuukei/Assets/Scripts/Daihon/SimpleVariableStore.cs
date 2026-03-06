// ==========================================================================
// SimpleVariableStore.cs
// IVariableStore のシンプルな Dictionary ベース実装。
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Daihon;

namespace Daihon.Unity
{
    /// <summary>
    /// <see cref="IVariableStore"/> のシンプルな実装。
    /// 変数を Dictionary で管理し、型の再代入チェックと一時変数のスコープ管理を行います。
    /// </summary>
    public class SimpleVariableStore : IVariableStore
    {
        private readonly Dictionary<string, DaihonValue> _variables = new Dictionary<string, DaihonValue>();

        /// <summary>変数が定義済みかどうか。</summary>
        public bool IsDefined(string name)
        {
            return _variables.ContainsKey(name);
        }

        /// <summary>変数の値を取得する。未定義の場合は例外をスローする。</summary>
        public DaihonValue GetValue(string name)
        {
            if (_variables.TryGetValue(name, out var value))
                return value;

            throw new DaihonRuntimeException($"未定義の変数「{name}」が参照されました。");
        }

        /// <summary>
        /// 変数に値を代入する。
        /// 既に定義済みの場合、型が異なる値への再代入は禁止する（仕様 §3.3）。
        /// </summary>
        public void SetValue(string name, DaihonValue value)
        {
            if (_variables.TryGetValue(name, out var existing))
            {
                // 型の再代入チェック（None 型は任意の型に変更可能）
                if (existing.Type != DaihonValue.ValueType.None
                    && value.Type != DaihonValue.ValueType.None
                    && existing.Type != value.Type)
                {
                    throw new DaihonRuntimeException(
                        $"型エラー: 変数「{name}」は {existing.Type} 型ですが、{value.Type} 型の値を代入しようとしました。");
                }
            }

            _variables[name] = value;
        }

        /// <summary>
        /// 初期値を設定する（まだ定義されていない変数のみ）。
        /// 既に定義済みの場合は何もしない。
        /// </summary>
        public void SetDefaultValue(string name, DaihonValue value)
        {
            if (!_variables.ContainsKey(name))
                _variables[name] = value;
        }

        /// <summary>一時変数（_接頭辞）をすべて破棄する。</summary>
        public void ClearTemporaryVariables()
        {
            var tempKeys = _variables.Keys.Where(k => k.StartsWith("_")).ToList();
            foreach (var key in tempKeys)
                _variables.Remove(key);
        }

        /// <summary>すべての変数をクリアする。</summary>
        public void ClearAll()
        {
            _variables.Clear();
        }
    }
}
