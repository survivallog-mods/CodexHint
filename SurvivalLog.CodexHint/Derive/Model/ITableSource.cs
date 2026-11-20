using System.Collections.Generic;

namespace SurvivalLog.CodexHint.Derive.Model;

/// <summary>推导层数据入口：按表行类型给出全量行。
/// 离线测试用 JsonTableSource（gamekb 镜像），运行时用 GameTableSource（ConfigManager）。
/// 空表/文件缺失返回空列表，不抛。</summary>
public interface ITableSource
{
    IReadOnlyList<T> Rows<T>() where T : class;
}
