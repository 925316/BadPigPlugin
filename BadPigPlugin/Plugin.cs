using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace BadPigPlugin;

[BepInPlugin("org.bepinex.plugins.badpiggies", "Enhanced Bad Piggies", "1.1.0.0")]
public class Plugin : BaseUnityPlugin
{
    private Harmony _harmony;

    private void Awake()
    {
        _harmony = new Harmony("org.bepinex.plugins.badpiggies");
        _harmony.PatchAll();
    }

    private void OnDestroy() => _harmony?.UnpatchSelf();
}

[HarmonyPatch(typeof(GameProgress))]
public static class Currency_Patches
{
    [HarmonyPatch("AddSnoutCoins"), HarmonyPrefix]
    public static void SnoutPrefix(ref int count) => count *= 2;

    [HarmonyPatch("AddDesserts"), HarmonyPrefix]
    public static void DessertPrefix(ref int count) => count *= 2;

    [HarmonyPatch("AddScrap"), HarmonyPrefix]
    public static void ScrapPrefix(ref int count) => count *= 2;
}

public static class ShopData
{
    public static readonly Dictionary<IapManager.InAppPurchaseItemType, int> ItemRewards = new()
    {
        { IapManager.InAppPurchaseItemType.SnoutCoinPackSmall, 700 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackSmallSale, 700 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackMedium, 1200 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackMediumSale, 1200 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackLarge, 3200 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackLargeSale, 3200 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackHuge, 5500 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackHugeSale, 5500 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackUltimate, 8800 },
        { IapManager.InAppPurchaseItemType.SnoutCoinPackUltimateSale, 8800 },
        { IapManager.InAppPurchaseItemType.StarterPack, 300 }
    };

    public static int GetPrice(string idOrEnum)
    {
        string key = idOrEnum.ToLower();
        if (key.Contains("ultimate")) return key.Contains("sale") ? 6000 : 8000;
        if (key.Contains("huge")) return key.Contains("sale") ? 3750 : 5000;
        if (key.Contains("large")) return key.Contains("sale") ? 2250 : 3000;
        if (key.Contains("medium")) return key.Contains("sale") ? 750 : 1000;
        if (key.Contains("small")) return 1;
        if (key.Contains("bundle")) return key.Contains("starter") ? 800 : 2000;

        if (key.Contains("wooden")) return 160;
        if (key.Contains("metal")) return 300;
        if (key.Contains("golden")) return 500;
        if (key.Contains("sandbox")) return 100;
        return 500;
    }
}

[HarmonyPatch(typeof(Shop))]
public static class Shop_Fixes
{
    [HarmonyPatch("SetPriceIndicator"), HarmonyPrefix]
    public static void SetPriceIndicator_Prefix(PurchaseInfo infoData, ref string formattedPrice, Shop __instance)
    {
        if (string.IsNullOrEmpty(formattedPrice))
            formattedPrice = __instance.GetFormattedPrice(infoData.purchaseItem);

        infoData.EnableCollider(true);
        var priceT = infoData.transform.Find("Price");
        if (priceT != null)
        {
            priceT.Find("LoadingIndicator")?.gameObject.SetActive(false);
            priceT.Find("NotConnectedIndicator")?.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch("GetFormattedPrice"), HarmonyPostfix]
    public static void GetFormattedPrice_Postfix(IapManager.InAppPurchaseItemType purchaseType, ref string __result)
    {
        int price = ShopData.GetPrice(purchaseType.ToString());
        __result = $"[snout] {price}";
    }
}

[HarmonyPatch(typeof(IapManager))]
public static class Iap_Patches
{
    [HarmonyPatch("SnoutCoinPurchasable"), HarmonyPostfix]
    public static void SnoutCoinPurchasable_Postfix(ref bool __result) => __result = true;

    [HarmonyPatch("GetPurchaseItemTypeCount"), HarmonyPostfix]
    public static void GetRewards_Postfix(IapManager.InAppPurchaseItemType type, ref int __result)
    {
        if (__result <= 0 && ShopData.ItemRewards.TryGetValue(type, out int bonus))
            __result = bonus;
    }
}

[HarmonyPatch(typeof(VirtualCatalogManager))]
public static class Catalog_Patches
{
    [HarmonyPatch("GetProductPrice", new Type[] { typeof(string) }), HarmonyPrefix]
    public static bool PriceStr(string id, ref int __result)
    { __result = ShopData.GetPrice(id); return false; }

    [HarmonyPatch("GetProductPrice", new Type[] { typeof(IapManager.InAppPurchaseItemType) }), HarmonyPrefix]
    public static bool PriceEnum(IapManager.InAppPurchaseItemType itemType, ref int __result)
    { __result = ShopData.GetPrice(itemType.ToString()); return false; }
}

[HarmonyPatch(typeof(IapManager), "PurchaseItem")]
public static class Purchase_Logic
{
    private static readonly MethodInfo DeliverMethod = AccessTools.Method(typeof(IapManager), "DeliverItem", new[] { typeof(IapManager.InAppPurchaseItemType) });
    private static readonly MethodInfo HandleSuccess = AccessTools.Method(typeof(IapManager), "HandlePurchaseSucceededEvent", new[] { typeof(string) });
    private static readonly MethodInfo HandleFailed = AccessTools.Method(typeof(IapManager), "HandlePurchaseFailedEvent", new[] { typeof(string) });
    private static readonly FieldInfo DataField = AccessTools.Field(typeof(GameProgress), "m_data");

    [HarmonyPrefix]
    public static bool Prefix(IapManager.InAppPurchaseItemType type, IapManager __instance)
    {
        try
        {
            int cost = ShopData.GetPrice(type.ToString());

            var m_data = DataField.GetValue(null);
            int currentCoins = (int)m_data.GetType().GetMethod("GetInt").Invoke(m_data, new object[] { "SnoutCoins", 0 });

            if (currentCoins >= cost)
            {
                if (GameProgress.UseSnoutCoins(cost))
                {
                    bool delivered = (bool)DeliverMethod.Invoke(__instance, new object[] { type });
                    if (delivered)
                    {
                        HandleSuccess.Invoke(__instance, new object[] { __instance.GetProductIdByItem(type) ?? "" });
                        PlayBuySound();
                        return false;
                    }
                }
                HandleFailed.Invoke(__instance, new object[] { "DELIVER_ERROR" });
            }
            else
            {
                HandleFailed.Invoke(__instance, new object[] { "NOT_ENOUGH_SNOUT_COINS" });
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Purchase Patch Error: {e.Message}");
            return true;
        }
    }

    private static void PlayBuySound()
    {
        if (WPFMonoBehaviour.gameData?.commonAudioCollection?.snoutCoinUse != null)
            Singleton<AudioManager>.Instance?.Play2dEffect(WPFMonoBehaviour.gameData.commonAudioCollection.snoutCoinUse);
    }
}

[HarmonyPatch(typeof(Shop), "PurchaseItem", new Type[] { typeof(string) })]
public static class Shop_Purchase_Fix
{
    [HarmonyPrefix]
    public static bool Prefix(Shop __instance, string inAppPurchaseItemTypeAsString)
    {
        if (Enum.TryParse(inAppPurchaseItemTypeAsString, out IapManager.InAppPurchaseItemType type))
        {
            AccessTools.Method(typeof(Shop), "LockScreen").Invoke(__instance, null);
            Singleton<IapManager>.Instance.PurchaseItem(type);
            return false;
        }
        return true;
    }
}