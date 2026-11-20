using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace SurvivalLog.CodexHint.Tests;

public class SmokeTests
{
    [Fact]
    public void PluginTypeExists()
    {
        // 从文件加载而非 typeof 绑定：测试工程不引用 BepInEx，编译期绑定基类会失败；
        // LoadFrom + GetType 仅读 metadata，不解析 BasePlugin 基类链。
        var asm = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "SurvivalLog.CodexHint.dll"));
        Assert.NotNull(asm.GetType("SurvivalLog.CodexHint.CodexHintPlugin"));
    }
}
