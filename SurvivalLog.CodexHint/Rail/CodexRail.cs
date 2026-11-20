using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using SurvivalLog.CodexHint.Game;

namespace SurvivalLog.CodexHint.Rail;

/// <summary>WebView 通道（RailUi 模式移植）：Root 帧 bootstrap 幂等注入 + 探活 +
/// plan 全量推送 + 选中态推送。全部 ExecuteJavaScript 走带回调重载
/// （v1.8.10 CERail 铁证：单参重载本机 Vuplex/Il2Cpp 组合下静默丢弃）。
/// 选中态：GetStateByUI(Il2CppType.From(typeof(WebUI_Codex))) → State_Web_Codex
/// （RailCommands.cs 同款姿势；字符串 Type.GetType 实测拿不到类型——首版全空根因）。</summary>
internal static class CodexRail
{
    private static string _bootstrap;
    private static bool _alive;          // 探活确认（WebView 重建复位重注）
    private static bool _planPushed;     // 本次存活期 plan 已推
    private static string _lastSel = ""; // 上次选中键（变化才推）
    private static bool _selLogged;      // 选中态通道就绪日志（一次）
    private static readonly HashSet<string> _warned = new();
    private static Il2CppSystem.Action<string> _ackCb;   // 空回执（纪律：带回调）
    private static Il2CppSystem.Action<string> _probeCb; // 探活回执
    private static bool _consoleHooked;
    private static readonly HashSet<string> _consoleSeen = new();

    /// <summary>Ticker 每秒调；plan 就绪后开始工作。</summary>
    public static void Tick()
    {
        try
        {
            if (!CodexData.IsBuilt) return;
            var web = GetWebView();
            if (web == null) return;

            HookConsole(web);
            InjectBootstrap(web);
            Probe(web);
            if (!_alive) return;

            if (!_planPushed)
            {
                Exec(web, "window.__CODEXHINT__.setData(" + JsStr(CodexData.PlanJson) + ")");
                _planPushed = true;
                CodexHintPlugin.Log?.LogInfo("[CodexHint] plan 已推送 WebView");
            }
            PushSelectionIfChanged(web);
        }
        catch (Exception e)
        {
            WarnOnce("tick", "rail tick: " + e.Message);
        }
    }

    internal static Vuplex.WebView.IWebView GetWebView()
    {
        try
        {
            if (!GameCore.HotUpdate.BaseSingleton<GameCore.HotUpdate.ReduxUI.ReduxUISystem>.IsInstanceCreated)
                return null;
            var rus = GameCore.HotUpdate.BaseSingleton<GameCore.HotUpdate.ReduxUI.ReduxUISystem>.Instance;
            var layer = rus?.GetWebUILayer();
            return layer?.canvasWebViewPrefab?.WebView;
        }
        catch { return null; }
    }

    private static void InjectBootstrap(Vuplex.WebView.IWebView web)
    {
        if (_alive) return; // 存活期不重注（探活失败才复位重注）
        _bootstrap ??= "window.__CODEXHINT__||(function(){" +
            ReadResource("SurvivalLog.CodexHint.Rail.codexhint.page.js") + "})()";
        Exec(web, _bootstrap);
    }

    private static void Probe(Vuplex.WebView.IWebView web)
    {
        if (_probeCb == null)
            _probeCb = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<string>>(
                new Action<string>(v =>
                {
                    var wasAlive = _alive;
                    _alive = v == "ok";
                    if (_alive && !wasAlive)
                        CodexHintPlugin.Log?.LogInfo("[CodexHint] bootstrap 探活确认（__CODEXHINT__ 就绪）");
                    if (!_alive && wasAlive)
                    {
                        _planPushed = false; // WebView 重建：下次存活重推
                        _lastSel = "";
                        CodexHintPlugin.Log?.LogInfo("[CodexHint] 探活复位（WebView 重建），待重注重推");
                    }
                }));
        web.ExecuteJavaScript("window.__CODEXHINT__?window.__CODEXHINT__.probe():'0'", _probeCb);
    }

