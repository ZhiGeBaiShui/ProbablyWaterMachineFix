using System;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(ProbablyWaterMachineFix.Core), "ProbablyWaterMachineFix", "1.0.0", "ZhiGeBaiShui")]
[assembly: MelonGame]

namespace ProbablyWaterMachineFix
{
    // 水分收集器水量修复（补丁 mod）。
    //
    // 各修复按「大类/小类/类名」分层放在 Fixes/ 下维护，本文件只做入口：
    //   · Fixes/Water/MoistureFarmOutputVolumeFix    —— 修复①：GetOutputVolume 的 32 位溢出（≈21.47 L 归零）
    //   · Fixes/Water/MoistureFarmOutputContainerFix —— 修复②：Fill 的临时容器改用目标容器的 id（容量一致）
    internal class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            try
            {
                // 必须写全名 HarmonyLib.Harmony —— MelonMod 基类上有个同名的 Harmony 实例属性
                // （已标记 Obsolete）会把类型名遮住，简写成 Harmony. 会被解析成那个属性。
                HarmonyLib.Harmony.CreateAndPatchAll(typeof(Core).Assembly);
                MelonLogger.Msg("[INIT] 初始化完成");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("初始化失败: " + ex);
            }
        }
    }
}
