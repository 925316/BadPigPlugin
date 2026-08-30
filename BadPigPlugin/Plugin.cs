using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace BadPigPlugin;

[BepInPlugin("org.bepinex.plugins.badpiggies", "Enhanced Bad Piggies", "1.2.0.0")]
public class Plugin : BaseUnityPlugin
{
    private Harmony _harmony;

    private void Awake()
    {
        _harmony = new Harmony("org.bepinex.plugins.badpiggies");
        _harmony.PatchAll();
        StoreCatalog.Init();

        // Apply the VirtualCatalogManager price patch ONLY if that type actually
        // exists at runtime. If it does not, we must not let the attribute-based
        // patch throw at load time and disable the whole plugin.
        var vcm = AccessTools.TypeByName("VirtualCatalogManager");
        if (vcm != null)
        {
            var mStr = AccessTools.Method(vcm, "GetProductPrice", new[] { typeof(string) });
            var mEnum = AccessTools.Method(vcm, "GetProductPrice", new[] { typeof(IapManager.InAppPurchaseItemType) });
            if (mStr != null) _harmony.Patch(mStr, prefix: new HarmonyMethod(AccessTools.Method(typeof(CatalogPatch), "PriceStr")));
            if (mEnum != null) _harmony.Patch(mEnum, prefix: new HarmonyMethod(AccessTools.Method(typeof(CatalogPatch), "PriceEnum")));
            Debug.Log("[BadPigPlugin] VirtualCatalogManager price patch applied.");
        }
        else
        {
            Debug.LogWarning("[BadPigPlugin] VirtualCatalogManager not found; catalog price patch skipped (Shop.GetFormattedPrice still covers the UI).");
        }

        StoreCatalog.DumpToLog();
    }

    private void OnDestroy() => _harmony?.UnpatchSelf();
}

// ---------------------------------------------------------------------------
// Local price + reward catalog.
//
// Built ONCE at startup from the REAL InAppPurchaseItemType enum (verified by
// dumping the game IL). Runtime lookups are exact dictionary hits -- no fragile
// per-call string matching. Prices are denominated in SnoutCoins (earned by
// playing) so the shop stays a real economy, per the "keep the challenge" goal.
// ---------------------------------------------------------------------------
public static class StoreCatalog
{
    public static readonly Dictionary<IapManager.InAppPurchaseItemType, int> Price = new();
    public static readonly Dictionary<IapManager.InAppPurchaseItemType, int> CoinReward = new();

    static StoreCatalog()
    {
        foreach (IapManager.InAppPurchaseItemType t in Enum.GetValues(typeof(IapManager.InAppPurchaseItemType)))
        {
            if (t == IapManager.InAppPurchaseItemType.Undefined) { Price[t] = 0; continue; }
            string n = t.ToString();

            if (n.Contains("SnoutCoinPack"))
            {
                int reward = CoinRewardFor(n);
                CoinReward[t] = reward;
                // Baby-mode escape hatch: only the Small pack is ~free (1 coin -> 700).
                // Every other coin pack is priced 1:1 with its payout, so buying it is a
                // pointless no-op (no inflation, no exploit) and is naturally unused.
                Price[t] = n.Contains("Small") ? 1 : reward;
            }
            else if (IsPowerUp(n))
            {
                Price[t] = TierCost(n);
            }
            else if (n.Contains("LootCrate"))
            {
                Price[t] = n.Contains("Golden") ? 500 : n.Contains("Metal") ? 300 : 160;
            }
            else if (n.Contains("Bundle"))
            {
                Price[t] = n.Contains("Huge") ? 2500 : n.Contains("Big") ? 1500 : n.Contains("Medium") ? 800 : 300;
            }
            else if (n.Contains("Unlock"))
            {
                Price[t] = n.Contains("FullVersion") ? 1000 : n.Contains("SpecialSandbox") ? 500 : n.Contains("Episode") ? 400 : 200;
            }
            else if (n.Contains("PermanentBlueprint")) Price[t] = 1000;
            else if (n.Contains("StarterPack")) Price[t] = 300;
            else if (n.Contains("AddTenDesserts")) Price[t] = 100;
            else Price[t] = 200; // safe fallback for anything unforeseen
        }
    }

