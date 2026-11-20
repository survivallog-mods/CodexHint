using System.Collections.Generic;
using SurvivalLog.CodexHint.Derive.Model;

namespace SurvivalLog.CodexHint.Derive;

/// <summary>GroundLoot 点位名 → 中文地图名（Config_MapPoint 前缀匹配）。
/// 规则：MapPoint.PositionName 去掉尾缀 Start 得基名（NEWMaket01_afterStart→NEWMaket01_after、
/// MarketStart→Market、Small_MarketStart→Small_Market）；点位名取**最长命中基名**为前缀者
/// （Market_24→特价超市，Small_Market_5 优先命中 Small_Market→社区便利店）。
/// 映射不到返回 null——调用方整行不输出（宁缺勿谜语，内部名绝不给玩家看）。</summary>
public static class MapNames
{
    private static List<(string baseName, string name, bool pre)> _prefixes; // 按基名长度降序；pre=灾前
    private static readonly Dictionary<string, bool> _phase = new();        // 地图名 → 灾前？

    public static void Init(ITableSource src)
    {
        var list = new List<(string, string, bool)>();
        foreach (var m in src.Rows<MapPointRow>())
        {
            if (string.IsNullOrEmpty(m.PositionName) || string.IsNullOrEmpty(m.Name)) continue;
            var b = m.PositionName;
            if (b.EndsWith("Start")) b = b.Substring(0, b.Length - "Start".Length);
            var nm = (m.Name ?? "").Replace("\u200b", ""); // 地图名清理零宽字符
            if (b.Length == 0 || nm.Length == 0) continue;
            list.Add((b, nm, m.IsUnlock));
            _phase[nm] = m.IsUnlock;
        }
        list.Sort((a, b) => b.Item1.Length.CompareTo(a.Item1.Length)); // 长基名优先
        _prefixes = list;
    }

    public static string Resolve(string positionName)
    {
        if (string.IsNullOrEmpty(positionName) || _prefixes == null) return null;
        foreach (var (baseName, name, _) in _prefixes)
            if (positionName.StartsWith(baseName)) return name;
        return null;
    }

    /// <summary>地图阶段前缀：初始解锁（灾前可去）→「灾前」；后续解锁 →「野外探险」。</summary>
    public static string PhasePrefix(string mapName)
    {
        if (mapName == null || !_phase.TryGetValue(mapName, out var pre)) return "";
        return pre ? "灾前 " : "野外探险 ";
    }
}
