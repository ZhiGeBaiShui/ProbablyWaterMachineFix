# Bug 报告：水分收集器的产出上限

**游戏版本：** 0.46（正式版，Mono）

**涉及类：** `MachineMoistureFarm`

**详细修复mod代码的github链接：https://github.com/ZhiGeBaiShui/ProbablyWaterMachineFix**

## 摘要

两个独立问题：产量计算 int 溢出导致归零；产出被固定的 6000 mL 中转容器截断。

---

## 问题 1：`GetOutputVolume` int 溢出

```csharp
public static int GetOutputVolume(GameItem gameItem)
{
    int currentPerformanceBonus = MachineryHelper.GetCurrentPerformanceBonus(gameItem);
    return Math.Max(850000 * (100 + currentPerformanceBonus) / 100, 0);
}
```

`850000 * (100 + currentPerformanceBonus) / 100` 在currentPerformanceBonus = 2427 时溢出——在mod环境下，这个溢出的阈值很容易到达，玩家较为容易触发这个bug。

**修法：**

```csharp
int bonus = MachineryHelper.GetCurrentPerformanceBonus(gameItem);
long volume = 850000L * ((long)bonus + 100) / 100; // 计算公式与850000 * (100 + currentPerformanceBonus) / 100等价
return (int)Math.Clamp(volume, 0, 2147483520L); // 溢出的冗余判断，溢出了采用最大值而不是静默归零
// 不采用Math.Clamp(volume, 0, int.MaxValue)是因为(int) (float) 2147483647 = -2147483648，GetOutputVolume返回的产水量int会经过float转换后再回转成int，直接调用上限会出现溢出错误
```

## 问题 2：`Fill` 固定 6000 mL 中转容器，收集器显示超过6000mL，实际产水最大只有6000mL

```csharp
public static void Fill(string quality, GameItem container, int volume)
{
    GameItem gameItem = ItemSpawner.Spawn("water_jug");   // 固定6000ml
    WaterHelper.AddBaseWater(gameItem, volume);
    WaterHelper.TransferLiquid(gameItem, container);
    gameItem.Destroy();
}
```

`AddWater` 按接收容器的 `LIQUID_CONTAINER_CAPACITY` 封顶，而水壶只有 6000 mL，所以产出超过 6 L 时在中转这一步就被截断，玩家放的容器多大都没用。
原版中的AI和自学习模组在后期能将收集器的加成提到6000ml产能以上，很容易触发这个bug问题。

**修法：**

```csharp
// A：直接灌目标容器，去掉中转
// 已知隐患：生成的目标容器是注册目录中的初始状态，而不是玩家放的容器状态，如果容器的tag被修改了，会导致目标容器的容量被修改
GameItem gameItem = ItemSpawner.Spawn(container.waterTag); // 伪代码，非实际实现，将生成一个固定water_jug标签的水容器实例替换为生成一个固定的container容器
WaterHelper.AddBaseWater(gameItem, volume);
```

```csharp
// B：生成中转水壶后，将容量对齐目标容器
GameItem jug = ItemSpawner.Spawn("water_jug");
int capacity = container.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").GetInt();
jug.ModifyTag("LIQUID_CONTAINER_CAPACITY", s => s.SetInt(capacity), skipValidate: true);
```