    private static bool IsPowerUp(string n) =>
        n.Contains("Blueprint") || n.Contains("SuperGlue") || n.Contains("SuperMagnet") ||
        n.Contains("TurboCharge") || n.Contains("NightVision");

    // Coin amount granted by a SnoutCoin pack (mirrors the original store values).
    private static int CoinRewardFor(string n) =>
        n.Contains("Ultimate") ? 8800 : n.Contains("Huge") ? 5500 : n.Contains("Large") ? 3200 :
        n.Contains("Medium") ? 1200 : 700;

    private static int TierCost(string n) =>
        n.Contains("Single") ? 20 : n.Contains("Small") ? 30 : n.Contains("Medium") ? 120 :
        n.Contains("Large") ? 250 : n.Contains("Huge") ? 500 : n.Contains("Ultimate") ? 800 : 100;

    public static void Init()
    { /* static ctor runs on first access */ }

    public static void DumpToLog()
    {
        Debug.Log($"[BadPigPlugin] StoreCatalog entries: {Price.Count}");
        foreach (var kv in Price)
            Debug.Log($"[BadPigPlugin]   {kv.Key} -> cost {kv.Value}" + (CoinReward.ContainsKey(kv.Key) ? $", reward {CoinReward[kv.Key]}" : ""));
    }
}

// Force the shop UI out of its "not connected" dead state and show our local price.
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

    [HarmonyPatch("GetFormattedPrice", new Type[] { typeof(IapManager.InAppPurchaseItemType) }), HarmonyPostfix]
    public static void GetFormattedPrice_Postfix(IapManager.InAppPurchaseItemType purchaseType, ref string __result)
    {
        if (StoreCatalog.Price.TryGetValue(purchaseType, out int price))
            __result = $"[snout] {price}";
    }
}

[HarmonyPatch(typeof(IapManager))]
public static class Iap_Patches
{
    [HarmonyPatch("SnoutCoinPurchasable"), HarmonyPostfix]
    public static void SnoutCoinPurchasable_Postfix(ref bool __result) => __result = true;

    [HarmonyPatch("GetPurchaseItemTypeCount", new Type[] { typeof(IapManager.InAppPurchaseItemType) }), HarmonyPostfix]
    public static void GetRewards_Postfix(IapManager.InAppPurchaseItemType type, ref int __result)
    {
        if (__result <= 0 && StoreCatalog.CoinReward.TryGetValue(type, out int bonus))
            __result = bonus;
        else if (__result <= 0)
            __result = 1; // non-coin items: a sensible default count
    }
}

// Manual catalog price patch (applied only if VirtualCatalogManager exists).
public static class CatalogPatch
{
    public static bool PriceStr(string id, ref int __result)
    {
        if (Enum.TryParse(id, out IapManager.InAppPurchaseItemType t) && StoreCatalog.Price.TryGetValue(t, out int p))
        { __result = p; return false; }
        return true;
    }

    public static bool PriceEnum(IapManager.InAppPurchaseItemType itemType, ref int __result)
    {
        if (StoreCatalog.Price.TryGetValue(itemType, out int p)) { __result = p; return false; }
        return true;
    }
}

// Core: intercept the real purchase entry point and settle locally with SnoutCoins.
[HarmonyPatch(typeof(IapManager), "PurchaseItem", new Type[] { typeof(IapManager.InAppPurchaseItemType) })]
public static class Purchase_Logic
{
    private static readonly MethodInfo Deliver = AccessTools.Method(typeof(IapManager), "DeliverItem", new[] { typeof(IapManager.InAppPurchaseItemType) });
    private static readonly MethodInfo OnSuccess = AccessTools.Method(typeof(IapManager), "HandlePurchaseSucceededEvent", new[] { typeof(string) });
    private static readonly MethodInfo OnFailed = AccessTools.Method(typeof(IapManager), "HandlePurchaseFailedEvent", new[] { typeof(string) });
    private static readonly FieldInfo DataField = AccessTools.Field(typeof(GameProgress), "m_data");
    private static readonly MethodInfo GetProductId = AccessTools.Method(typeof(IapManager), "GetProductIdByItem", new[] { typeof(IapManager.InAppPurchaseItemType) });

