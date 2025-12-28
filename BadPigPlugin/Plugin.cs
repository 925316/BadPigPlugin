using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

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
        count *= 2;
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
public static class Shop_SetPriceIndicator_FixNetwork_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(PurchaseInfo infoData, ref string formattedPrice, ref string formattedSalePrice, Shop __instance)
    {
        if (string.IsNullOrEmpty(formattedPrice))
        {
            formattedPrice = __instance.GetFormattedPrice(infoData.purchaseItem);
        }

        Transform priceTransform = infoData.transform.Find("Price");
        if (priceTransform != null)
        {
            Transform loadingIndicator = priceTransform.Find("LoadingIndicator");
            if (loadingIndicator != null) loadingIndicator.gameObject.SetActive(false);

            Transform notConnectedIndicator = priceTransform.Find("NotConnectedIndicator");
            if (notConnectedIndicator != null) notConnectedIndicator.gameObject.SetActive(false);
        }

        infoData.EnableCollider(true);

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
            { "WoodenLootCrateSale", "[snout] 120" },
            { "MetalLootCrateSale", "[snout] 225" },
            { "GoldenLootCrateSale", "[snout] 375" },
            { "GoldenLootCratePack", "[snout] 1200" },
            { "UnlockSpecialSandbox", "[snout] 100" },
            { "PermanentBlueprint", "[snout] 500" },
            { "StarterPack", "[snout] 300" },
        };

        if (itemType.Contains("SnoutCoinPack"))
        {
            if (itemType.Contains("Ultimate")) __result = "[snout] 8000";
            else if (itemType.Contains("Huge")) __result = "[snout] 5000";
            else if (itemType.Contains("Large")) __result = "[snout] 3000";
            else if (itemType.Contains("Medium")) __result = "[snout] 1000";
            else if (itemType.Contains("Small")) __result = "[snout] 600";
        }

        if (itemType.Contains("Bundle"))
        {
            if (itemType.Contains("Starter")) __result = "[snout] 800";
            else if (itemType.Contains("Medium")) __result = "[snout] 1500";
            else if (itemType.Contains("Big")) __result = "[snout] 2500";
            else if (itemType.Contains("Huge")) __result = "[snout] 4000";
        }

        if (defaultPrices.ContainsKey(itemType))
        {
            __result = defaultPrices[itemType];
        }
    }
}

[HarmonyPatch(typeof(IapManager), "GetPurchaseItemTypeCount")]
public static class IapManager_GetPurchaseItemTypeCount_Patch
{
    [HarmonyPostfix]
    public static void Postfix(IapManager.InAppPurchaseItemType type, ref int __result)
    {
        if (__result == 0)
        {
            switch (type)
            {
                case IapManager.InAppPurchaseItemType.SnoutCoinPackSmall:
                case IapManager.InAppPurchaseItemType.SnoutCoinPackSmallSale:
                    __result = 700;
                    break;

                case IapManager.InAppPurchaseItemType.SnoutCoinPackMedium:
                case IapManager.InAppPurchaseItemType.SnoutCoinPackMediumSale:
                    __result = 1200;
                    break;

                case IapManager.InAppPurchaseItemType.SnoutCoinPackLarge:
                case IapManager.InAppPurchaseItemType.SnoutCoinPackLargeSale:
                    __result = 3200;
                    break;

                case IapManager.InAppPurchaseItemType.SnoutCoinPackHuge:
                case IapManager.InAppPurchaseItemType.SnoutCoinPackHugeSale:
                    __result = 5500;
                    break;

                case IapManager.InAppPurchaseItemType.SnoutCoinPackUltimate:
                case IapManager.InAppPurchaseItemType.SnoutCoinPackUltimateSale:
                    __result = 8800;
                    break;

                case IapManager.InAppPurchaseItemType.StarterPack:
                    __result = 300;
                    break;
            }
        }
    }
}

[HarmonyPatch(typeof(IapManager), "SnoutCoinPurchasable")]
public static class IapManager_SnoutCoinPurchasable_Patch
{
    [HarmonyPostfix]
    public static void Postfix(IapManager.InAppPurchaseItemType itemId, ref bool __result)
    {
        __result = true;
    }
}

