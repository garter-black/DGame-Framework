# Issue 追踪：GitHub（Issue tracker）

本仓库的 issue 和 spec 都以 GitHub issue 保存，所有操作使用 `gh` CLI。

## 约定（Conventions）

- **创建 issue（Create an issue）**：`gh issue create --title "..." --body "..."`。多行正文使用 heredoc。
- **读取 issue（Read an issue）**：`gh issue view <number> --json number,title,body,labels,comments`。
- **列出 issue（List issues）**：`gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`，按需加 `--label`、`--state` 过滤。
- **设为父 issue 的子 issue（Make an issue a sub-issue of a parent）**：`gh issue create --parent <parent> ...`，或事后 `gh issue edit <parent> --add-sub-issue <child>`（`gh` 2.94+）。旧版 `gh`：`gh api --method POST repos/fuyuoo/DGame-Framework/issues/<parent>/sub_issues -F sub_issue_id=<child-db-id>`（数据库 id，见下方“阻塞”）。不支持子 issue 时，在子 issue 正文顶部写 `Part of #<parent>`。
- **阻塞（Blocking）**：使用 GitHub **原生 issue 依赖**，这是界面可见的标准表示。用 `gh issue create --blocked-by <n>,<n> ...` 或 `gh issue edit <child> --add-blocked-by <blocker>` 添加（`gh` 2.94+）。旧版 `gh`：`gh api --method POST repos/fuyuoo/DGame-Framework/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`，其中 `<blocker-db-id>` 是阻塞方的数字**数据库 id**（`gh api repos/fuyuoo/DGame-Framework/issues/<n> --jq .id`，_不是_ `#编号` 或 `node_id`）。不支持依赖时，在子 issue 正文顶部写 `Blocked by: #<n>, #<n>`。所有阻塞方关闭后，该 ticket 即解除阻塞。
- **统计未关闭的阻塞方（Count open blockers）**：`gh api repos/fuyuoo/DGame-Framework/issues/<n> --jq .issue_dependencies_summary.blocked_by`（只计未关闭的；0 表示可以开始）。`gh issue view <n> --json blockedBy` 会包含已关闭的阻塞方，需过滤：`--jq '[.blockedBy.nodes[] | select(.state == "OPEN")] | length'`。
- **评论（Comment on an issue）**：`gh issue comment <number> --body "..."`
- **添加 / 移除标签（Apply / remove labels）**：`gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **关闭（Close）**：先用 `gh issue comment <number> --body "..."` 写明原因，再 `gh issue close <number>`。对已关闭的 issue，`gh issue close --comment` 会丢弃评论且仍返回 0。

本地有两个远端（`origin` = fork，`upstream` = AmaniDawn/DGame），且没有设置 `gh` 默认仓库，所以 `gh issue` / `gh pr` / `gh label` 一律带 `-R fuyuoo/DGame-Framework`；`gh api` 路径使用 `repos/fuyuoo/DGame-Framework/...`。不要向 `AmaniDawn/DGame` 或 `mattpocock/skills` 提交 issue。

## PR 作为分诊入口（Pull requests as a triage surface）

**PR 作为需求入口：否（no）。** _（如果本仓库把外部 PR 当作功能请求，改为 `yes`；`/triage` 会读取此项。）_

设为 `yes` 时，PR 与 issue 使用相同的标签和状态，对应 `gh pr` 命令：

- **读取 PR（Read a PR）**：`gh pr view <number>`，再 `gh pr view <number> --comments`（非 TTY 下 `--comments` 只输出评论、不含正文），`gh pr diff <number>` 查看 diff。
- **列出待分诊的外部 PR**：`gh api --paginate 'repos/{owner}/{repo}/pulls?state=open' --jq '.[] | select(.author_association | IN("OWNER","MEMBER","COLLABORATOR") | not) | {number, title, author: .user.login, author_association, labels: [.labels[].name]}'`。
- **评论 / 标签 / 关闭**：`gh pr comment`、`gh pr edit --add-label`/`--remove-label`、`gh pr close`。

GitHub 的 issue 和 PR 共用编号，单独的 `#42` 可能是任一种：先 `gh pr view 42`，失败再 `gh issue view 42`。

## 技能说“发布到 issue tracker”时（publish to the issue tracker）

创建一个 GitHub issue。

## 技能说“获取相关 ticket”时（fetch the relevant ticket）

按上方“读取 issue”读取。

## Wayfinding 操作（Wayfinding operations）

供 `/wayfinder` 使用。**地图（map）** 是一个 issue，其 **子 issue（child）** 就是各个 ticket。

- **地图（Map）**：一个带 `wayfinder:map` 标签的 issue，正文包含 Notes / Decisions-so-far / Fog。`gh issue create --label wayfinder:map`。
- **子 ticket（Child ticket）**：以 GitHub 子 issue 方式挂到地图下（见“设为父 issue 的子 issue”）。不支持子 issue 时，把它加入地图正文的任务列表，并在子 issue 正文顶部写 `Part of #<map>`。标签：`wayfinder:<type>`（`research`/`prototype`/`grilling`/`task`）。认领后指派给负责的开发者。
- **阻塞（Blocking）**：见“约定”中的“阻塞”。
- **前沿查询（Frontier query）**：列出地图下未关闭的子 issue（`gh issue list --state open`，限定在地图的子 issue / 任务列表内），去掉仍有未关闭阻塞方（见“统计未关闭的阻塞方”，或 `Blocked by` 行中仍未关闭的 issue）或已有指派人的，按地图顺序取第一个。
- **认领（Claim）**：`gh issue edit <n> --add-assignee @me`，作为本次会话的第一个写操作。
- **解决（Resolve）**：`gh issue comment <n> --body "<答案>"`，再 `gh issue close <n>`，然后在地图的 Decisions-so-far 中追加一条上下文指针（要点 + 链接）。
