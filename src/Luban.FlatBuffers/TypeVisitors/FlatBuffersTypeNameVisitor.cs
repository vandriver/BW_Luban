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

using Luban.Types;
using Luban.TypeVisitors;
using Luban.Utils;

namespace Luban.FlatBuffers.TypeVisitors;

public class FlatBuffersTypeNameVisitor : ITypeFuncVisitor<string>
{
    public static FlatBuffersTypeNameVisitor Ins { get; } = new();

    // flatbuffers 不允许嵌套 vector（vector of vector 会被 flatc 拒绝），
    // 因此嵌套容器统一包装为单字段表：List_X { list: [X]; } / KeyValue_K_V { key; value; }。
    // WrapName 返回去掉最外层数组括号的包装表名（与字段类型名 `[WrapName(t)]` 配套使用）。
    public static string WrapName(TType type)
    {
        switch (type)
        {
            case TList l: return $"List_{WrapName(l.ElementType)}";
            case TSet s: return $"List_{WrapName(s.ElementType)}";
            case TArray a: return $"List_{WrapName(a.ElementType)}";
            case TMap m: return $"KeyValue_{WrapName(m.KeyType)}_{WrapName(m.ValueType)}";
            default: return type.Apply(Ins);
        }
    }

    public string Accept(TBool type)
    {
        return "bool";
    }

    public string Accept(TByte type)
    {
        return "ubyte";
    }

    public string Accept(TShort type)
    {
        return "int16";
    }

    public string Accept(TInt type)
    {
        return "int32";
    }

    public string Accept(TLong type)
    {
        return "int64";
    }

    public string Accept(TFloat type)
    {
        return "float32";
    }

    public string Accept(TDouble type)
    {
        return "float64";
    }

    public string Accept(TEnum type)
    {
        // 老版魔改不生成 enum 定义，枚举字段一律映射为 int32
        return "int32";
    }

    public string Accept(TString type)
    {
        return "string";
    }

    public string Accept(TDateTime type)
    {
        return "int64";
    }

    public string Accept(TBean type)
    {
        return TypeUtil.MakeFlatBuffersFullName(type.DefBean.Namespace, type.DefBean.Name);
    }

    // 字段类型名只对元素做包装：list,list,int32 -> [List_int32]（老版行为），
    // 若对整体 WrapName 会多包一层（[List_List_int32]）
    public string Accept(TArray type)
    {
        return $"[{WrapName(type.ElementType)}]";
    }

    public string Accept(TList type)
    {
        return $"[{WrapName(type.ElementType)}]";
    }

    public string Accept(TSet type)
    {
        return $"[{WrapName(type.ElementType)}]";
    }

    public string Accept(TMap type)
    {
        return $"[{WrapName(type)}]";
    }
}
