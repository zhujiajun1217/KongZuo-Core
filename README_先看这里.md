# 《空座》Unity 团队项目

**项目环境**：Unity 6.3 LTS `6000.3.26f1` · Universal Render Pipeline · C#。
**开发目标**：当前项目以 Windows PC 主线为准；仓库中的 Mobile 渲染配置不代表已完成手机适配。

> 第一次打开或想快速了解当前做到哪里：先看 [项目总览与进度](Docs/项目总览与进度.md)。该页区分主线、模型联调、第四周演示和 V0.3，并标明待验收内容。

## 1. 打开现有项目

1. 每位成员从团队 GitHub 仓库 Clone 项目，不要新建一个同名 Unity 项目再复制 `Assets` 合并。
2. 使用 Unity Hub 安装并打开 Unity `6000.3.26f1`，选择仓库根目录（包含 `Assets`、`Packages` 和 `ProjectSettings`）。
3. 等待 Unity 导入资源和编译脚本；检查 Console。若团队启用了 Git LFS，先完成 LFS 安装并拉取资源，再打开 Unity。
4. 要试玩主线，打开 `Assets/KongZuo/Scenes/00_Bootstrap.unity` 并按 Play。打包前在 `File → Build Profiles` 确认 Windows 目标、场景启用状态和启动顺序。
5. 最新戏楼模型的主线效果看 `01_Prologue.unity` / `02_Chapter01.unity`；主线使用 `Generated/Prefabs/KZ_OperaHouse_P0_V03_From11_Final.prefab`，单独模型预览及其他演示场景见[项目总览与进度](Docs/项目总览与进度.md)。

Unity 新建空项目时使用的场景生成菜单仅适用于初始化/测试，不要在现有主工程中重新生成或覆盖已有人编辑的场景。

**版本管理配置：**
- `Edit → Project Settings → Editor`：`Asset Serialization = Force Text`，保持 `.meta` 可见。
- `Edit → Project Settings → Player → Other Settings → Active Input Handling` 建议灰盒阶段为 `Both`；新旧输入系统都得到代码兼容。如果新输入系统包没有安装但需要使用，请在 Window → Package Manager 安装 Input System。
- `File → Build Profiles`：确认 Windows 平台，以及三个场景都启用，第一项为 `00_Bootstrap`。
- Unity 6 中 Build Profiles 取代旧 Build Settings 主入口；生成器通过 `EditorBuildSettings.scenes` 配置活动 Profile 场景清单。

## 2. 操作方式与试玩路线

**按键：** WASD/方向键移动；鼠标转头；E 检视、关闭文档/解谜；Tab 调查笔记；Esc 释放/锁定鼠标；谜题数字1/3切换人物，数字2测试锁定七号位，Enter提交。

### 序章《拆楼前夜》

1. 进入 Play，首先看到拆除前资产清点表；按 E 合上。
2. 准星看向木门，按 E 推门，进入前厅。
3. 到**西侧办公室**看桌上的固定电话并读完通话；到观众厅第一排中央看七号椅；到**东侧售票口**查看1992年登记表。**三项任意顺序**。
4. 确保进出过观众厅，从前厅方向重新进入；看到七号座人影，继续靠近前排。
5. 舞台灯一闪，人影消失；查看七号座上新出现的节目单（如果纸张被椅背遮挡，也可直接 E 调查七号椅）。
6. 合上节目单，显示《空座》标题并自动进入第一章。

### 第一章《座》

1. 在前厅读留座戏宣传册、旧合照（文字检视代替翻面）；在东侧售票口读残缺票根。
2. 在票根旁打开「一排座次示意图」；三项证据**没有齐全时可以打开但不能通过**。
3. 第四周临时谜题样例：一排六座安排观众甲，七座始终空，一排八座安排观众乙。使用1/3切换，Enter判定；错误可以重新提交，人物卡不可复制。
4. 正确后角色自动移到安全观察点，锁住行走但能看四周；聚光灯暗→1992年观众剪影出现→女子由侧台下场至七号座→坐下并取钥匙→观众转头→返回2004年。
5. E 合上“第一章结束”记录，继续场景检视。**第二章没有接入，不会假装已经完成。**

**场景灰盒只有大约5排×13座的占位模型**，而不是最终美术计划9排×13座。房间拓扑、门洞、舞台阶梯以策划V0.4为参考，但程序人员须和场景美术同学在本机检查碰撞。

## 3. 四人团队协作和 Git 流程

