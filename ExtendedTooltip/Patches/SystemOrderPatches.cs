﻿using ExtendedTooltip.Systems;
using Game;
using Game.Common;
using Game.UI.Tooltip;
using HarmonyLib;

namespace ExtendedTooltip.Patches
{
    [HarmonyPatch(typeof(SystemOrder))]
    public static class SystemOrder_Patches
    {
        [HarmonyPatch(nameof(SystemOrder.Initialize))]
        [HarmonyPostfix]
        public static void Postfix(UpdateSystem updateSystem)
        {
            updateSystem?.UpdateAt<CustomTranslationSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem?.UpdateAt<ExtendedTooltipUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem?.UpdateAt<ExtendedTempTooltipSystem>(SystemUpdatePhase.UITooltip);
            updateSystem?.UpdateAt<ExtendedTooltipSystem>(SystemUpdatePhase.UITooltip);
            updateSystem?.UpdateAt<ExtendedBulldozerTooltipSystem>(SystemUpdatePhase.UITooltip);
        }
    }
}
