# 《空座》LML 第四周：谜题系统、音频管理、整合原型

版本：独立安全增量包 v2.0（针对已上传的旧 v0.3 工程结构，**不是**你们 GitHub 私有仓库当前完整状态）。Unity：6000.3.26f1，Universal 3D / URP，Windows PC。

## 开发边界及现状

你上传的 `KongZuo.zip` 中可读取到 `KZGameSession`、`KZStoryDirector`、`KZHud`、`KZInput` 等13个脚本，以及 `00_Bootstrap`、`01_Prologue`、`02_Chapter01` 三张场景。本交付包不会改动这些文件。

策划 V0.4 的第一章新谜题为：查看「留座戏」规定 → 获取1992年开戏前布置照片 → 在观众厅进行舞台双柱及侧梯三地标照片对位 → 阅读周明生场务记录 → 从【钥匙串/工牌/铜哨】推理遗物 → 返回一排七座 → 完整残响 → 2004年恢复。

注意：新「布置照片」不是原有的「旧合照」；不得替换掉旧合照、票根、序章节目单；它们仍是现有版本的线索资产。剧本确认的内容是沈芸坐过七号座、拿走钥匙；第四周 **不应**让玩家获知之后的案件真相。

## 最简单安装：只新增一个目录

1. 在 GitHub Desktop 的 `main` 执行 Fetch/Pull 更新；确保工作区没有待提交的他人修改。
2. 新建个人分支 `feature/lml-week4-systems`。**不要在 main 上操作。**
3. 关闭 Unity，在 GitHub Desktop 选择 `Repository → Show in Explorer`，找到真正的工程根目录（能看见 `Assets`、`Packages`、`ProjectSettings`）。
4. 从 ZIP 中找到 `Assets/KongZuo/LMLWeek4/`，把**整个 `LMLWeek4` 文件夹**复制到本机 `KongZuo/Assets/KongZuo/` 下。不要把 ZIP 直接塞到 `Assets`；不要新建重复的 `Assets/Assets`。
5. 如果工程中**已经存在** `Assets/KongZuo/LMLWeek4/`：不要覆盖，不要继续；先与主程序核对同名模块是否已经提交/修改。此包的代码文件目录均只在新目录内，不包含任何原有 `.cs` 或 `.unity`。
6. Unity Hub 使用 **6000.3.26f1 + Universal 3D(URP)** 打开**原项目**。如果 Package Manager 报错，先排除网络/依赖错误再运行生成器，不要乱删 manifest.json。
7. 等 Console 编译结束且无红色错误，菜单依次执行：
   - `空座 → LML 第四周 → 01 生成独立演示场景`
   - `空座 → LML 第四周 → 02 执行系统自测`
8. 新场景地址为 `Assets/KongZuo/LMLWeek4/Scenes/LML_Week4_Demo.unity`。打开该场景并点击 Play，按下方流程通关。
9. 验收后在 GitHub Desktop 仅提交 `Assets/KongZuo/LMLWeek4/**` 及 Unity 为其新生成的 `.meta` 文件。Push -> Pull Request，逐文件检查。**不要勾选你没理解的其他修改**。

> 生成器：如果演示场景已经存在，直接拒绝覆盖；不修改正式三张场景、不改 Build Settings、不更换项目设置，也不下载额外 Unity 包。演示场景是独立功能验收，并**没有自动接入正式第一章**。

## 演示方式（建议录屏顺序）

- `WASD` 移动，鼠标看向三份文档，`E` 阅读并再次 `E` 关闭后才记录线索；电话可选（用文字显示配音字幕）。
- 去观众厅，`F` 举起1992照片，缓慢移动并转动镜头，调整至舞台两根立柱 + 右侧台阶的「照片十字线」与场景地标重合；需要保持短暂稳定。
- `G` 打开遗物推理。输入 `2/3 + Enter`：答案错误但不锁死；`1 + Enter`：确认钥匙串，获得返回七号座目标。未收集三项线索、未对位时无法绕过。
- 走近第一排中央第7号座，`E` 触发锣声、黑场、1992年声音与人影、坐下、观众转头、拿走钥匙、返回2004。
- `F10` 打开音量设置（总音量/环境/音效/语音/音乐五项），`M` 切换试听2004/1992年环境声，`R` 完全重置当前测试，`Esc` 退出界面；鼠标释放后左键重新捕获。

## 代码分工与位置