- 每位成员从同一远程仓库 Clone，在独立的 `feature/...` 分支开发；不要多人通过网盘同时编辑同一个物理项目目录。
- 提交 Unity 资源时保留并提交对应的 `.meta` 文件。不要提交 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/` 和本地 `Builds/`。
- 提交 PR 时写清楚改动版本/场景、验证步骤、实际结果和未完成项。合并前由负责人检查 Console、受影响场景和相应 QA 用例；Windows Build 只有实际构建并启动验证后才标为通过。
- `01_Prologue.unity` 和 `02_Chapter01.unity` 指定唯一场景负责人；其他成员优先在独立 Prefab/资源上工作，接入场景前先沟通。
- 在 [项目总览与进度](Docs/项目总览与进度.md) 登记四位成员的职责，并在 PR 合并后更新进度和验收状态。

**场景冲突约定：** 生成的 `01_Prologue.unity` 和 `02_Chapter01.unity` 需要指定唯一场景负责人；其他同学尽量在自己的 Prefab 上工作，使用程序统一接口绑定到主场景中。

## 4. 主程序修改位置

| 功能 | 文件 |
|---|---|
| 全局状态与唯一ID | `Scripts/Core/KZGameSession.cs` / `KZFlags.cs` |
| 场景加载入口 | `Scripts/Core/KZBootstrapEntry.cs` / `KZSceneNames.cs` |
| 输入兼容 | `Scripts/Gameplay/KZInput.cs` |
| 第一人称行走 | `Scripts/Gameplay/KZFirstPersonPlayer.cs` |
| E键射线调查 | `Scripts/Gameplay/KZInteractor.cs` / `KZInteractable.cs` |
| 观众厅触发盒 | `Scripts/Gameplay/KZStoryTrigger.cs` |
| 序章电话、人影、过章、残响 | `Scripts/Chapters/KZStoryDirector.cs` |
| 座次谜题可测试逻辑 | `Scripts/Chapters/KZSeatPuzzle.cs` |
| 文档检视 / 笔记 / 谜题 UI | `Scripts/UI/KZHud.cs` |
| 自动生成场景（只在Editor） | `Editor/KZMainProjectSetup.cs` |

**重点**：不要反复运行建场景菜单清空队友的资源。已生成场景只需继续修改已有 `.unity` 文件和 Prefab。`KZMainProjectSetup.cs` 仅作为一次性初始化和测试建场工具。

## 5. 当前已实现和未实现的边界

**P0原型已编码：**
- 启动场景→序章→第一章；角色第一人称行走、物品射线交互。
- 序章证据任意调查顺序；满足三个旗标且重返观众厅；一生只触发一次的人影事件；节目单及标题。
- 宣传册、旧合照、票根证据；座次图交互、错误/未填/缺证据判定、重复检查。
- 1992/2004简化状态组、占位角色沿路径移动、坐下与取钥匙时间节点、玩家移动锁定与恢复、调查笔记。
- 场景初始化的现有文件保护、URP材质占位、Build Profiles场景配置。

**仍待制作或验收的正式内容：**
- P0 戏楼模型已接入序章和第一章；正式演出效果、雨效/火焰/灰尘、录音、戏曲授权素材和 UI 正式美术仍需按团队计划制作/验收。
- 带骨骼的沈芸走路/坐下/取钥匙动画与精确IK；目前是简单几何体位置移动，不能代替最终演出。
- 物品旋转放大及真实的图片背面查看；现在用文字模拟“翻面”。
- 永久存档、设置、完整第二章至结局、真正的9×13座位谜题设计。
- 最终模型已接入序章和第一章并通过 Unity 编辑器结构检查；完整 Play 回归、碰撞细查和 Windows 打包验收仍需团队完成。美术/谜题联调场景的试玩记录不能代替主线和 Build 验收。
- Android/iOS 操作适配、打包和真机验收目前没有记录；Mobile URP 配置不等于已支持手机游玩。

## 6. 首次遇到问题怎么查

- **菜单没出现**：Console可能有别的包编译失败；`Assets/KongZuo/Editor` 文件夹路径必须正确。
- **粉色材质**：检查是否 Universal 3D / URP 模板，以及 `GraphicsSettings` 的URP资源已配置。
- **鼠标被锁/不能移动**：开始的工作单需要按E收起；Esc可以释放鼠标，左键重锁；检查 Input Handling。
- **无法触发人影**：按 Tab 对照电话、七号椅、登记表三项，再从前厅重进观众厅；不是“看过三个物品就立即触发”。
- **无法提交座次**：确认宣传册、合照、票根全读，按数字1和3切换、Enter判定。
- **Play有红色报错**：把 Console 第一条报错的完整内容与目标编辑器版本发给主程序负责人，不要盲目换Unity版本。

项目各开发线的状态、试玩入口和验证边界见 [Docs/项目总览与进度.md](Docs/项目总览与进度.md)。验收用例与逻辑检查见 `Docs/QA_第四周.md` 和 `Docs/greybox_logic_checks.py`。

## 7. 主程序负责人 LML 的交接约定

- **唯一主工程**：团队只创建一个正式 `KongZuo` 仓库，其他人 `git clone`，不能重新建同名 Unity 项目后靠复制 Assets 合并。
- **版本锁定**：Unity 6000.3.26f1；Unity 自动生成的 `Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/ProjectVersion.txt` 进入 Git。
- **文件负责人**：主程序维护 `Assets/KongZuo/Scripts/Core`、`.../Gameplay`、`.../Editor` 以及三张 `.unity` 场景；其他成员提交自己的 Prefabs / Art / Audio / UI，不要在未沟通时改核心场景。
- **每日集成**：从 main 拉取 → 在自己分支开发 → 提交含 `.meta` 文件的变更 → 主程序检查 Console 与 Play → 合并 main。
- **别直接把 ZIP 当成“已经做好的 Unity 工程”用 Unity Hub 打开**。先创建 Universal 3D 空工程，再合并此包的 `Assets/KongZuo`。
- **如果 Unity 中验证失败**：复制 Unity Console 第一条红色报错全文、对应文件行号、使用的 Editor 和 Input System 配置。不要直接换版本。

### 统一命名建议

- `KZ_` 前缀为公共程序资源；`ENV_` 环境；`P_` 可交互物件；`SFX_` 音效；`UI_` 界面；
- 场景名锁定为 `00_Bootstrap`、`01_Prologue`、`02_Chapter01`；
- 剧情旗标只能在 `KZFlags.cs` 集中声明；本周只实现内存状态，永久存档留到统一架构评审后再加。

### 不要误会当前完成程度

Unity 编辑器 Play、Windows Build 和手机真机测试必须分别实际执行并记录结果。不要将静态检查、模型预览或独立联调试玩写成主线/发行版本已通过。
