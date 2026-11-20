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

using Luban.Datas;
using Luban.DataTarget;
using Luban.Defs;
using Luban.Lua.DataVisitors;
using System.Text;

namespace Luban.Lua.DataTarget;

[DataTarget("lua")]
public class LuaDataTarget : DataTargetBase
{
    public void ExportTableSingleton(DefTable t, Record record, StringBuilder result)
    {
        result.Append("return ").AppendLine();
        result.Append(record.Data.Apply(ToLuaLiteralVisitor.Ins));
    }

    public void ExportTableMap(DefTable t, List<Record> records, StringBuilder s)
    {
        s.Append("return").AppendLine();
        s.Append('{').AppendLine();
        foreach (Record r in records)
        {
            DBean d = r.Data;
            string keyStr = d.GetField(t.Index).Apply(ToLuaLiteralVisitor.Ins);
            if (!keyStr.StartsWith("[", StringComparison.Ordinal))
            {
                s.Append($"[{keyStr}] = ");
            }
            else
            {
                s.Append($"[ {keyStr} ] = ");
            }
            s.Append(d.Apply(ToLuaLiteralVisitor.Ins));
            s.Append(',').AppendLine();
        }
        s.Append('}');
    }

    /// <summary>
    /// 老版兼容: mode=define 表(旧版 ID 枚举表)导出常量枚举 return { KEY = value, -- comment }, 格式对齐 v3 产物;
    /// key 取 index 字段(字符串直接作 lua 标识符 key), value 取 define_value 字段,
    /// 注释取 define_comment 字段(表级配置了该列时逐条输出, 单条值为空则输出 ' -- ')
    /// </summary>
    public void ExportTableDefine(DefTable t, List<Record> records, StringBuilder s)
    {
        s.Append("return").AppendLine();
        s.Append('{').AppendLine();
        foreach (Record r in records)
        {
            DBean d = r.Data;
            var keyField = d.GetField(t.Index);
            if (keyField is DString keyData)
            {
                s.Append($"{keyData.Value} = ");
            }
            else
            {
                s.Append($"[{keyField.Apply(ToLuaLiteralVisitor.Ins)}] = ");
            }
            s.Append(d.GetField(t.DefineValue).Apply(ToLuaLiteralVisitor.Ins)).Append(',');
            if (!string.IsNullOrEmpty(t.DefineComment))
            {
                var comment = d.GetField(t.DefineComment);
                string commentStr = comment is DString c ? c.Value : comment.Apply(ToLuaLiteralVisitor.Ins);
                s.Append($" -- {commentStr.Replace("\n", " ")}");
            }
            s.AppendLine();
        }
        s.AppendLine("}");
    }

    public void ExportTableList(DefTable t, List<Record> records, StringBuilder s)
    {
        s.Append("return").AppendLine();
        if (t.IndexList.Count > 1)
        {
            // 多 key 表(如 heroLv: index="id+lv" 或 "id,lv") 输出逐级嵌套 map, 与 __tables__ 中的 index 深度一致,
            // 兼容校验脚本按深度遍历及运行时按 [id][lv] 索引, 结构同 v3 产物
            s.Append('{').AppendLine();
            EmitNestedTable(t, records, 0, s);
            s.Append('}');
        }
        else
        {
            s.Append('{').AppendLine();
            foreach (Record r in records)
            {
                DBean d = r.Data;
                s.Append(d.Apply(ToLuaLiteralVisitor.Ins));
                s.Append(',').AppendLine();
            }
            s.Append('}');
        }
    }

