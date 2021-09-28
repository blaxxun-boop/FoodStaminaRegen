using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using ServerSync;

namespace FoodStaminaRegen
{
	[BepInPlugin(ModGUID, ModName, ModVersion)]
	public class FoodStaminaRegen : BaseUnityPlugin
	{
		private const string ModName = "Stamina Regeneration from Food";
		private const string ModVersion = "1.5";
		private const string ModGUID = "org.bepinex.plugins.foodstaminaregen";

		private static readonly ConfigSync configSync = new(ModName) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

		private enum Toggle
		{
			On = 1,
			Off = 0
		}
		
		private static ConfigEntry<Toggle> serverConfigLocked = null!;
		private static ConfigEntry<Toggle> isEnabled = null!;
		private static ConfigEntry<float> regMultiplier = null!;

		private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
		{
			ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

			SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
			syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

			return configEntry;
		}

		private void Awake()
		{
			serverConfigLocked = config("1 - General", "Config is locked", Toggle.Off, new ConfigDescription("If on, only admins can change the configuration on a server."));
			configSync.AddLockingConfigEntry(serverConfigLocked);
			isEnabled = config("1 - General", "Enabled", Toggle.On, new ConfigDescription("If the mod is enabled."));
			regMultiplier = config("1 - General", "Multiplier for all foods", 1f, new ConfigDescription("Can be used to multiply all base regeneration values configured below."));

			mod = this;

			Harmony harmony = new(ModGUID);
			harmony.PatchAll();
		}

		private static FoodStaminaRegen mod = null!;
		private static Dictionary<string, ConfigEntry<float>> staminaRegen = new();

		[HarmonyPatch(typeof(ObjectDB), "Awake")]
		private class ReadFoodConfigs
		{
			[HarmonyPriority(Priority.Last)]
			private static void Postfix()
			{
				Localization english = new();
				english.SetupLanguage("English");

				List<ItemDrop.ItemData.SharedData> items = ObjectDB.instance.m_items.Select(i => i.GetComponent<ItemDrop>().m_itemData.m_shared).ToList();
				staminaRegen = items.Where(item => item.m_itemType == ItemDrop.ItemData.ItemType.Consumable && item.m_foodStamina > 0)
					.ToDictionary(item => item.m_name, item => mod.config("2 - Food", english.Localize(item.m_name).Replace("'", "").Replace("\"", ""), item.m_foodStamina * 0.03f, new ConfigDescription("", null, new ConfigurationManagerAttributes { DispName = Localization.instance.Localize(item.m_name).Replace("'", "").Replace("\"", "") })));
			}
		}

		[HarmonyPatch(typeof(Player), "UpdateFood")]
		private class FoodUpdatePatch
		{
			private static float? basisStaminaRegen;

			private static void Prefix(Player __instance)
			{
				if (isEnabled.Value == Toggle.On)
				{
					basisStaminaRegen ??= __instance.m_staminaRegen;

					float totalStaminaRegen = (float)basisStaminaRegen;

					foreach (Player.Food food in __instance.m_foods)
					{
						if (staminaRegen.TryGetValue(food.m_item.m_shared.m_name, out ConfigEntry<float> regen))
						{
							totalStaminaRegen += regen.Value * regMultiplier.Value;
						}
					}

					__instance.m_staminaRegen = totalStaminaRegen;
				}
			}
		}

		[HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", typeof(ItemDrop.ItemData), typeof(int), typeof(bool))]
		private class FoodDescPatch
		{
			private static void Postfix(ItemDrop.ItemData item, ref string __result)
			{
				if (isEnabled.Value == Toggle.On && staminaRegen.TryGetValue(item.m_shared.m_name, out ConfigEntry<float> regen))
				{
					__result += "\nEndurance: <color=orange>" + regen.Value * regMultiplier.Value * 10 + "%</color>";
				}
			}
		}
	}
}
