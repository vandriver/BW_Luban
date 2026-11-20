// Copyright 2025 Code Philosophy
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Luban.DataLoader;
using Luban.Datas;
using Luban.DataVisitors;
using Luban.Defs;
using Luban.Utils;
using System.Text;

namespace Luban.Lua.DataVisitors;

public class ToLuaLiteralVisitor : ToLiteralVisitorBase
{
    public static ToLuaLiteralVisitor Ins { get; } = new();

    public override string Accept(DString type)
    {
        // lua 类型字段(如 drop.groups、formula.func)值是 lua 代码片段, 原样输出不带引号
        if (type.IsLuaCode)
        {
            return type.Value;
        }
        // 老版兼容: 以 '{' 开头的字符串视为 lua 表文本, 原样输出(如 drop.groups="{{1,1002,1,{500,10000}},...}"),
        // 与 v3 产物一致, 校验脚本按 table 遍历; 普通字符串正常转义带引号输出
        if (type.Value.StartsWith("{", StringComparison.Ordinal))
        {
            return type.Value;
        }
        return DataUtil.EscapeLuaStringWithQuote(type.Value);
    }

    public override string Accept(DBean type)
    {
        var x = new StringBuilder();
        // 字段间以 ", " 分隔且最后一项后不输出逗号, 对齐 v3 产物(如 {id=1, lv=2})
        bool hasPrevField = false;
        if (type.Type.IsAbstractType)
        {
            x.Append($"{{ {FieldNames.LuaTypeNameKey}='{DataUtil.GetImplTypeName(type)}'");
            hasPrevField = true;
        }
        else
        {
            x.Append('{');
        }

        int index = 0;
        foreach (var f in type.Fields)
        {
            var defField = (DefField)type.ImplType.HierarchyFields[index++];
            if (f == null || !defField.NeedExport())
            {
                continue;
            }
            if (hasPrevField)
            {
                x.Append(", ");
            }
            hasPrevField = true;
            // 非合法 Lua 标识符的字段名(如语言列 zh-CN、含点号/空格等)必须用 ['xxx'] 包裹, 否则生成非法 Lua 语法
            if (IsLuaIdentifier(defField.Name))
            {
                x.Append(defField.Name);
            }
            else
            {
                x.Append('[').Append(DataUtil.EscapeLuaStringWithQuote(defField.Name)).Append(']');
            }
            x.Append('=');
            x.Append(f.Apply(this));
        }
        x.Append('}');
        return x.ToString();
    }


    /// <summary>
    /// 字段名是否为合法 Lua 标识符(字母/下划线开头, 后跟字母/数字/下划线)。
    /// </summary>
    private static bool IsLuaIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || !(char.IsLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }
        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }
        return true;
    }

    private void Append(List<DType> datas, StringBuilder x)
    {
        // 元素间以 ", " 分隔且最后一项后不输出逗号, 对齐 v3 产物(如 {10301, 10304})
        x.Append('{');
        bool hasPrevElem = false;
        foreach (var e in datas)
        {
            if (hasPrevElem)
            {
                x.Append(", ");
            }
            hasPrevElem = true;
            x.Append(e.Apply(this));
        }
        x.Append('}');
    }

    public override string Accept(DArray type)
    {
        var x = new StringBuilder();
        Append(type.Datas, x);
        return x.ToString();
    }

    public override string Accept(DList type)
    {
        var x = new StringBuilder();
        Append(type.Datas, x);
        return x.ToString();
    }

    public override string Accept(DSet type)
    {
        var x = new StringBuilder();
        Append(type.Datas, x);
        return x.ToString();
    }

    public override string Accept(DMap type)
    {
        var x = new StringBuilder();
        x.Append('{');
        foreach (var e in type.DataMap)
        {
            x.Append('[');
            x.Append(e.Key.Apply(this));
            x.Append(']');
            x.Append('=');
            x.Append(e.Value.Apply(this));
            x.Append(',');
        }
        x.Append('}');
        return x.ToString();
    }
}
