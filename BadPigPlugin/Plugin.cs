using BepInEx;
using HarmonyLib;
using System;
using UnityEngine;

namespace BadPigPlugin;

[BepInPlugin("org.bepinex.plugins.badpiggies", "an enhanced experience for Bad Piggies", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    private Harmony _harmony;

    private void Awake()
    {
        Debug.LogWarning($"[Bad Piggies] Plugin org.bepinex.plugins.badpiggies is loaded!");

        _harmony = new Harmony("org.bepinex.plugins.badpiggies");
        _harmony.PatchAll();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}

[HarmonyPatch(typeof(GameProgress), "AddSnoutCoins")]
public static class AddSnoutCoins_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ref int count)
    {
        int original = count;
        count *= 100;
        Debug.LogWarning($"[Bad Piggies] SnoutCoins: {original} -> {count}");
        return true;
    }
}

[HarmonyPatch(typeof(GameProgress), "AddDesserts")]
public static class AddDesserts_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(string dessertName, ref int count)
    {
        int original = count;
        count *= 2;
        Debug.LogWarning($"[Bad Piggies] {dessertName} Desserts: {original} -> {count}");
        return true;
    }
}

[HarmonyPatch(typeof(GameProgress), "AddScrap")]
public static class AddScrap_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ref int count)
    {
        int original = count;
        count *= 2;
        Debug.LogWarning($"[Bad Piggies] Scrap: {original} -> {count}");
        return true;
    }
}