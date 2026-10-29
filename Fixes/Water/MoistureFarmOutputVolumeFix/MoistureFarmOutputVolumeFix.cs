using HarmonyLib;

namespace ProbablyWaterMachineFix.Fixes.Water
{
    // 修复①：原版 MachineMoistureFarm.GetOutputVolume 的 32 位乘法溢出。
    //
    // 原版实现：
    //   int bonus = MachineryHelper.GetCurrentPerformanceBonus(gameItem);
    //   return Math.Max(850000 * (100 + bonus) / 100, 0);
    // 850000 * (100 + bonus) 是 int 乘法，bonus >= 2427 时超出 int32 → 变负 → 被 Math.Max 钳成 0
    //   → 表现：产量超过 21.47 L 直接归零。
    //
    // 修法：同一个式子改用 64 位算，超出上限时饱和而不是回绕。
    //       饱和值取 2147483520 而不是 int.MaxValue：产量之后还要过 ApplySharedAir 的 float 除法，
    //       而 (float)int.MaxValue 会舍入到 2^31，(int)Math.Round 强转回来变成 int.MinValue（负数），
    //       等于又归 0。2147483520 是 float 能精确表示的最大 int32，过这一关不会回绕。
    [HarmonyPatch(typeof(MachineMoistureFarm), nameof(MachineMoistureFarm.GetOutputVolume))]
    internal static class OutputVolumeHook
    {
        private static bool Prefix([HarmonyArgument(0)] GameItem gameItem, ref int __result)
        {
            int bonus = MachineryHelper.GetCurrentPerformanceBonus(gameItem);
            long volume = 850000L * ((long)bonus + 100) / 100;
            __result = (int)System.Math.Clamp(volume, 0, 2147483520L);
            return false;
        }
    }
}
