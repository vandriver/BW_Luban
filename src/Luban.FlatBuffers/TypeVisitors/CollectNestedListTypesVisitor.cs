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

using Luban.Defs;
using Luban.Types;
using Luban.TypeVisitors;

namespace Luban.FlatBuffers.TypeVisitors;

/// <summary>
/// 嵌套 list/set/array 的包装类型集合。
/// flatbuffers 不允许嵌套 vector，因此元素是容器时统一生成包装表：
/// table List_X { list: [X](required); }（X 为元素类型的包装名）。
/// </summary>
public class NestedListTypeCollection
{
    /// <summary>包装表名 -> 元素类型</summary>
    public Dictionary<string, TType> ListTypes { get; } = new();

    public HashSet<string> VisitedTypes { get; } = new();
}

public class CollectNestedListTypesVisitor : DecoratorActionVisitor<NestedListTypeCollection>
{
    public static CollectNestedListTypesVisitor Ins { get; } = new();

    public override void DoAccept(TType type, NestedListTypeCollection x)
    {
    }

    public void Accept(DefBean bean, NestedListTypeCollection x)
    {
        if (!x.VisitedTypes.Add(bean.FullName))
        {
            return;
        }
        if (bean.IsAbstractType)
        {
            foreach (var c in bean.HierarchyNotAbstractChildren)
            {
                Accept(c, x);
            }
        }
        else
        {
            foreach (var field in bean.HierarchyFields)
            {
                field.CType.Apply(this, x);
            }
        }
    }

    public override void Accept(TBean type, NestedListTypeCollection x)
    {
        Accept(type.DefBean, x);
    }

    private void AcceptContainer(TType listType, TType elemType, NestedListTypeCollection x)
    {
        if (elemType is TBean or TList or TSet or TArray or TMap)
        {
            // 先递归收集内层（list of list of ... 或 bean 内部字段）
            elemType.Apply(this, x);
            if (elemType is TList or TSet or TArray or TMap)
            {
                // 元素是容器：注册元素类型的包装表（字段类型名 [WrapName(elemType)] 引用它）。
                // 包装表体为 `list: [WrapName(元素的元素类型)]`，因此 value 必须是元素的元素类型：
                // 如 list,list,int32 注册 {List_int32: int32}（表体 [int32]）；
                //    list,list,list,int32 外层注册 {List_List_int32: List_int32（容器）}（表体 [List_int32]）。
                // 若 value 存容器本身会渲染出自我引用（list: [List_int32]）。
                TType elem = elemType switch
                {
                    TList l => l.ElementType,
                    TSet s => s.ElementType,
                    TArray a => a.ElementType,
                    _ => null,
                };
                if (elem != null)
                {
                    x.ListTypes[FlatBuffersTypeNameVisitor.WrapName(elemType)] = elem;
                }
            }
        }
    }

    public override void Accept(TArray type, NestedListTypeCollection x)
    {
        AcceptContainer(type, type.ElementType, x);
    }

    public override void Accept(TList type, NestedListTypeCollection x)
    {
        AcceptContainer(type, type.ElementType, x);
    }

    public override void Accept(TSet type, NestedListTypeCollection x)
    {
        AcceptContainer(type, type.ElementType, x);
    }

    public override void Accept(TMap type, NestedListTypeCollection x)
    {
        // map 的键/值内部可能嵌套 list（如 map,int,list,int 的 KeyValue_int32_List_int32）
        type.KeyType.Apply(this, x);
        type.ValueType.Apply(this, x);
    }
}