[HarmonyPatch(typeof(VirtualCatalogManager), "GetProductPrice", new Type[] { typeof(string) })]
public static class VirtualCatalogManager_GetProductPrice_String_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(string id, ref int __result)
    {
        if (id.Contains("snout_pack"))
        {
            if (id.Contains("ultimate")) __result = 8000;
            else if (id.Contains("huge")) __result = 5000;
            else if (id.Contains("large")) __result = 3000;
            else if (id.Contains("medium")) __result = 1000;
            else if (id.Contains("small")) __result = 600;

            if (id.Contains("sale"))
            {
                if (id.Contains("ultimate")) __result = 6000;
                else if (id.Contains("huge")) __result = 3750;
                else if (id.Contains("large")) __result = 2250;
                else if (id.Contains("medium")) __result = 750;
                else if (id.Contains("small")) __result = 450;
            }
            return false;
        }

        if (id.Contains("wooden")) __result = 160;
        else if (id.Contains("metal")) __result = 300;
        else if (id.Contains("golden")) __result = 500;
        else if (id.Contains("sandbox")) __result = 100;
        else __result = 500;

        return false;
    }
}

[HarmonyPatch(typeof(VirtualCatalogManager), "GetProductPrice", new Type[] { typeof(IapManager.InAppPurchaseItemType) })]
public static class VirtualCatalogManager_GetProductPrice_Enum_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IapManager.InAppPurchaseItemType itemType, ref int __result)
    {
        string itemTypeString = itemType.ToString();

        if (itemTypeString.Contains("SnoutCoinPack"))
        {
            if (itemTypeString.Contains("Ultimate")) __result = 8000;
            else if (itemTypeString.Contains("Huge")) __result = 5000;
            else if (itemTypeString.Contains("Large")) __result = 3000;
            else if (itemTypeString.Contains("Medium")) __result = 1000;
            else if (itemTypeString.Contains("Small")) __result = 600;

            if (itemTypeString.Contains("Sale"))
            {
                if (itemTypeString.Contains("Ultimate")) __result = 6000;
                else if (itemTypeString.Contains("Huge")) __result = 3750;
                else if (itemTypeString.Contains("Large")) __result = 2250;
                else if (itemTypeString.Contains("Medium")) __result = 750;
                else if (itemTypeString.Contains("Small")) __result = 450;
            }
            return false;
        }

        if (itemTypeString.Contains("Bundle"))
        {
            if (itemTypeString.Contains("Starter")) __result = 800;
            else if (itemTypeString.Contains("Medium")) __result = 1500;
            else if (itemTypeString.Contains("Big")) __result = 2500;
            else if (itemTypeString.Contains("Huge")) __result = 4000;
            return false;
        }

        if (itemTypeString.Contains("WoodenLootCrate")) __result = 160;
        else if (itemTypeString.Contains("MetalLootCrate")) __result = 300;
        else if (itemTypeString.Contains("GoldenLootCrate")) __result = 500;
        else if (itemTypeString.Contains("WoodenLootCrateSale")) __result = 120;
        else if (itemTypeString.Contains("MetalLootCrateSale")) __result = 225;
        else if (itemTypeString.Contains("GoldenLootCrateSale")) __result = 375;
        else if (itemTypeString.Contains("GoldenLootCratePack")) __result = 1200;
        else if (itemTypeString.Contains("UnlockSpecialSandbox")) __result = 100;
        else if (itemTypeString.Contains("PermanentBlueprint")) __result = 500;
        else if (itemTypeString.Contains("StarterPack")) __result = 300;
        else __result = 500;

        return false;
    }
}