    private static void PushSelectionIfChanged(Vuplex.WebView.IWebView web)
    {
        var sel = ReadCodexSelection();
        if (sel == null) { WarnOnce("selNull", "选中态读取为 null（State_Web_Codex 不可得，面板不会渲染）"); return; }
        var key = sel.Value.cat + ":" + sel.Value.sid;
        if (key == _lastSel || key.EndsWith(":0")) return; // 0=未选中
        _lastSel = key;
        if (!_selLogged)
        {
            _selLogged = true;
            CodexHintPlugin.Log?.LogInfo($"[CodexHint] 选中态通道就绪（首个 {key}）");
        }
        Exec(web, $"window.__CODEXHINT__.setSelection({sel.Value.cat},{sel.Value.sid})");
    }

    /// <summary>游戏 Redux 选中态（RailCommands.cs 同款：Il2CppType.From + TryCast）。
    /// CurrentTab 是 UI 侧 0 基 tab 索引（WebBind "_TabMsg/currentTab"），+1 → CodexCategory 1-7。</summary>
    private static (int cat, int sid)? ReadCodexSelection()
    {
        try
        {
            if (!GameCore.HotUpdate.BaseSingleton<GameCore.HotUpdate.ReduxUI.ReduxUISystem>.IsInstanceCreated)
                return null;
            var rus = GameCore.HotUpdate.BaseSingleton<GameCore.HotUpdate.ReduxUI.ReduxUISystem>.Instance;
            if (rus == null) return null;
            var state = rus.GetStateByUI(Il2CppType.From(typeof(GameCore.HotUpdate.ReduxUI.WebUI_Codex)));
            var codex = state?.TryCast<GameCore.HotUpdate.ReduxUI.State_Web_Codex>();
            if (codex == null) return null;
            return (codex.CurrentTab.Value + 1, codex.SelectedSourceId.Value);
        }
        catch (Exception e) { WarnOnce("selRead", "选中态读取异常: " + e.Message); return null; }
    }

    /// <summary>WebView 控制台转发（RailUi.HookConsole 同款；事件以 add_X 暴露，
    /// 泛型委托须转 Action<Il2CppSystem.Object, T>）——JS 遥测/异常进 BepInEx 日志。</summary>
    private static void HookConsole(Vuplex.WebView.IWebView web)
    {
        if (_consoleHooked || web == null) return;
        try
        {
            var handler = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<
                Il2CppSystem.EventHandler<Vuplex.WebView.ConsoleMessageEventArgs>>(
                new Action<Il2CppSystem.Object, Vuplex.WebView.ConsoleMessageEventArgs>(
                    (s, e) => OnConsoleMessage(e)));
            web.add_ConsoleMessageLogged(handler);
            _consoleHooked = true;
            CodexHintPlugin.Log?.LogInfo("[CodexHint] WebView 控制台转发已挂");
        }
        catch (Exception e)
        {
            _consoleHooked = true; /* 挂不上不重试 */
            WarnOnce("consoleHook", "控制台转发挂载失败: " + e.Message);
        }
    }

    private static void OnConsoleMessage(Vuplex.WebView.ConsoleMessageEventArgs e)
    {
        try
        {
            var msg = e?.Message;
            if (string.IsNullOrEmpty(msg) || !msg.Contains("[CodexHint]")) return; // 只看自家遥测，免刷屏
            string lvl;
            try { lvl = e.Level.ToString(); } catch { lvl = "?"; }
            var key = lvl + "|" + (msg.Length > 150 ? msg.Substring(0, 150) : msg);
            if (_consoleSeen.Count > 400) _consoleSeen.Clear();
            if (!_consoleSeen.Add(key)) return;
            CodexHintPlugin.Log?.LogInfo($"[CodexHint][JS:{lvl}] {msg}");
        }
        catch { }
    }

    private static void WarnOnce(string key, string text)
    {
        if (_warned.Add(key)) CodexHintPlugin.Log?.LogWarning("[CodexHint] " + text);
    }

    private static void Exec(Vuplex.WebView.IWebView web, string js)
    {
        if (_ackCb == null)
            _ackCb = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<string>>(new Action<string>(_ => { }));
        web.ExecuteJavaScript(js, _ackCb);
    }

    private static string ReadResource(string name)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(name);
        if (s == null) { WarnOnce("res", "内嵌资源缺失: " + name); return ""; }
        using var r = new System.IO.StreamReader(s, System.Text.Encoding.UTF8);
        return r.ReadToEnd();
    }

    /// <summary>JSON 文本 → JS 字符串字面量（含引号/换行/反斜杠转义）。</summary>
    private static string JsStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\n", "\\n").Replace("\r", "") + "\"";
}