    private static void EmitNestedTable(DefTable t, List<Record> records, int depth, StringBuilder s)
    {
        if (depth == t.IndexList.Count - 1)
        {
            foreach (Record r in records)
            {
                DBean d = r.Data;
                AppendNestedKey(s, d.GetField(t.IndexList[depth].IndexField.Name).Apply(ToLuaLiteralVisitor.Ins));
                s.Append(d.Apply(ToLuaLiteralVisitor.Ins));
                s.Append(',').AppendLine();
            }
            return;
        }
        var groups = new Dictionary<string, List<Record>>();
        foreach (Record r in records)
        {
            string key = r.Data.GetField(t.IndexList[depth].IndexField.Name).Apply(ToLuaLiteralVisitor.Ins);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<Record>();
                groups.Add(key, list);
            }
            list.Add(r);
        }
        foreach (var kv in groups)
        {
            AppendNestedKey(s, kv.Key);
            s.Append('{').AppendLine();
            EmitNestedTable(t, kv.Value, depth + 1, s);
            s.Append("},").AppendLine();
        }
    }

    private static void AppendNestedKey(StringBuilder s, string keyStr)
    {
        if (!keyStr.StartsWith("[", StringComparison.Ordinal))
        {
            s.Append($"[{keyStr}] = ");
        }
        else
        {
            s.Append($"[ {keyStr} ] = ");
        }
    }

    protected override string DefaultOutputFileExt => "lua";

    /// <summary>
    /// 生成 __tables__.lua 全表注册表(运行时索引定义 + 表注释)。
    /// 表名与后处理重命名规则一致: output 列优先, 否则 value_type 首字母小写;
    /// 仅多 key 表输出 index 字段名数组(嵌套 map 深度), 单 key/无 key 表输出空表。
    /// </summary>
    public override OutputFile ExportTableManifest(List<DefTable> tables)
    {
        var sb = new StringBuilder();
        sb.Append("--------------this file id auto generated, don't modify it.--------------").AppendLine();
        sb.Append("return {").AppendLine();
        sb.Append("    tables = {").AppendLine();
        foreach (DefTable t in tables)
        {
            // __tables__.lua 仅输出 client 目标组(a/c)及未标注 group 的表, 对齐 v3 产物;
            // lua/s/test/build 等组的表不输出(客户端运行时不需要)
            if (t.Groups.Count > 0 && !t.Groups.Contains("a") && !t.Groups.Contains("c"))
            {
                continue;
            }
            string tableName = string.IsNullOrWhiteSpace(t.OutputFile)
                ? ToProjectName(t.ValueType)
                : t.OutputFile;
            sb.Append("        ").Append(tableName).Append(" = {");
            if (t.IndexList.Count > 1)
            {
                foreach (IndexInfo index in t.IndexList)
                {
                    sb.Append(' ').Append('"').Append(index.IndexField.Name).Append('"').Append(',');
                }
                // 去掉末尾逗号, 对齐 v3 产物格式 { "id", "lv" }
                sb.Length -= 1;
            }
            sb.Append(" }, --").Append(t.Comment ?? "").AppendLine();
        }
        sb.Append("    },").AppendLine();
        sb.Append("    enums = {").AppendLine();
        sb.Append("    }").AppendLine();
        sb.Append('}');
        return CreateOutputFile($"__tables__.{OutputFileExt}", sb.ToString());
    }

    /// <summary>
    /// value_type 转项目小驼峰表名(与 v3 产物命名一致):
    /// 前导连续大写 >=3 时小写前 3 个(缩写, 如 PVEChapter->pveChapter);
    /// <=2 时仅首字母小写(如 XGameplayCard->xGameplayCard); 全大写时整体小写(如 VIP->vip)。
    /// </summary>
    private static string ToProjectName(string valueType)
    {
        if (string.IsNullOrEmpty(valueType))
        {
            return valueType ?? "";
        }

        int i = 0;
        while (i < valueType.Length && char.IsUpper(valueType[i]))
        {
            i++;
        }

        if (i == valueType.Length)
        {
            return valueType.ToLowerInvariant();
        }

        if (i <= 2)
        {
            return char.ToLowerInvariant(valueType[0]) + valueType.Substring(1);
        }

        return valueType.Substring(0, 3).ToLowerInvariant() + valueType.Substring(3);
    }

    public override OutputFile ExportTable(DefTable table, List<Record> records)
    {
        var ss = new StringBuilder();
        if (table.IsDefineTable)
        {
            ExportTableDefine(table, records, ss);
        }
        else if (table.IsMapTable)
        {
            ExportTableMap(table, records, ss);
        }
        else if (table.IsSingletonTable)
        {
            ExportTableSingleton(table, records[0], ss);
        }
        else
        {
            ExportTableList(table, records, ss);
        }
        return CreateOutputFile($"{table.OutputDataFile}.{OutputFileExt}", ss.ToString());
    }
}