[HarmonyPatch(typeof(IapManager), "PurchaseItem")]
public static class IapManager_PurchaseItem_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IapManager.InAppPurchaseItemType type, IapManager __instance)
    {
        try
        {
            int productPrice = -1;
            var virtualCatalog = Singleton<VirtualCatalogManager>.Instance;
            if (virtualCatalog != null)
            {
                productPrice = virtualCatalog.GetProductPrice(type);
            }

            if (productPrice > 0)
            {
                var gameProgressType = typeof(GameProgress);
                var m_dataField = gameProgressType.GetField("m_data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (m_dataField != null)
                {
                    var m_data = m_dataField.GetValue(null);
                    var dataType = m_data.GetType();
                    var getIntMethod = dataType.GetMethod("GetInt");

                    int currentCoins = 0;
                    if (getIntMethod != null)
                    {
                        currentCoins = (int)getIntMethod.Invoke(m_data, new object[] { "SnoutCoins", 0 });
                    }

                    if (currentCoins >= productPrice)
                    {
                        var useSnoutCoinsMethod = gameProgressType.GetMethod("UseSnoutCoins", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                        if (useSnoutCoinsMethod != null)
                        {
                            bool success = (bool)useSnoutCoinsMethod.Invoke(null, new object[] { productPrice });

                            if (success)
                            {
                                MethodInfo deliverMethod = AccessTools.Method(typeof(IapManager), "DeliverItem",
                                    new Type[] { typeof(IapManager.InAppPurchaseItemType) });
                                bool deliverSuccess = (bool)deliverMethod.Invoke(__instance, new object[] { type });

                                if (deliverSuccess)
                                {
                                    string productId = __instance.GetProductIdByItem(type);
                                    if (!string.IsNullOrEmpty(productId))
                                    {
                                        MethodInfo handleSuccessMethod = AccessTools.Method(typeof(IapManager), "HandlePurchaseSucceededEvent",
                                            new Type[] { typeof(string) });
                                        handleSuccessMethod.Invoke(__instance, new object[] { productId });
                                    }
                                    else
                                    {
                                        MethodInfo handleSuccessMethod = AccessTools.Method(typeof(IapManager), "HandlePurchaseSucceededEvent",
                                            new Type[] { typeof(string) });
                                        handleSuccessMethod.Invoke(__instance, new object[] { "" });
                                    }

                                    if (WPFMonoBehaviour.gameData != null && WPFMonoBehaviour.gameData.commonAudioCollection != null)
                                    {
                                        AudioManager audioManager = Singleton<AudioManager>.Instance;
                                        if (audioManager != null)
                                        {
                                            audioManager.Play2dEffect(WPFMonoBehaviour.gameData.commonAudioCollection.snoutCoinUse);
                                        }
                                    }
                                }
                                else
                                {
                                    MethodInfo handleFailedMethod = AccessTools.Method(typeof(IapManager), "HandlePurchaseFailedEvent",
                                        new Type[] { typeof(string) });
                                    handleFailedMethod.Invoke(__instance, new object[] { "DELIVER_ERROR" });
                                }
                            }
                            else
                            {
                                MethodInfo handleFailedMethod = AccessTools.Method(typeof(IapManager), "HandlePurchaseFailedEvent",
                                    new Type[] { typeof(string) });
                                handleFailedMethod.Invoke(__instance, new object[] { "DELIVER_ERROR" });
                            }
                        }
                        else
                        {
                            Debug.LogError("[BadPiggies] Could not find UseSnoutCoins method!");
                        }
                    }
                    else
                    {
                        MethodInfo handleFailedMethod = AccessTools.Method(typeof(IapManager), "HandlePurchaseFailedEvent",
                            new Type[] { typeof(string) });
                        handleFailedMethod.Invoke(__instance, new object[] { "NOT_ENOUGH_SNOUT_COINS" });
                    }
                }
                else
                {
                    Debug.LogError("[BadPiggies] Could not find GameProgress.m_data field!");
                }
            }
            else
            {
                MethodInfo handleFailedMethod = AccessTools.Method(typeof(IapManager), "HandlePurchaseFailedEvent",
                    new Type[] { typeof(string) });
                handleFailedMethod.Invoke(__instance, new object[] { "INVALID_PRICE" });
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"PurchaseItem patch failed: {ex}");
            return true;
        }
    }
}

[HarmonyPatch(typeof(Shop), "PurchaseItem")]
public static class Shop_PurchaseItem_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Shop __instance, string inAppPurchaseItemTypeAsString)
    {
        try
        {
            if (!Enum.IsDefined(typeof(IapManager.InAppPurchaseItemType), inAppPurchaseItemTypeAsString))
                return false;

            var purchaseId = (IapManager.InAppPurchaseItemType)Enum.Parse(typeof(IapManager.InAppPurchaseItemType), inAppPurchaseItemTypeAsString);

            MethodInfo lockScreenMethod = AccessTools.Method(typeof(Shop), "LockScreen");
            lockScreenMethod.Invoke(__instance, null);

            Singleton<IapManager>.Instance.PurchaseItem(purchaseId);

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Shop.PurchaseItem patch failed: {ex}");
            return true;
        }
    }
}