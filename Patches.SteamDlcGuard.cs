using System;
using Cysharp.Threading.Tasks;
using Eremite.Services;
using HarmonyLib;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ 启动兼容补丁：Steam DLC 校验失败不再"降级"存档 ============
        // 现象："加载错误 / 定居点无法加载" 弹窗，随机出现，重装 / 校验游戏文件都无效。
        // 实测链路（Player.log 逐行证实）：
        //   1) [Steamworks.NET] SteamAPI_Init() failed
        //   2) SteamService.VerifyDLC -> InvalidOperationException: Steamworks is not initialized
        //   3) BasePlatformService.VerifyDLCs 抛异常，DLC 列表被视为全部缺失
        //   4) DLCsService.IsDLCDowngrade() == true -> "[DLC] DLC downgrade detected!"
        //   5) DLCsService.MarkSavesAsOld(true) 把 SavesSupportState.worldSupportCode 写成 "N/A"
        //   6) 主场景重载，SavesSupportService 重新 OnLoading 时状态已是 "N/A"
        //   7) SavesSupportService.ValidateWorld(): "N/A" != "W_22_6_2023" -> "World version incompatible!"
        //   8) WorldSaveSupportPopup 弹出（就是玩家看到的"定居点无法加载"），点"新定居点"永远转圈
        // 也就是说：这不是存档坏了，而是 Steam 未初始化时游戏自己把存档标记成了旧版本。
        //
        // 修法：Steam DLC 查询抛异常时按"已安装"处理，DLC 降级流程根本不启动，
        // 存档码保持正确的 W_22_6_2023，弹窗链路在第一步就被切断。
        // Steam 正常时该分支不会被走到，不影响真实 DLC 降级提示。

        private static bool _steamDlcVerifyFailed;

        [HarmonyPatch(typeof(SteamService), "VerifyDLC")]
        [HarmonyFinalizer]
        private static Exception GuardVerifyDLC(Exception __exception, ref UniTask<bool> __result)
        {
            if (__exception == null) return null;

            _steamDlcVerifyFailed = true;
            try { __result = UniTask.FromResult(true); } catch { }
            FileLog($"FC STEAM-GUARD: VerifyDLC 抛异常({__exception.GetType().Name})，按已安装处理");
            return null;
        }

        [HarmonyPatch(typeof(DLCsService), "IsDLCDowngrade")]
        [HarmonyPrefix]
        private static bool GuardIsDlcDowngrade(ref bool __result)
        {
            if (!_steamDlcVerifyFailed) return true;

            __result = false;
            FileLog("FC STEAM-GUARD: IsDLCDowngrade 已抑制（Steam 未初始化）");
            return false;
        }

        [HarmonyPatch(typeof(DLCsService), "MarkSavesAsOld")]
        [HarmonyPrefix]
        private static bool GuardMarkSavesAsOld()
        {
            if (!_steamDlcVerifyFailed) return true;

            FileLog("FC STEAM-GUARD: MarkSavesAsOld 已跳过（Steam 未初始化）");
            return false;
        }
    }
}
