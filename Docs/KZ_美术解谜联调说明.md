# 《空座》长明戏楼美术与第一章谜题联调

## 基础与范围

- 工程根目录：桌面 `KongZuo`，Unity `6000.3.26f1`。
- 解谜基础：`Assets/KongZuo/LMLWeek4/Scenes/LML_Week4_Demo.unity`。该场景已经连接 `KZW4DemoPlayer`、`KZW4DemoDirector`、`KZW4AudioManager`、三份证据、照片对景、遗物判断和残响。
- 美术基础：`Assets/KongZuo/ZhaiModel/KongZuo_OperaHouse_P2/Generated/PF_KZ_P2_OperaHouse_All.prefab`。这是长明戏楼入口、观众厅、舞台和 65 席座椅组成的完整 Prefab；源文件为 49 个 OBJ，没有 FBX。`KZ_P2_Interior_Preview.unity` 和 `KZ_P2_Exterior_Preview.unity` 是纯美术预览，没有谜题组件。
- V0.3 的 `KZV03Director` 与第四周的 `KZW4DemoDirector` 是独立状态流，联调场景只使用后者。主工程 `KZHud` 和原序章、第一章保持原样。

## 生成与试玩

1. 在 Unity Hub 打开桌面 `KongZuo`，确认 Console 没有编译错误并退出 Play。
2. 顶部菜单点击 **空座 → 联调 → 生成美术解谜整合场景**。生成器会使用 Unity 的 `AssetDatabase.CopyAsset` 复制原谜题场景，再在新场景中接入戏楼 Prefab，不修改原场景；重复点击只更新同一场景。
3. 点击 **空座 → 联调 → 打开美术解谜测试场景**，打开 `Assets/KongZuo/Scenes/KZ_ArtPuzzle_Integration.unity`。
4. 点击 Play。WASD 移动、鼠标观察、E 调查三份资料；到观众厅按 F 举照片，在参考机位对齐舞台两柱和右侧台阶；按 G 选“钥匙串”，回到第一排七座按 E 观看残响。F10 调音量，M 切换环境试听，R 重测。

生成器会在新场景中保留原解谜/UI/音频引用，将第一排七号座交互绑到美术座椅，为 65 席加碰撞，调整照片地标、残响路线、出生点，并加入入口、后墙、外院和舞台阶梯的简化碰撞。它会检查单一玩家、摄像机、AudioListener、Director、音频管理器及 Missing Script 数量。

## 当前验证边界

新工具已用 Unity 6.3.26f1 的程序集做 C# 编译检查，静态检查了源场景对象名称、65 席座椅、资源 GUID 和现有音频引用。独立启动 Unity 的验证副本时，许可证客户端未能建立连接；随后用户从 Unity Hub 正常打开工程，Unity 已实际生成并保存 `Assets/KongZuo/Scenes/KZ_ArtPuzzle_Integration.unity`。编辑器日志出现 `KZ_ART_PUZZLE_READY`，本次生成没有编译错误。场景文件静态检查：美术 Prefab 引用存在、16 个资源 GUID 均可解析、无 `m_Script: {fileID: 0}`、只有 1 个 Camera / AudioListener / CharacterController，并有 113 个 BoxCollider。2026-10-27 用户在 Unity 打开新场景并对“能否走到残响结束”的试玩询问回复“可以”；编辑器日志未见该场景新增的编译错误或运行时异常。碰撞边界的细致 QA 与 Windows Build 仍未验证。

占位音频 WAV 已在第四周资源中；`voice.phone` 没有配音文件，原系统会显示字幕。正式美术贴图、角色动画、V0.3 其他章节和主工程存档状态的打通仍待后续联调。