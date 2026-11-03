# 《空座》主线 Unity 项目

本仓库只包含桌面主线：启动场景 → 序章 → 第一章。Unity 版本为 **6000.3.26f1**。

## 打开与运行

1. 安装 Unity `6000.3.26f1`，并在克隆后运行 `git lfs install` 和 `git lfs pull`。
2. 用 Unity Hub 打开本仓库根目录，等待资源导入和脚本编译完成。
3. 打开 `Assets/KongZuo/Scenes/00_Bootstrap.unity` 并按 Play。
4. Windows 构建前，在 Build Profiles 确认主线三个场景已启用，且 Bootstrap 排在第一位。

首次打开时 Unity 会生成本机缓存；`Library/`、`Logs/`、`Temp/` 等目录不需要提交。

## 目录

- `Assets/KongZuo/Scenes/`：Bootstrap、序章和第一章。
- `Assets/KongZuo/Scripts/`：主线运行时代码。
- `Assets/KongZuo/Art/`：主线场景及戏楼模型使用的资源。
- `Assets/KongZuo/Audio/`：主线占位音效。
- `Assets/KongZuo/Editor/`：场景检查和编辑器辅助工具。
- `Packages/`、`ProjectSettings/`：Unity 包与项目设置。
- `Docs/`：项目状态、模型接入说明和主线 QA。

独立演示、旧版本和 ZIP 备份不放在这个主线仓库中。不要删除或手工重建 Unity 资源的 `.meta` 文件。

## 验证状态

Unity 导入与脚本编译已验证。项目目前没有 EditMode 自动化测试；完整 Play 流程和 Windows 构建仍需按 [主线 QA](Docs/QA_主线.md) 实际验收。

更多说明见[项目总览](Docs/项目总览与进度.md)和[戏楼模型接入说明](Docs/模型接入说明.md)。
