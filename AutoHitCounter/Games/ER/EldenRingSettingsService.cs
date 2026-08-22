// 

using System;
using AutoHitCounter.Interfaces;
using static AutoHitCounter.Games.ER.EldenRingOffsets;

namespace AutoHitCounter.Games.ER;

public class EldenRingSettingsService(IMemoryService memoryService)
{
    // Menu input delay setter (ER 1.12+). The call that loads the delay threshold is followed by
    // movss [rbx+0x18], xmm0 ; mov rax, rbx:
    //   E8 ?? ?? ?? ??  call <getter>
    //   F3 0F 11 43 18  movss [rbx+0x18], xmm0
    //   48 8B C3        mov rax, rbx
    private const string MenuInputDelayPattern = "E8 ?? ?? ?? ?? F3 0F 11 43 18 48 8B C3";

    // Overwrites the 5-byte "call <getter>" with "xorps xmm0, xmm0 ; nop ; nop", so the following
    // movss stores 0 into the threshold, disabling the delay.
    private static readonly byte[] MenuInputDelayPatch = [0x0F, 0x57, 0xC0, 0x90, 0x90];

    private nint _menuInputDelayAddr;
    private byte[] _menuInputDelayOriginalBytes;

    public void ToggleNoLogo(bool isEnabled) =>
        memoryService.WriteBytes(Patches.NoLogo, isEnabled ? [0x90, 0x90] : [0x74, 0x53]);
    
    public void ToggleStutterFix(bool isEnabled) =>
        memoryService.Write(memoryService.Read<nint>(UserInputManager.Base) + UserInputManager.SteamInputEnum, isEnabled);
    
    public void ToggleDisableAchievements(bool isEnabled)
    {
        var isAwardAchievementsEnabledFlag = memoryService.FollowPointers64(CSTrophy.Base, [
            CSTrophy.CSTrophyPlatformImp_forSteam,
            CSTrophy.IsAwardAchievementEnabled
        ], false);
        memoryService.Write(isAwardAchievementsEnabledFlag, isEnabled);
    }

    public void ToggleMenuInputDelayFix(bool isEnabled)
    {
        if (_menuInputDelayAddr == IntPtr.Zero)
        {
            // Resolved lazily via AOB scan so it stays version independent. The pattern only exists
            // on 1.12+ (the versions that introduced the delay); older builds simply no-op.
            _menuInputDelayAddr = memoryService.FindPattern(MenuInputDelayPattern);
            if (_menuInputDelayAddr == IntPtr.Zero)
                return;

            // Captured before any patch is applied so the fix can be toggled back off cleanly.
            _menuInputDelayOriginalBytes = memoryService.ReadBytes(_menuInputDelayAddr, MenuInputDelayPatch.Length);
        }

        memoryService.WriteBytes(_menuInputDelayAddr,
            isEnabled ? MenuInputDelayPatch : _menuInputDelayOriginalBytes);
    }
}