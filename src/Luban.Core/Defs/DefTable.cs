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

using Luban.Diagnostics;
using Luban.RawDefs;
using Luban.Types;
using Luban.TypeVisitors;
using Luban.Utils;
using Luban.Validator;

namespace Luban.Defs;

public record class IndexInfo(TType Type, DefField IndexField, int IndexFieldIdIndex);

public class DefTable : DefTypeBase
{
    private static readonly NLog.Logger s_logger = NLog.LogManager.GetCurrentClassLogger();

    public DefTable(RawTable b)
    {
        Name = b.Name;
        Namespace = b.Namespace;
        Index = b.Index;
        ValueType = b.ValueType;
        Mode = b.Mode;
        InputFiles = b.InputFiles;
        Groups = b.Groups;
        Comment = b.Comment;
        ReadSchemaFromFile = b.ReadSchemaFromFile;
        Tags = b.Tags;
        _outputFile = b.OutputFile;
        CurrentVariant = b.CurrentVariant ?? "";
        Source = b.Source;
        // 老版兼容: mode=define 表的取值列/值注释列
        DefineValue = b.DefineValue;
        DefineComment = b.DefineComment;
    }

    public string Index { get; private set; }

    public string ValueType { get; }

    public TableMode Mode { get; }

    public bool ReadSchemaFromFile { get; }

    /// <summary>
    /// Selected variant name after resolve. Empty when the fallback table definition is used.
    /// </summary>
    public string CurrentVariant { get; }

    public bool IsSingletonTable => Mode == TableMode.ONE;

    // 老版兼容: define 表在编译/索引/校验上与 map 表一致, 仅导出格式不同(常量枚举)
    public bool IsMapTable => Mode == TableMode.MAP || IsDefineTable;

    public bool IsDefineTable => Mode == TableMode.DEFINE;

    public bool IsListTable => Mode == TableMode.LIST;

    /// <summary>define 表取值字段名(导出 {index: 该字段值})</summary>
    public string DefineValue { get; }

    /// <summary>define 表值注释字段名(导出时追加 -- 注释)</summary>
    public string DefineComment { get; }

    public bool IsExported { get; set; }

    public List<string> InputFiles { get; }

    private readonly string _outputFile;

    /// <summary>表定义 output 列(重命名目标名), 可能为空</summary>
    public string OutputFile => _outputFile;

    public TType KeyTType { get; private set; }

    public DefField IndexField { get; private set; }

    public int IndexFieldIdIndex { get; private set; }

    public TBean ValueTType { get; private set; }

    public TType Type { get; private set; }

    public bool IsUnionIndex { get; private set; }

    public bool MultiKey { get; private set; }

    public List<IndexInfo> IndexList { get; } = new();

    public List<ITableValidator> Validators { get; } = new();

    public string OutputDataFile => string.IsNullOrWhiteSpace(_outputFile) ? FullName.Replace('.', '_').ToLower() : _outputFile;

    public override void Compile()
    {
        var ass = Assembly;

        if ((ValueTType = (TBean)ass.CreateType(Namespace, ValueType, false)) == null)
        {
            throw new LubanException(Source, "error.def.table.value_type_not_exist", FullName, ValueType);
        }

        switch (Mode)
        {
            case TableMode.ONE:
            {
                IsUnionIndex = false;
                KeyTType = null;
                Type = ValueTType;
                break;
            }
            case TableMode.MAP:
            case TableMode.DEFINE:
            {
                IsUnionIndex = true;
                if (!string.IsNullOrWhiteSpace(Index))
                {
                    if (ValueTType.DefBean.TryGetField(Index, out var f, out var i))
                    {
                        // 老版魔改版容忍索引字段被 group 过滤（schema 不输出该字段，key 由数据隐含），
                        // 因此这里不校验 NeedExport
                        IndexField = f;
                        IndexFieldIdIndex = i;
                    }
                    else
                    {
                        throw new LubanException(Source, "error.def.table.index_not_exist", FullName, Index);
                    }
                }
                else if (ValueTType.DefBean.HierarchyFields.Count == 0)
                {
                    throw new LubanException(Source, "error.def.table.no_field", FullName);
                }
                else
                {
                    var f = ValueTType.DefBean.HierarchyFields[0];

                    // 老版魔改版容忍默认索引字段被 group 过滤（如 MapRefresh 的 id 是 s 组不导出）
                    IndexField = f;
                    Index = IndexField.Name;
                    IndexFieldIdIndex = 0;
                }
                KeyTType = IndexField.CType;
                Type = TMap.Create(false, null, KeyTType, ValueTType, false);
                this.IndexList.Add(new IndexInfo(KeyTType, IndexField, IndexFieldIdIndex));
                if (IsDefineTable)
                {
                    // 老版兼容: define 表校验取值列/值注释列存在(缺失会导致导出静默出错)
                    if (!ValueTType.DefBean.TryGetField(DefineValue, out _, out _))
                    {
                        throw new Exception($"table:'{FullName}' mode=define 的 define_value:'{DefineValue}' 字段不存在");
                    }
                    if (!string.IsNullOrWhiteSpace(DefineComment) && !ValueTType.DefBean.TryGetField(DefineComment, out _, out _))
                    {
                        throw new Exception($"table:'{FullName}' mode=define 的 define_comment:'{DefineComment}' 字段不存在");
                    }
                }
                break;
            }
            case TableMode.LIST:
            {
                var indexs = Index.Split('+', ',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
                foreach (var idx in indexs)
                {
                    if (ValueTType.DefBean.TryGetField(idx, out var f, out var i))
                    {
                        if (IndexField == null)
                        {
                            IndexField = f;
                            IndexFieldIdIndex = i;
                        }
                        this.IndexList.Add(new IndexInfo(f.CType, f, i));
                    }
                    else
                    {
                        throw new LubanException(Source, "error.def.table.index_not_exist", FullName, idx);
                    }
                }
                // 如果不是 union index, 每个key必须唯一，否则 (key1,..,key n)唯一
                IsUnionIndex = IndexList.Count > 1 && !Index.Contains(',');
                MultiKey = IndexList.Count > 1 && Index.Contains(',');
                break;
            }
            default:
                throw new LubanException(Source, "error.def.table.unknown_mode", Mode);
        }

        foreach (var index in IndexList)
        {
            TType indexType = index.Type;
            string idxName = index.IndexField.Name;
            if (indexType.IsNullable)
            {
                throw new LubanException(Source, "error.def.table.index_nullable", FullName, idxName);
            }
            if (!indexType.Apply(IsValidTableKeyTypeVisitor.Ins))
            {
                throw new LubanException(Source, "error.def.table.index_invalid_type", FullName, idxName, index.IndexField.Type);
            }
        }
    }
}
