using System.Linq;
using SurvivalLog.CodexHint.Derive;

namespace SurvivalLog.CodexHint.Game;

/// <summary>plan 数据构建入口：表就绪后构建一次全量 JSON，日志输出七分类覆盖数
/// （与离线门槛 525/191/38/23/164/111/24、零降级 对拍）。</summary>
public static class CodexData
{
    private static string _json;
    private static bool _built;

    public static bool IsBuilt => _built;
    public static string PlanJson => _json;

    /// <summary>表未就绪（核心表空/Dict 未加载）返回 false 待下轮重试；
    /// 15 张全非空才 build（防半载状态产出残废 plan——实机#1 教训）。</summary>
    public static bool EnsureBuilt()
    {
        if (_built) return true;
        var src = new GameTableSource();
        if (src.Cm() == null) return false;
        if (src.Rows<Derive.Model.ItemRow>().Count < 1000) return false;        // 3899
        if (src.Rows<Derive.Model.ProductionRow>().Count == 0) return false;    // 838
        if (src.Rows<Derive.Model.PlantRow>().Count == 0) return false;         // 42
        if (src.Rows<Derive.Model.TrapRow>().Count == 0) return false;          // 5
        if (src.Rows<Derive.Model.RenoRow>().Count == 0) return false;          // 24
        if (src.Rows<Derive.Model.ShopRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.RandomGroupRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.PackageRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.StrangerStockRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.SceneRatRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.GroundLootRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.EventRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.RewardSetRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.RecipeRow>().Count == 0) return false;
        if (src.Rows<Derive.Model.FurnitureRow>().Count == 0) return false;

        _json = new UnlockPlanBuilder(src).BuildJson();
        _built = true;
        LogCoverage(src);
        return true;
    }

    private static void LogCoverage(GameTableSource src)
    {
        var json = System.Text.Json.JsonDocument.Parse(_json).RootElement;
        var plans = json.GetProperty("plans");
        var degraded = json.GetProperty("degraded").GetArrayLength();
        int Count(int cat) => plans.EnumerateObject().Count(p => p.Name.StartsWith(cat + ":"));
        CodexHintPlugin.Log?.LogInfo(
            $"[CodexHint] plan 就绪：食物{Count(1)} 菜肴{Count(2)} 植物{Count(3)} 猎物{Count(4)} " +
            $"制造{Count(5)} 家具{Count(6)} 装修{Count(7)} 降级{degraded} " +
            $"（离线门槛：191/525/38/23/164/111/24/0）");
    }
}
