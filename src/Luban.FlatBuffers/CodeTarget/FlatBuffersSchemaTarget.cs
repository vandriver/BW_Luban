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

using Luban.CodeFormat;
using Luban.CodeTarget;
using Luban.Defs;
using Luban.FlatBuffers.TemplateExtensions;
using Luban.FlatBuffers.TypeVisitors;
using Luban.Types;
using Scriban;
using Scriban.Runtime;

namespace Luban.FlatBuffers.CodeTarget;

[CodeTarget("flatbuffers")]
public class FlatBuffersSchemaTarget : AllInOneTemplateCodeTargetBase
{
    public override string FileHeader => "";

    protected override string FileSuffixName => "fbs";

    protected override ICodeStyle CodeStyle => CodeFormatManager.Ins.NoneCodeStyle;

    protected override string DefaultOutputFileName => "schema.fbs";

    // 老版魔改版不转义/不校验 flatbuffers 保留字（字段名 attribute/table 直接输出），
    // 因此这里返回空集合，跳过 ValidationTypeNames 的保留字检查。
    protected override IReadOnlySet<string> PreservedKeyWords => s_emptyPreservedKeyWords;

    private static readonly HashSet<string> s_emptyPreservedKeyWords = new();

    protected override void OnCreateTemplateContext(TemplateContext ctx)
    {
        ctx.PushGlobal(new FlatBuffersTemplateExtension());

        var maps = CollectKeyValueEntry(GenerationContext.Current.ExportBeans).KeyValueEntries.Values;
        ctx.PushGlobal(new ScriptObject()
        {
            {"__maps", maps},
        });

        // 嵌套 list/set/array（元素是容器）需要生成 List_X 包装表，
        // 否则 schema 会出现 [[T]] 嵌套 vector 导致 flatc 编译失败
        var lists = CollectNestedListEntry(GenerationContext.Current.ExportBeans).ListTypes;
        ctx.PushGlobal(new ScriptObject()
        {
            {"__lists", lists},
        });

        // 表 value bean 的主键字段（index field）在 schema 中标记 (key)，
        // 与老版魔改行为一致（bean 表体 id:int32(key);）。老版仅 map/list 表标记，
        // singleton(ONE) 表不标记；且仅单字段显式索引标记（联合索引 a+b 老版不标，
        // 自动取首字段的索引字段若被 group 过滤则不会出现在导出字段中）。
        var indexFields = new Dictionary<string, string>();
        foreach (DefTable table in GenerationContext.Current.ExportTables)
        {
            if ((table.IsMapTable || table.IsListTable) && table.IndexList.Count == 1 && table.ValueTType?.DefBean != null && table.IndexField != null)
            {
                indexFields.TryAdd(table.ValueTType.DefBean.FullName, table.IndexField.Name);
            }
        }
        ctx.PushGlobal(new ScriptObject()
        {
            {"__index_fields", indexFields},
        });

        // 只输出被引用的 bean（老版只输出 tables 引用闭包，不输出孤立类型定义）
        // 用 Dictionary 而非 HashSet：Scriban 模板仅支持索引器访问（set[key]）
        var refBeans = new Dictionary<string, bool>();
        var visited = new HashSet<string>();
        foreach (DefTable table in GenerationContext.Current.ExportTables)
        {
            CollectReferencedBeans(table.ValueTType, refBeans, visited);
        }
        ctx.PushGlobal(new ScriptObject()
        {
            {"__ref_beans", refBeans},
        });
    }

    private static void CollectReferencedBeans(TType type, Dictionary<string, bool> beans, HashSet<string> visited)
    {
        switch (type)
        {
            case TBean b: CollectReferencedBean(b.DefBean, beans, visited); break;
            case TList l: CollectReferencedBeans(l.ElementType, beans, visited); break;
            case TSet s: CollectReferencedBeans(s.ElementType, beans, visited); break;
            case TArray a: CollectReferencedBeans(a.ElementType, beans, visited); break;
            case TMap m:
                CollectReferencedBeans(m.KeyType, beans, visited);
                CollectReferencedBeans(m.ValueType, beans, visited);
                break;
        }
    }

    private static void CollectReferencedBean(DefBean bean, Dictionary<string, bool> beans, HashSet<string> visited)
    {
        if (!visited.Add(bean.FullName))
        {
            return;
        }
        beans[bean.FullName] = true;
        if (bean.IsAbstractType)
        {
            foreach (var c in bean.HierarchyNotAbstractChildren)
            {
                CollectReferencedBean(c, beans, visited);
            }
        }
        else
        {
            // 只遍历导出字段（HierarchyExportFields），与老版引用闭包一致：
            // 被 group 过滤字段引用的 bean 不输出（如 RecruitFilterItem 仅被 s 组字段引用）
            foreach (var field in bean.HierarchyExportFields)
            {
                CollectReferencedBeans(field.CType, beans, visited);
            }
        }
    }

    private MapKeyValueEntryCollection CollectKeyValueEntry(List<DefBean> beans)
    {
        var c = new MapKeyValueEntryCollection();

        foreach (DefBean bean in beans)
        {
            CollectMapKeyValueEntriesVisitor.Ins.Accept(bean, c);
        }

        return c;
    }

    private NestedListTypeCollection CollectNestedListEntry(List<DefBean> beans)
    {
        var c = new NestedListTypeCollection();

        foreach (DefBean bean in beans)
        {
            CollectNestedListTypesVisitor.Ins.Accept(bean, c);
        }

        return c;
    }
}
