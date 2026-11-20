using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SurvivalLog.CodexHint.Derive;
using SurvivalLog.CodexHint.Derive.Model;

// 用法：ExportPlans <gamekb tables 目录> [输出目录]
// 产物：plans-export.json（全量）+ plans-review.md（分类统计与样例，供文案审阅）
var tables = args.Length > 0 ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "gamekb", "2026-11-12_26615062", "tables"));
var outDir = args.Length > 1 ? args[1] : AppContext.BaseDirectory;

var src = new JsonTableSource(tables);
var json = new UnlockPlanBuilder(src).BuildJson();
File.WriteAllText(Path.Combine(outDir, "plans-export.json"), json);

var doc = JsonDocument.Parse(json);
var plans = doc.RootElement.GetProperty("plans");
var catNames = new Dictionary<int, string> {
    {1,"食物（吃到）"},{2,"菜肴（做出）"},{3,"植物（收获）"},{4,"猎物（捕获/遭遇）"},
    {5,"制造（做出）"},{6,"家具（获得并放置）"},{7,"装修（获得）"} };

var lines = new List<string> { "# 解锁方案全量导出（文案审阅）", "" };
lines.Add($"条目：{plans.EnumerateObject().Count()} 有方案 / {doc.RootElement.GetProperty("degraded").GetArrayLength()} 降级");
lines.Add("");

// tag 统计
var tagCount = new Dictionary<string, int>();
foreach (var p in plans.EnumerateObject())
    foreach (var s in p.Value.EnumerateArray())
    {
        var t = s.GetProperty("tag").GetString();
        tagCount[t] = tagCount.TryGetValue(t, out var n) ? n + 1 : 1;
    }
lines.Add("## 来源 tag 分布");
foreach (var kv in tagCount.OrderByDescending(k => k.Value))
    lines.Add($"- {kv.Key}: {kv.Value} 条");
lines.Add("");

// 每分类样例（每 tag 至多 3 条）
foreach (var cat in catNames.Keys)
{
    lines.Add($"## {catNames[cat]}");
    var byTag = new Dictionary<string, List<string>>();
    foreach (var p in plans.EnumerateObject().Where(p => p.Name.StartsWith(cat + ":")))
        foreach (var s in p.Value.EnumerateArray())
        {
            var t = s.GetProperty("tag").GetString();
            var tx = s.GetProperty("text").GetString();
            if (!byTag.TryGetValue(t, out var l)) byTag[t] = l = new List<string>();
            if (l.Count < 3) l.Add(tx); // 每 tag 采前 3
        }
    foreach (var kv in byTag)
    {
        lines.Add($"- **{kv.Key}**");
        foreach (var tx in kv.Value) lines.Add($"  - {tx}");
    }
    lines.Add("");
}
File.WriteAllLines(Path.Combine(outDir, "plans-review.md"), lines);
Console.WriteLine($"written: plans-export.json / plans-review.md ({plans.EnumerateObject().Count()} entries)");
