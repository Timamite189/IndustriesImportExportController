using CitiesHarmony.API;
using HarmonyLib;
using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static TransferManager;

namespace IndustriesImportExportController
{
    public class IndustriesImportExportController : IUserMod
    {
        public static bool enabled = false;
        
        public string Name => "Industries Import Export Controller";
        public string Description => "Control the Importing and Exporting of goods and resources to and from your Industry Areas";

        public static readonly TransferReason[] resources = {
            TransferReason.Ore,
            TransferReason.Oil,
            TransferReason.Logs,
            TransferReason.Grain,
            TransferReason.AnimalProducts,
            TransferReason.Flours,
            TransferReason.Paper,
            TransferReason.PlanedTimber,
            TransferReason.Petroleum,
            TransferReason.Plastics,
            TransferReason.Glass,
            TransferReason.Metals,
            TransferReason.LuxuryProducts
        };

        public static Dictionary<TransferReason, bool> ImportConfig = new Dictionary<TransferReason, bool>(resources.Length);
        public static Dictionary<TransferReason, bool> ExportConfig = new Dictionary<TransferReason, bool>(resources.Length);

        static IndustriesImportExportController()
        {
            foreach (var resource in resources)
            {
                ImportConfig[resource] = false;
                ExportConfig[resource] = false;
            }
        }

        public void OnEnabled()
        {
            enabled = true;
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled()
        {
            enabled = false;
            if (HarmonyHelper.IsHarmonyInstalled) Patcher.UnpatchAll();
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            var importGroup = helper.AddGroup("Disable importing of materials");
            foreach (var resource in resources)
                importGroup.AddCheckbox(string.Format("Disable {0} import", resource.ToString()), ImportConfig[resource], isChecked => { ImportConfig[resource] = isChecked; });

            var exportGroup = helper.AddGroup("Disable exporting of materials");
            foreach (var resource in resources)
                exportGroup.AddCheckbox(string.Format("Disable {0} export", resource.ToString()), ExportConfig[resource], isChecked => { ExportConfig[resource] = isChecked; });
        }
    }

    public class Patcher
    {
        private const string HarmonyId = "timamite189.iiec";
        private static bool patched = false;

        public static void PatchAll()
        {
            if (patched) return;
            patched = true;

            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void UnpatchAll()
        {
            if (!patched) return;

            var harmony = new Harmony(HarmonyId);
            harmony.UnpatchAll(HarmonyId);

            patched = false;
        }

        public void OnLevelLoaded(LoadMode mode)
        {
            if (IndustriesImportExportController.enabled && (mode == LoadMode.LoadGame || mode == LoadMode.LoadScenario || mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario))
            {
                // TODO: Remove
            }
        }
    }

    [HarmonyPatch(typeof(TransferManager), "AddIncomingOffer", new Type[] { typeof(TransferReason), typeof(TransferOffer) })]
    public class TransferManagerAddIncomingOfferPatch
    {
        public static bool Prefix(TransferReason material, TransferOffer offer)
        {
            return IndustriesImportExportController.resources.Contains(material) ? !IndustriesImportExportController.ImportConfig[material] : true;
        }
    }

    [HarmonyPatch(typeof(TransferManager), "AddOutgoingOffer", new Type[] { typeof(TransferReason), typeof(TransferOffer) })]
    public class TransferManagerAddOutgoingOfferPatch
    {
        public static bool Prefix(TransferReason material, TransferOffer offer)
        {
            return IndustriesImportExportController.resources.Contains(material) ? !IndustriesImportExportController.ExportConfig[material] : true;
        }
    }
}
