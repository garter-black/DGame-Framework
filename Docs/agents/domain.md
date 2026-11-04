# 领域文档（Domain Docs）

工程类技能在探索代码库时如何使用本仓库的领域文档。

## 探索前先读

- 仓库根目录的 **`GLOSSARY.md`**；或
- 若根目录存在 **`GLOSSARY-MAP.md`**，按它的指引读取每个上下文各自的 `GLOSSARY.md`，只读与当前主题相关的。
- **`Docs/adr/`**：读取与将要修改区域相关的 ADR。多上下文仓库还要检查 `src/<context>/Docs/adr/` 中的上下文级决策。

以上文件不存在时**直接继续**，不要提示缺失，也不要建议预先创建。`/domain-modeling` 技能（通过 `/grill-with-docs` 和 `/improve-codebase-architecture` 触发）会在术语或决策真正确定时按需创建。

## 文件结构

单上下文仓库（大多数情况，本仓库属于此类）：

```
/
├── GLOSSARY.md
├── Docs/adr/
│   ├── 0001-event-sourced-orders.md
│   └── 0002-postgres-for-write-model.md
└── src/
```

多上下文仓库（根目录存在 `GLOSSARY-MAP.md`）：

```
/
├── GLOSSARY-MAP.md
├── Docs/adr/                          ← 全系统决策
└── src/
    ├── ordering/
    │   ├── GLOSSARY.md
    │   └── Docs/adr/                  ← 上下文级决策
    └── billing/
        ├── GLOSSARY.md
        └── Docs/adr/
```

## 使用术语表中的词汇

输出中提到领域概念时（issue 标题、重构提案、假设、测试名），使用 `GLOSSARY.md` 中定义的术语，不要换成术语表明确避免的同义词。

如果需要的概念不在术语表中，说明两种可能：要么是在发明项目不用的说法（应重新考虑），要么是确实存在空缺（记下来交给 `/domain-modeling`）。

## 标出与 ADR 的冲突

如果输出与已有 ADR 矛盾，要明确指出，不要静默覆盖：

> _与 ADR-0007（订单事件溯源）矛盾，但值得重新讨论，因为……_
