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

using Luban.DataExporter.Builtin.Json;
using Luban.DataTarget;
using Luban.Defs;
using Luban.FlatBuffers.DataVisitors;
using Luban.Utils;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Luban.FlatBuffers.DataTarget;

[DataTarget("flatbuffers-json")]
public class FlatBuffersDataTarget : DataTargetBase
{
    protected override string DefaultOutputFileExt => "json";

    private void WriteAsTable(DefTable table, List<Record> datas, Utf8JsonWriter x)
    {
        x.WriteStartObject();
        // 如果修改了这个名字，请同时修改table.tpl
        x.WritePropertyName("data_list");
        if (table.IsSingletonTable)
        {
            // 老版魔改版 singleton 表 data_list 直接输出 bean 对象
            // （与 schema 的 `data_list:Bean;` 一致，flatc 才能正确转 bin）
            foreach (var d in datas)
            {
                d.Data.Apply(FlatBuffersJsonDataVisitor.Ins, x);
            }
        }
        else
        {
            x.WriteStartArray();
            foreach (var d in datas)
            {
                d.Data.Apply(FlatBuffersJsonDataVisitor.Ins, x);
            }
            x.WriteEndArray();
        }
        x.WriteEndObject();
    }

    public override OutputFile ExportTable(DefTable table, List<Record> records)
    {
        var ss = new MemoryStream();
        var jsonWriter = new Utf8JsonWriter(ss, new JsonWriterOptions()
        {
            Indented = !JsonDataTarget.UseCompactJson,
            SkipValidation = false,
            // 老版魔改版对 HTML 敏感字符(& < > ' " 等)做 \uXXXX 转义但保留中文，
            // 此处用 UnicodeRanges.All 编码器对齐老版产物（UnsafeRelaxedJsonEscaping 会直接输出 & 等字符）
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        });
        WriteAsTable(table, records, jsonWriter);
        jsonWriter.Flush();
        return CreateOutputFile($"{table.OutputDataFile}.{OutputFileExt}", DataUtil.StreamToBytes(ss));
    }
}
