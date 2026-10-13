using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;

namespace ProbablyWaterMachineFix
{
    // 原版 WaterHelper.AddWater 的等价复制（全内联版，形状贴近原版）。
    //
    // ⚠️ 行为保持原样——6000 mL 的具体修改后续再做。
    // ⚠️ 保真度：①-⑥ 段逐条对齐；⑦-⑪ 段为"结构级"（调用顺序/涉及的函数已确认）。
    //    启用后请先测：加水 / 倒水 / 过滤器 / 卖水。
    // ⚠️ 与原版的必要差异只有一处：原版内联传 lambda 给 ModifyTag；
    //    C# 里必须用 DelegateSupport.ConvertDelegate 转换（已内联在各调用点）。
    internal static class AddWaterCopy
    {
        internal static void Run(GameItem waterContainer, int grade, int addAmount, bool useRange,
                                 int lowPara, int highPara, bool initWaterFeature)
        {
            // 1. 预置水：成分 → 量（grade == 2 取另一套）
            var premade = grade == 2 ? WaterPremadeHelper.baseWaterContaminantDistribution
                                     : WaterPremadeHelper.waterContaminantDistribution;

            // 2. 容器与容量
            if (waterContainer == null) return;
            if (!waterContainer.IsTag("LIQUID_CONTAINER_TAG")) return;
            TagState capTag = waterContainer.GetTagReadonly("LIQUID_CONTAINER_CAPACITY");
            if (capTag == null) return;
            int capacity = capTag.valueInt;                    // ★ 上限的唯一来源

            // 3. 现有各成分与合计
            var cur = new Dictionary<string, int>();
            int total = 0;
            foreach (Liquid liquid in Liquid.Liquids)
            {
                int v = waterContainer.GetTagReadonly(liquid.currentPart).valueInt;
                cur[liquid.currentPart] = v;
                total += v;
            }

            // 4. 可用空间（传入量在这里封顶）
            int free = capacity - total;
            if (addAmount != -1) free = System.Math.Min(addAmount, free);
            if (free <= 0) return;

            // 5. 本次加多少：grade → (low, high) 区间 → 随机比例
            int low, high;
            if (useRange)      { low = lowPara; high = highPara; }
            else if (grade == 0) { low = 9990; high = 9999; }
            else if (grade == 1) { low = 3500; high = 8999; }
            else if (grade == 2) { low = 9000; high = 9599; }
            else if (grade == 3) { low = 9600; high = 9799; }
            else if (grade == 4) { low = 9800; high = 9899; }
            else if (grade == 5) { low = 9900; high = 9989; }
            else                 { low = 9990; high = 9999; }

            int pct    = RNG.GetRandomInt(low, high);                          // 0..10000
            int amount = System.Math.Min(free, (free * pct + 9999) / 10000);   // "水"的量
            int rest   = free - amount;                                        // 其余成分总量

            // 6. 按预置比例把 rest 分到各成分（float 乘 → 截断）
            var add = new Dictionary<string, int>();
            int added = 0;
            foreach (var pair in premade)
            {
                int v = (int)(rest * pair.Value);
                add[pair.Key] = v;
                added += v;
            }

            // 7. 余数补给
            if (rest - added > 0)
            {
                foreach (string key in premade.Keys)
                {
                    add[key] += rest - added;
                    break;
                }
            }

            // 8. 超容量回削：把已有液体按比例挤掉，给新水腾位置
            int sum = 0;
            foreach (int v in cur.Values) sum += v;
            if (sum > 0)
            {
                int over = sum;
                foreach (string key in cur.Keys.ToList())
                {
                    int v = cur[key];
                    int nv = System.Math.Max(v - (int)(over * (v / (float)amount)), 0);
                    waterContainer.ModifyTag(key, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(
                        (System.Action<TagState>)(t => t.valueInt = nv)));
                }
            }

            // 9. 过滤器：逐成分限流
            GameItem filter = WaterHelper.GetFilterItem(waterContainer);
            if (filter != null)
            {
                foreach (string key in cur.Keys.ToList())
                {
                    int nv = WaterHelper.HandleFilter(key, cur[key], waterContainer, filter);
                    waterContainer.ModifyTag(key, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(
                        (System.Action<TagState>)(t => t.valueInt = nv)));
                }
            }

            // 10. 水按 amount 加上去
            string waterId = Liquid.GetCurrentPartFromId("water");
            int waterValue = cur[waterId] + amount;
            waterContainer.ModifyTag(waterId, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(
                (System.Action<TagState>)(t => t.valueInt = waterValue)));

            // 11. 其余成分按 premade 的值累加
            foreach (var pair in premade)
            {
                TagState t = waterContainer.GetTagReadonly(pair.Key);
                int nv = (t == null ? 0 : t.valueInt) + (int)pair.Value;
                string key = pair.Key;
                waterContainer.ModifyTag(key, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(
                    (System.Action<TagState>)(t => t.valueInt = nv)));
            }

            // 12. 水的表现层
            if (initWaterFeature)
                WaterFeatureHelper.InitWaterFeature(waterContainer, false);
        }
    }

    // 拦截原版 AddWater → 跑我们的复制版（当前行为保持原样）
    [HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.AddWater))]
    [HarmonyWrapSafe]
    internal static class AddWaterHook
    {
        private static bool Prefix([HarmonyArgument(0)] GameItem waterContainer,
                                   [HarmonyArgument(1)] int grade,
                                   [HarmonyArgument(2)] int addAmount,
                                   [HarmonyArgument(3)] bool useRange,
                                   [HarmonyArgument(4)] int lowPara,
                                   [HarmonyArgument(5)] int highPara,
                                   [HarmonyArgument(6)] bool initWaterFeature)
        {
            AddWaterCopy.Run(waterContainer, grade, addAmount, useRange, lowPara, highPara, initWaterFeature);
            return false;
        }
    }
}
