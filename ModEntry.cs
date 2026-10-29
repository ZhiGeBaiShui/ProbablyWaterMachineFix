namespace ProbablyWaterMachineFix
{
    // 水分收集器水量修复（补丁 mod）。
    //
    // 补丁类按「大类/小类/类名」分层放在 Fixes/ 下；游戏加载 mod 时会自动扫描并应用
    // 程序集里所有 [HarmonyPatch] 类，这里只做入口：
    //   · Fixes/Water/MoistureFarmOutputVolumeFix    —— 修复①：GetOutputVolume 的 32 位溢出
    //   · Fixes/Water/MoistureFarmOutputContainerFix —— 修复②：Fill 的中转容器容量对齐目标容器
    public class ModEntry : IMod
    {
        public void Init(ModManifest manifest)
        {
            new ModLog(manifest).Log("已加载");
        }

        public void OnEnable() { }

        public void OnDisable() { }
    }
}