| 文件 | 类职责 | 队友是否直接编辑 |
|---|---|---|
| Runtime/KZW4PuzzleState.cs | 纯 C# 线索收集、答案判定、残响幂等/中断状态 | 主程序维护 |
| Runtime/KZW4PuzzleDefinition.cs | ScriptableObject 定义题目、选项、答案、反馈 | 策划可在 Inspector 改配置，先评审 |
| Runtime/KZW4PhotoAlignment.cs | 三地标照片参考视角 / 当前视角的透视误差计算 | 主程序维护 |
| Runtime/KZW4KeyInput.cs | F / G / M / R / F10 的两种输入系统兼容 | 主程序维护 |
| Runtime/KZW4AudioCatalog.cs | 声音事件 ID、分类、片段、音量与冷却时间 | 音频组员改配置/素材，不必改 C# |
| Runtime/KZW4AudioManager.cs | 2D/3D音效、环境声切换、旁白字幕回调、分声道音量 | 主程序维护 |
| Runtime/KZW4DemoPlayer.cs | **演示场景专用**第一人称移动 | 不接管正式玩家控制器 |
| Runtime/KZW4DemoInteractable.cs | **演示场景专用**交互物标记 | 不更改原 `KZInteractable` |
| Runtime/KZW4DemoDirector.cs | 独立链路：证据→照片→解谜→七号座→残响→恢复 | 主程序维护 |
| Editor/KZW4DemoSceneSetup.cs | 一键生成仅在LML文件夹下的测试场景/配置 | 主程序维护 |
| Editor/KZW4SelfTest.cs | Editor 里运行组合/边界/几何测试 | 主程序/QA |
| Audio/Placeholder/*.wav | **程序合成占位**声音，仅用于测试 | 后续交由音频组员替换，不作正式配音 |

默认音效 ID：`ambience.2004`、`ambience.1992`、`paper`、`phone`、`gong`、`puzzle.ok`、`puzzle.fail`、`key`、`voice.phone`。后续素材只需要改 `KZW4_AudioCatalog.asset` 中的 `AudioClip`，并保留每个 ID。`voice.phone` 默认没有配音音频，但字幕逻辑仍可显示。可以单独为背景音乐创建 `music.default` 条目并调用 `PlayMusic`；当前演示不内置正式音乐。

## 第四周排期：任务完成定义

| 时间 | 具体工作 | 提交物 / 通过标准 |
|---|---|---|
| D1：安全基线 | 更新仓库、单独建分支；核对正式场景拥有者、Unity版本、包管理状态；安装模块 | Git diff 仅显示新增 LMLWeek4；无现有脚本被替换 |
| D2：谜题核心 | 验证证据乱序、缺线索拦截、三地标对位、错误和正确反馈 | Unity 自测通过；错答案无法推进，正确仅判定一次 |
| D3：音频 | 试听环境声/电话/纸声/锣声；音效冷却、独立音量、字幕优先 | 无持续叠播；无声音时字幕仍可阅读；音量可独立调节 |
| D4：整合演示 | 演示场景中完成“谜题→七号座→残响→2004”；场景中断恢复 | 从干净开局可重复演示；停Play后不破坏正式场景 |
| D5：团队联调 | 接入意见文档、录屏、测试截图、失败用例修复、PR | PR只包含自有目录和审查确认的最小变更；队员能从PR复现独立测试 |

> 时间安排是本包提供的开发建议，不是学校给你的刚性天数。若还剩更短时间，先确保 D1、D2、D4，再增加音频精修。

## 重要：正式第一章的整合“待审改动”（本包不自动执行）

原团队脚本 `KZStoryDirector.cs` 目前在 `SeatPuzzle` 解出后立即 `StartCoroutine(PlayEcho())`，而 V0.4 应调整为：

1. 第一章新的可调查内容由场景负责人批准后添加，不删除现有旧合照与票根；新版布置照片与场务记录是另起对象。
2. 新谜题判定成功时再把 `KZFlags.PuzzleSolved` 写入 `KZGameSession`，避免和现有剧情状态并行成两套真相。不要直接把测试状态当成正式存档。
3. 将正式 `KZStoryDirector` 的“旧座次图立即触发残响”调整为“新谜题解出后返回一排七座才调用 PlayEcho”；**只改极少数分支**，与负责剧情/关卡场景的队友一起审核。
4. 音频由统一经理接管，但先确认队友是否已建立自己的 AudioManager；不能在同一正式场景重复叠放两个管理器。
5. 完成场景接线后从 `00_Bootstrap` 正式走一次完整流程（本独立演示的过关 **不能** 当作这一项完成）。

详细的手工接入建议见 `正式第一章_最小整合审查清单.md`。

## 完成/未完成边界

已提供：完整独立模块源码、编辑器场景生成工具、8个程序合成占位音效、可调谜题资产、编辑器逻辑自测、演示流程、团队PR检查表。

未在当前环境完成：Unity 6000.3.26f1 的真实 C# 编译、Play/Windows Build、最终主工程场景接入及与队友最新未上传分支的兼容性验证。这三项必须由你们在本机/PR继续完成，不能把静态检查当作 Unity 真机合格证。
