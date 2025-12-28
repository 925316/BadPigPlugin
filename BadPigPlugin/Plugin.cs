using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
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

[HarmonyPatch(typeof(IapManager), "GiveLootCrate")]
public static class IapManager_GiveLootCrate_Patch
{
    [HarmonyPrefix]
    public static void Prefix(LootCrateType crateType, int amount, string price, string gainType)
    {
        Debug.Log($"[GiveLootCrate] Give loot. Type: {crateType}, amount: {amount}, price: {price}, gainType: {gainType}");
    }
}

[HarmonyPatch(typeof(Shop), "SetPriceIndicator")]
public static class Shop_SetPriceIndicator_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(PurchaseInfo infoData, ref string formattedPrice, ref string formattedSalePrice, Shop __instance)
    {
        var itemType = infoData.purchaseItem.ToString();

        //Debug.Log($"[Shop] SetPriceIndicator ENTERING: {itemType}, originalPrice: {formattedPrice}, originalSalePrice: {formattedSalePrice}");

        if (string.IsNullOrEmpty(formattedPrice))
        {
            formattedPrice = __instance.GetFormattedPrice(infoData.purchaseItem);
            //Debug.Log($"[Shop] SetPriceIndicator FETCHED: {itemType} -> {formattedPrice}");
        }

        if (infoData.isSaleItem && string.IsNullOrEmpty(formattedSalePrice) && !string.IsNullOrEmpty(formattedPrice))
        {
            var match = System.Text.RegularExpressions.Regex.Match(formattedPrice, @"(\d+)");
            if (match.Success && int.TryParse(match.Value, out int priceValue))
            {
                int salePrice = (int)(priceValue * 0.7f);
                formattedSalePrice = formattedPrice.Replace(priceValue.ToString(), salePrice.ToString());
                //Debug.Log($"[Shop] SetPriceIndicator SALE: {itemType} -> {formattedSalePrice}");
            }
        }

        //Debug.Log($"[Shop] SetPriceIndicator FINAL: {itemType}, formattedPrice: {formattedPrice}, formattedSalePrice: {formattedSalePrice}");
        return true;
    }
}

[HarmonyPatch(typeof(Shop), "UpdatePrices")]
public static class Shop_UpdatePrices_Patch
{
    [HarmonyPostfix]
    public static void Postfix(IapManager.CurrencyType updateType, bool updateAll, Shop __instance)
    {
        foreach (PurchaseInfo purchaseInfo in __instance.gameObject.GetComponentsInChildren<PurchaseInfo>())
        {
            string price = __instance.GetFormattedPrice(purchaseInfo.purchaseItem);
            string salePrice = purchaseInfo.isSaleItem ? __instance.GetFormattedPrice(purchaseInfo.saleItem) : null;

            if (string.IsNullOrEmpty(price))
            {
                Debug.LogWarning($"[Shop] Empty price for: {purchaseInfo.purchaseItem}");
            }
        }
    }
}

[HarmonyPatch(typeof(Shop), "GetFormattedPrice")]
public static class Shop_GetFormattedPrice_Patch
{
    [HarmonyPostfix]
    public static void Postfix(IapManager.InAppPurchaseItemType purchaseType, ref string __result)
    {
        string itemType = purchaseType.ToString();
        var defaultPrices = new Dictionary<string, string>
        {
            { "WoodenLootCrate", "[snout] 160" },
            { "MetalLootCrate", "[snout] 300" },
            { "GoldenLootCrate", "[snout] 500" },
            { "SuperGlueSmall", "[snout] 200" },
            { "SuperGlueMedium", "[snout] 450" },
            { "SuperGlueLarge", "[snout] 1500" },
            { "TurboChargeSmall", "[snout] 200" },
            { "TurboChargeMedium", "[snout] 450" },
            { "TurboChargeLarge", "[snout] 1500" },
            { "SuperMagnetSmall", "[snout] 200" },
            { "SuperMagnetMedium", "[snout] 450" },
            { "SuperMagnetLarge", "[snout] 1500" },
            { "BlueprintSmall", "[snout] 200" },
            { "BlueprintMedium", "[snout] 450" },
            { "BlueprintLarge", "[snout] 1500" },
            { "NightVisionSmall", "[snout] 200" },
            { "NightVisionMedium", "[snout] 450" },
            { "NightVisionLarge", "[snout] 1500" }
        };

        if (defaultPrices.ContainsKey(itemType))
        {
            __result = defaultPrices[itemType];
            //Debug.Log($"[Shop] GetFormattedPrice : {itemType} -> {__result}");
        }
        else if (itemType.Contains("SnoutCoinPack"))
        {
            if (itemType.Contains("Ultimate")) __result = "$9.99";
            else if (itemType.Contains("Huge")) __result = "$4.99";
            else if (itemType.Contains("Large")) __result = "$2.99";
            else if (itemType.Contains("Medium")) __result = "$1.99";
            else if (itemType.Contains("Small")) __result = "$0.99";
        }
        else if (itemType == "UnlockSpecialSandbox")
        {
            __result = "[snout] 100";
        }
    }
}