    static Purchase_Logic()
    {
        Debug.Log($"[BadPigPlugin] resolve: DeliverItem={Deliver != null}, OnSuccess={OnSuccess != null}, OnFailed={OnFailed != null}, m_data={DataField != null}, GetProductId={GetProductId != null}");
    }

    [HarmonyPrefix]
    public static bool Prefix(IapManager.InAppPurchaseItemType type, IapManager __instance)
    {
        try
        {
            if (!StoreCatalog.Price.TryGetValue(type, out int cost)) cost = int.MaxValue;
            if (cost <= 0) return false; // Undefined / non-purchasable

            if (Deliver == null)
            {
                Debug.LogError("[BadPigPlugin] DeliverItem not resolved; cannot grant item, aborting purchase (no charge).");
                SafeFail(__instance, "DELIVER_NOT_RESOLVED");
                return false;
            }

            int current = GetSnoutCoins();
            if (current < cost)
            {
                SafeFail(__instance, "NOT_ENOUGH_SNOUT_COINS");
                return false;
            }

            // Deliver FIRST, then charge -- never lose coins on a failed grant.
            bool delivered = SafeDeliver(__instance, type);
            if (!delivered)
            {
                SafeFail(__instance, "DELIVER_FAILED");
                return false;
            }

            GameProgress.UseSnoutCoins(cost);
            string pid = type.ToString();
            if (GetProductId != null) { try { pid = (string)GetProductId.Invoke(__instance, new object[] { type }); } catch { } }
            SafeSuccess(__instance, pid);
            PlayBuySound();
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BadPigPlugin] Purchase error: {e}");
            return true; // fall back to original (will likely fail offline, but logs reality)
        }
    }

    private static bool SafeDeliver(IapManager inst, IapManager.InAppPurchaseItemType type)
    {
        try
        {
            object r = Deliver.Invoke(inst, new object[] { type });
            if (r is bool b) return b;
            return true; // void-returning DeliverItem => assume success
        }
        catch (Exception e)
        {
            Debug.LogError($"[BadPigPlugin] DeliverItem threw: {e}");
            return false;
        }
    }

    private static int GetSnoutCoins()
    {
        try
        {
            if (DataField == null) return 0;
            object mdata = DataField.GetValue(null);
            var getInt = mdata.GetType().GetMethod("GetInt", new[] { typeof(string), typeof(int) });
            if (getInt == null) return 0;
            return (int)getInt.Invoke(mdata, new object[] { "SnoutCoins", 0 });
        }
        catch { return 0; }
    }

    private static void SafeSuccess(IapManager inst, string pid)
    {
        try { OnSuccess?.Invoke(inst, new object[] { pid }); }
        catch (Exception e) { Debug.LogWarning($"[BadPigPlugin] OnSuccess invoke failed (item still delivered): {e}"); }
    }

    private static void SafeFail(IapManager inst, string reason)
    {
        try { OnFailed?.Invoke(inst, new object[] { reason }); }
        catch { }
    }

    private static void PlayBuySound()
    {
        if (WPFMonoBehaviour.gameData?.commonAudioCollection?.snoutCoinUse != null)
            Singleton<AudioManager>.Instance?.Play2dEffect(WPFMonoBehaviour.gameData.commonAudioCollection.snoutCoinUse);
    }
}

// Route the shop's string-based buy button into the intercepted purchase path.
[HarmonyPatch(typeof(Shop), "PurchaseItem", new Type[] { typeof(string) })]
public static class Shop_Purchase_Fix
{
    [HarmonyPrefix]
    public static bool Prefix(Shop __instance, string inAppPurchaseItemTypeAsString)
    {
        try
        {
            if (Enum.TryParse(inAppPurchaseItemTypeAsString, out IapManager.InAppPurchaseItemType type))
            {
                try { AccessTools.Method(typeof(Shop), "LockScreen")?.Invoke(__instance, null); } catch { }
                var iap = Singleton<IapManager>.Instance;
                if (iap != null) iap.PurchaseItem(type);
                return false;
            }
        }
        catch (Exception e) { Debug.LogError($"[BadPigPlugin] Shop.PurchaseItem error: {e}"); }
        return true;
    }
}
