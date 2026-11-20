using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using SurvivalLog.CodexHint.Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SurvivalLog.CodexHint;

/// <summary>
/// 图鉴解锁方案：在生存图鉴（Codex WebUI）未解锁条目详情面板运行时推导并直显解锁方案。
/// 设计/机制实证见 docs/superpowers/specs/2026-11-19-codex-hint-design.md。
/// 只读不改：不解锁条目、不写存档、不修改游戏文件；General.Enabled=false 停推送停注入
/// （已渲染面板随图鉴重开清除，遵循 CERail 口径）。
/// </summary>
public const string Version = "1.0.0";
[BepInPlugin("com.local.survivallog.codexhint", "图鉴解锁方案", Version)]
public sealed class CodexHintPlugin : BasePlugin
{
    internal static ManualLogSource Log;
    internal static ConfigEntry<bool> Enabled;

    public override void Load()
    {
        Log = base.Log;
        Enabled = Config.Bind("General", "Enabled", true, "总开关：false=停止注入与推送（已渲染面板随图鉴重开清除）。");
        if (!Enabled.Value)
        {
            Log.LogInfo("CodexHint loaded (enabled=false，跳过挂载)");
            return;
        }
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<CodexTicker>())
            ClassInjector.RegisterTypeInIl2Cpp<CodexTicker>();
        var go = new GameObject("CodexHint.Ticker");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<CodexTicker>();
        Log.LogInfo("CodexHint loaded (ticker 挂载，等待配置表就绪构建 plan)");
    }
}

/// <summary>每秒探一次：表就绪 → 构建 plan（一次）；后续 tick 由 Rail 通道接管（Task 8/9）。</summary>
internal sealed class CodexTicker : MonoBehaviour
{
    public CodexTicker(IntPtr ptr) : base(ptr) { }
    public CodexTicker() { }

    private float _probe;

    public void Update()
    {
        if (CodexHintPlugin.Enabled != null && !CodexHintPlugin.Enabled.Value) return;
        _probe += Time.deltaTime;
        if (_probe < 1f) return;
        _probe = 0f;
        try
        {
            if (!CodexData.IsBuilt) CodexData.EnsureBuilt();
            Rail.CodexRail.Tick(); // built 后接管：bootstrap/探活/推送/选中态
        }
        catch (Exception e) { CodexHintPlugin.Log?.LogWarning($"[CodexHint] tick 异常: {e.Message}"); }
    }
}
