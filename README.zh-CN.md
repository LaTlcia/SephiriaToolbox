# SephiriaToolbox

[English](README.md) | **简体中文**

《赛菲利亚》（Sephiria）的Dps检测、战斗统计和背包整理模组，Mac 和 Windows双端。

> 非官方模组，和游戏开发商 TEAMHORAY 没有关系。

## 功能

### 战斗统计

- 统计每个玩家、每个伤害来源（武器招式、神器、魔法、减益……）的伤害和平均每秒伤害。
- 分三页：「当前」看正在打的区域，「历史」按层翻看打完的区域，「总计」看整局。
- 明细可以按来源展开，也可以按伤害类型展开。

### 构筑和战绩

- 「构筑」页显示队伍里每个人的武器、神器、天赋和属性。
- 每局自动保存一份记录，在「战绩」页翻看：
  - 装备、天赋、属性
  - 难度词条
  - 每层 Boss 的词缀和防御
  - 结算
- 记录存在本机：
  - Mac：`~/Saved Games/Sephiria_DpsLogs`
  - Windows：`文档\Saved Games\Sephiria_DpsLogs`

### 整理背包

- 按游戏源码进行数值建模，覆盖套装、神器、武器、奇迹、天赋、减益、Boss 防御等。
- 在本地模拟每一种摆法，找出目标最高的摆法和石板等级分配，确认后自动执行整理。
- 可选项：
  - 目标：等级最多、综合输出、武器优先、魔法优先、自定义权重
  - 场景：单体、多目标
- 神器价值表：每个神器再 +1 级、或者拿掉，Dps变化情况。
- 锻造建议：比较手上武器能锻造成的几把武器，并给出建议（对于所使用预设有清楚认知的，用途不大）。
- 刻印建议：有刻印天赋时，提示刻掉哪块石板收益最大。**只做提示，不会主动刻掉石板。**
- 计算在后台线程进行，不影响游戏主线程。

## 快捷键

| 键 | 作用 |
|---|---|
| F7 | 最小化 / 展开 |
| F8 | 隐藏 / 显示 |
| F9 | 切换页 |
| F10 | 展开全部明细 |
| F5 / F6 | 翻页（Windows 也可以用 PgUp / PgDn） |

窗口右上角：「A- / A+」缩放，「EN / 中」切换界面语言，「锁定」防止误拖。

## 界面语言

界面有中文和英文两种：默认跟随游戏语言（游戏是中文就显示中文，否则显示英文），也可以用窗口右上角的「EN / 中」手动切换。

界面上的文字都在语言包 `Lang/zh-CN.json` 和 `Lang/en-US.json` 里，编进 DLL，代码里只写键名。想改译文或新增一种语言，不用重新编译：把 `lang/<语言>.json`（例如 `lang/en-US.json`）放进模组文件夹，用同样的键，里面的条目会覆盖内置的，新文件会作为新语言加进切换按钮。语言包里缺的条目显示中文。

## 安装

1. 准备好 `SephiriaToolbox` 文件夹，里面有 `SephiriaToolbox.dll` 和 `metadata.json`。可以下载发布好的压缩包，也可以自己编译（见下一节）。
2. 放进游戏的 `AddOns` 文件夹，没有就新建：
   - **Mac**：在 Steam 里右键游戏 →「管理」→「浏览本地文件」，右键 `Sephiria.app` →「显示包内容」。放成 `Sephiria.app/AddOns/SephiriaToolbox/`。
   - **Windows**：`Sephiria.exe` 所在的文件夹里，放成 `AddOns\SephiriaToolbox\`。
3. 重启游戏。模组在游戏启动时加载。

适配的游戏版本是 1.0.33。游戏更新后，如果模组出错，重新编译一次一般就好。

## 编译

需要：

- .NET SDK（用 .NET 10 编译过）。
- 已经安装的游戏。编译时会引用游戏自带的 DLL，这些文件不在仓库里。

```bash
dotnet build -c Release -o out
```

默认会在 Steam 的默认安装位置找游戏：

- Mac：`~/Library/Application Support/Steam/steamapps/common/Sephiria/Sephiria.app/Contents/Resources/Data/Managed`
- Windows：`C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed`

游戏装在别处时，指定游戏的 `Managed` 文件夹：

```bash
dotnet build -c Release -o out -p:SephiriaManaged="你的游戏目录下的 Managed 文件夹"
```

编译好后，把 `out/SephiriaToolbox.dll` 和 `metadata.json` 一起放进 `AddOns/SephiriaToolbox/`。

## 说明

- 所有计算都在本机完成，不联网，不上传任何数据。
- 多人游戏里会统计全队的伤害。

## 许可证

[MIT](LICENSE)
