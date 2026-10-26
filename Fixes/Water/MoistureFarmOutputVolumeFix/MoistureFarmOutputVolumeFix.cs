using HarmonyLib;
using Il2Cpp;

namespace ProbablyWaterMachineFix.Fixes.Water
{
    // 修复①：原版 MachineMoistureFarm.GetOutputVolume 的 32 位溢出。
    //
    // 原版实现（ISIL 实证，WaterHelper/MachineMoistureFarm 数据集）：
    //   int bonus = MachineryHelper.GetCurrentPerformanceBonus(gameItem);
    //   return Math.Max((bonus + 100) * 1_000_000 / 100, 0);
    // 其中 (bonus + 100) * 1_000_000 是 32 位乘法：
    //   bonus >= 2048 时超出 int32 → 结果变负 → 被 Math.Max 钳成 0
    //   → 表现：水分收集器产量"归零"（临界产量 21.47 L，与玩家反馈"20 L 左右"吻合）。
    //
    // 修法：改用 64 位运算的等价式 ×10,000（不再经过被放大的中间量），
    //       超出 int 上限（2147.48 L，同时就是水量存储的极限）时饱和到 int.MaxValue，
    //       而不是像原版那样回绕成负数、被 Math.Max 钳成 0。
    [HarmonyPatch(typeof(MachineMoistureFarm), nameof(MachineMoistureFarm.GetOutputVolume))]
    [HarmonyWrapSafe]
    internal static class OutputVolumeHook
    {
        private static bool Prefix([HarmonyArgument(0)] GameItem gameItem, ref int __result)
        {
            int bonus = MachineryHelper.GetCurrentPerformanceBonus(gameItem);
            long v = ((long)bonus + 100) * 10000;
            __result = v > int.MaxValue ? int.MaxValue : (int)System.Math.Max(v, 0);
            return false;
        }
    }
}
