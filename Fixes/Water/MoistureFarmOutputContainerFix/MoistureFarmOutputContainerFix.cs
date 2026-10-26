using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace ProbablyWaterMachineFix.Fixes.Water
{
    // 修复②：收集器产水时，Fill 固定先 ItemSpawner.Spawn("water_jug") 造一个临时容器，
    //        把产量灌进它（AddXWater → AddWater，容量读的就是这个临时容器的 tag），
    //        最后 TransferLiquid(临时容器 → container) 转到真正的目标容器。
    //        临时容器的容量是固定的，产量会被它卡住。
    //
    // 修法：把这次新生成的中转容器容量，改成目标容器 container「当前」的容量 tag 值。
    //       · 只改一个 tag —— 容器类型 / 贴图 / 形状全部保持原版 water_jug；
    //       · AddWater 定上限时只读 LIQUID_CONTAINER_CAPACITY（ISIL 指令 163），所以只对齐这一个值就够；
    //       · 不依赖 container.identifier 能不能生成，也不管对方的容量来自物品定义还是运行时改的 tag。
    //
    // 刻意避开"在 patch 里调用被 patch 的函数"：
    //   · Fill 的 Prefix 只记下 container，并打开 bool 开关；
    //   · Spawn 的 Postfix 在开关打开时改 __result 的容量 tag —— 不重放、不重入。
    internal static class TempVesselSwitch
    {
        internal static bool Pending;     // bool 开关：只在 Fill → Spawn 这一对调用之间为 true
        internal static GameItem Target;  // Fill 的 Prefix 传过来的目标容器
    }

    // Prefix Fill：记下 container、打开开关；Postfix Fill：兜底关掉开关
    [HarmonyPatch(typeof(MachineMoistureFarm), nameof(MachineMoistureFarm.Fill))]
    [HarmonyWrapSafe]
    internal static class FillTrackHook
    {
        private static void Prefix([HarmonyArgument(1)] GameItem container)
        {
            TempVesselSwitch.Target = container;
            TempVesselSwitch.Pending = container != null;
        }

        private static void Postfix()
        {
            TempVesselSwitch.Pending = false;
            TempVesselSwitch.Target = null;
        }
    }

    // Postfix Spawn：开关打开时，把这次新生成的中转容器容量对齐到目标容器当前的 tag 值
    [HarmonyPatch(typeof(ItemSpawner), nameof(ItemSpawner.Spawn))]
    [HarmonyWrapSafe]
    internal static class SpawnCapacityHook
    {
        private static void Postfix(ref GameItem __result)
        {
            if (!TempVesselSwitch.Pending) return;   // 只有 Fill 那一次该生效

            GameItem target = TempVesselSwitch.Target;
            TempVesselSwitch.Pending = false;        // 消费即关，防止残留影响后续 Spawn
            TempVesselSwitch.Target = null;

            int value = target.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").valueInt;
            __result.ModifyTag("LIQUID_CONTAINER_CAPACITY",
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(
                    (System.Action<TagState>)(t => t.valueInt = value)));
        }
    }
}
