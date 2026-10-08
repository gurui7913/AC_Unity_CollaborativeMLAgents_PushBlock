# Unity 多智能体协作：奖励设计项目档案

[English README](README.md) · [最终展示](docs/presentation/FinalPresentationSlides.pdf)

UCL Architectural Computation 的 Digital Ecologies 小组项目，完成于 2025 年；本次于 2026 年 10 月整理代码、场景、训练产物及文档。

**组员：** Du Hao、Gu Rui、Lu Haiyu、Pan Lingfeng。

**Gu Rui 的贡献：** 合作假设、自定义奖励、C# 贡献追踪，以及后续按人数绝对偏差调整奖励的公式。定量分析、环境调整和性能比较由队友负责，不作为个人成果。项目基于官方示例与 MA-POCA，没有自行开发训练算法或验证城市任务迁移。

| 目的 | 入口 |
|---|---|
| 整体介绍 | [英文 README](README.md) |
| 个人与团队贡献 | [贡献说明](docs/contributions.md) |
| 奖励、追踪方式和历史问题 | [奖励设计](docs/reward-design.md) |
| 自己负责的章节 | [44 页展示](docs/presentation/FinalPresentationSlides.pdf)，第 28–36、40 页 |
| 独立代码列表 | [分段版本](src/piecewise)、[偏差版本](src/absolute_deviation) |
| 恢复 Unity 场景 | [复现指南](docs/reproduction.md)、[场景导出](unity/exports) |
| 合作率数据 | [评估说明](docs/evaluation.md)、[原始 CSV](data/raw) |
| 整理规则与来源 | [归档说明](docs/archive.md)、[来源清单](archive_manifest.json) |

默认示例本来就有团队奖励和时间惩罚；这里增加参与人数条件。“贡献”是接触/时间窗口等启发式代理。早期代码有超额人数得到正奖励的符号问题，本次保留历史代码并解释，未悄悄修复。后期公式仍在进球时触发，不是每步密集反馈。

## 重新生成数据与图表

在仓库根目录运行：

```bash
python analysis/recompute_efficiency.py
```

仅需 Python 标准库，无需 GPU/pandas。

图表另需可选的 matplotlib 依赖，在独立的 **Python 3.10+** 环境中运行：

```bash
python -m pip install -r analysis/requirements-charts.txt
python analysis/plot_results.py
```

输出包含 PNG 及可复用的 SVG；这套绘图环境与历史 Unity 训练环境分开。

仓库保存的是部分场景导出，**不是 Unity Hub 可直接打开的完整工程**。按指南准备 release_20 上游工程，再导入一个版本并检查引用。同名类的两个版本，以及 `src` 和导出脚本，不要重复放入 `Assets`。

本次验证包括哈希、路径、CSV 重算与文档链接；没有运行 Unity、模型推理或训练。原始本地资料与远端 Git 历史均保留。
