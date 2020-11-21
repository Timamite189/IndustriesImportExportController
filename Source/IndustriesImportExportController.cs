using CitiesHarmony.API;
using HarmonyLib;
using ICities;
using UnityEngine;
using ColossalFramework.Plugins;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Reflection;
using static TransferManager;
using System.IO;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml;

namespace IndustriesImportExportController {
    public class IndustriesImportExportController : IUserMod {

        public string Name => "Industries Import Export Controller";
        public string Description => "Control the Importing and Exporting of goods and resources to and from your Industry Areas";
        
        private static readonly string SettingsFile = "IndustriesImportExcportControllerConfig.xml";

        public static readonly TransferReason[] importableResources = {
            TransferReason.Ore,
            TransferReason.Oil,
            TransferReason.Logs,
            TransferReason.Grain
        };

        public static readonly TransferReason[] exportableResources = {
            TransferReason.Ore,
            TransferReason.Oil,
            TransferReason.Logs,
            TransferReason.Grain,
            TransferReason.LuxuryProducts
        };

        public static IndustriesImportExportController instance { get; private set; }

        public bool aiAddingConnections = false;
        public Config config;

        public IndustriesImportExportController() {
            instance = this;
        }

        public void OnEnabled() {
            LoadConfig();
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled() {
            if (HarmonyHelper.IsHarmonyInstalled)
                Patcher.UnpatchAll();
        }

        public void OnSettingsUI(UIHelperBase helper) {
            var importGroup = helper.AddGroup("Disable importing of materials");
            foreach (var resource in importableResources)
                importGroup.AddCheckbox(string.Format("Allow import of {0}", resource.ToString()), config.ImportConfig[resource], isChecked => { config.ImportConfig[resource] = isChecked; SaveConfig(); });

            var exportGroup = helper.AddGroup("Disable exporting of materials");
            foreach (var resource in exportableResources)
                exportGroup.AddCheckbox(string.Format("Allow export of {0}", resource.ToString()), config.ExportConfig[resource], isChecked => { config.ExportConfig[resource] = isChecked; SaveConfig(); });
        }

        private void LoadConfig() {
            if (File.Exists(SettingsFile)) {
                XmlSerializer ser = new XmlSerializer(typeof(Config));
                TextReader reader = new StreamReader(SettingsFile);
                try {
                    config = (Config)ser.Deserialize(reader);
                } catch (Exception) {
                    config = Config.CreateInstance();
                } finally {
                    if (reader != null)
                        reader.Dispose();
                }
            } else {
                config = Config.CreateInstance();
            }
        }

        private void SaveConfig() {
            XmlSerializer ser = new XmlSerializer(typeof(Config));
            TextWriter writer = new StreamWriter(SettingsFile);
            ser.Serialize(writer, config);
            writer.Close();
        }
    }

    public class Config : IXmlSerializable {

        public Dictionary<TransferReason, bool> ImportConfig = new Dictionary<TransferReason, bool>(IndustriesImportExportController.importableResources.Length);
        public Dictionary<TransferReason, bool> ExportConfig = new Dictionary<TransferReason, bool>(IndustriesImportExportController.exportableResources.Length);

        public static Config CreateInstance() {
            Config conf = new Config();
            FillMissingResources(conf.ImportConfig, IndustriesImportExportController.importableResources);
            FillMissingResources(conf.ExportConfig, IndustriesImportExportController.exportableResources);
            return conf;
        }

        private static void FillMissingResources(Dictionary<TransferReason, bool> dict, TransferReason[] resources) {
            foreach (var resource in resources) {
                if (!dict.ContainsKey(resource))
                    dict[resource] = true;
            }
        }

        public XmlSchema GetSchema() {
            return null;
        }

        public void ReadXml(XmlReader reader) {
            reader.ReadToDescendant("Import");
            ReadValues(reader.ReadSubtree(), ImportConfig);
            reader.ReadToNextSibling("Export");
            ReadValues(reader.ReadSubtree(), ExportConfig);
            reader.Skip();
            reader.ReadEndElement();

            FillMissingResources(ImportConfig, IndustriesImportExportController.importableResources);
            FillMissingResources(ExportConfig, IndustriesImportExportController.exportableResources);
        }

        private static void ReadValues(XmlReader reader, Dictionary<TransferReason, bool> dict) {
            reader.ReadStartElement();
            while (reader.NodeType != XmlNodeType.EndElement) {
                TransferReason material = (TransferReason)Enum.Parse(typeof(TransferReason), reader.Name, true);
                dict[material] = reader.ReadElementContentAsBoolean();
            }
        }

        public void WriteXml(XmlWriter writer) {
            writer.WriteStartElement("Import");
            foreach (var kp in ImportConfig)
                writer.WriteElementString(kp.Key.ToString(), kp.Value.ToString().ToLowerInvariant());
            writer.WriteEndElement();
            writer.WriteStartElement("Export");
            foreach (var kp in ExportConfig)
                writer.WriteElementString(kp.Key.ToString(), kp.Value.ToString().ToLowerInvariant());
            writer.WriteEndElement();
        }
    }

    public class Patcher {
        private const string HarmonyId = "timamite189.iiec";
        private static bool patched = false;

        public static void PatchAll() {
            if (patched) return;
            patched = true;

            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void UnpatchAll() {
            if (!patched) return;

            var harmony = new Harmony(HarmonyId);
            harmony.UnpatchAll(HarmonyId);

            patched = false;
        }
    }

    [HarmonyPatch(typeof(TransferManager), "AddIncomingOffer", new Type[] { typeof(TransferReason), typeof(TransferOffer) })]
    public class TransferManagerAddIncomingOfferPatch {
        public static bool Prefix(TransferReason material, TransferOffer offer) {
            IndustriesImportExportController controller = IndustriesImportExportController.instance;
            bool res = true;
            if (controller.aiAddingConnections) {
                if (controller.config.ExportConfig.ContainsKey(material)) {
                    res = controller.config.ExportConfig[material];
                }
            }

            if (!res) {
                Debug.Log("Blocked Import of " + material.ToString());
            }
            return res;
        }
    }

    [HarmonyPatch(typeof(TransferManager), "AddOutgoingOffer", new Type[] { typeof(TransferReason), typeof(TransferOffer) })]
    public class TransferManagerAddOutgoingOfferPatch {
        public static bool Prefix(TransferReason material, TransferOffer offer) {
            IndustriesImportExportController controller = IndustriesImportExportController.instance;
            bool res = true;
            if (controller.aiAddingConnections) {
                if (controller.config.ImportConfig.ContainsKey(material)) {
                    res = controller.config.ImportConfig[material];
                }
            }

            if (!res) {
                Debug.Log("Blocked Export of " + material.ToString());
            }
            return res;
        }
    }

    [HarmonyPatch(typeof(OutsideConnectionAI), "AddConnectionOffers")]
    public class OutsideConnectionAIAddConnectionOffersPatch {
        public static bool Prefix(ushort buildingID, ref Building data, int productionRate, int cargoCapacity, int residentCapacity, int touristFactor0, int touristFactor1, int touristFactor2, TransferReason dummyTrafficReason, int dummyTrafficFactor) {
            IndustriesImportExportController.instance.aiAddingConnections = true;
            return true;
        }

        public static void Postfix(ushort buildingID, ref Building data, int productionRate, int cargoCapacity, int residentCapacity, int touristFactor0, int touristFactor1, int touristFactor2, TransferReason dummyTrafficReason, int dummyTrafficFactor) {
            IndustriesImportExportController.instance.aiAddingConnections = false;
        }
    }
}
