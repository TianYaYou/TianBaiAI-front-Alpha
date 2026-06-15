# Repository Management

本项目现在按“Unity 大仓库 + WPF 启动器子仓库”的方式管理。

## 仓库关系

- 大仓库：`TianBaiAI-front-Alpha`
- 启动器子仓库：`TianBai-Launcher`
- 管理方式：Git submodule

大仓库只记录 `TianBai-Launcher` 当前指向的 commit，不直接追踪启动器内部文件。
这样 Unity 项目和启动器项目可以分别提交、分别推送，避免两个项目的构建产物和工程文件互相污染。

## 第一次克隆

```bash
git clone --recurse-submodules https://github.com/TianYaYou/TianBaiAI-front-Alpha.git
```

如果已经普通克隆了大仓库，再初始化启动器：

```bash
git submodule update --init --recursive
```

## 更新启动器

进入启动器目录后，按普通 Git 仓库开发：

```bash
cd TianBai-Launcher
git status
git pull
```

启动器提交完成后，回到大仓库记录新的 submodule 指针：

```bash
cd ..
git status
git add TianBai-Launcher
git commit -m "Update launcher submodule"
```

## 注意事项

- 不要在大仓库里直接把 `TianBai-Launcher` 内部文件当普通文件添加。
- 启动器自己的代码提交在 `TianBai-Launcher` 仓库完成。
- 大仓库只提交 `.gitmodules` 和 `TianBai-Launcher` 的 commit 指针变化。
- 如果启动器切换分支或提交，大仓库会显示 `TianBai-Launcher` 有变更，这是正常的。
