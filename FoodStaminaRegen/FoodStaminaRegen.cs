using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace FoodStaminaRegen
{
	[BepInPlugin("org.bepinex.plugins.foodstaminaregen", "Stamina Regeneration from Food", "1.3")]
	public class FoodStaminaRegen : BaseUnityPlugin
	{
		private static ConfigEntry<bool> isEnabled;

		private void Awake()
		{
			isEnabled = Config.Bind("General", "Enabled", true, "If the mod is enabled.");

			mod = this;

			Harmony harmony = new Harmony("org.bepinex.plugins.foodstaminaregen");
			harmony.PatchAll();
		}

		private static FoodStaminaRegen mod;
		private static Dictionary<string, ConfigEntry<float>> staminaRegen = new Dictionary<string, ConfigEntry<float>>();

		[HarmonyPatch(typeof(ObjectDB), "Awake")]
		private class ReadFoodConfigs
		{
			[HarmonyPriority(Priority.Last)]
			private static void Postfix()
			{
				List<ItemDrop.ItemData.SharedData> items = ObjectDB.instance.m_items.Select(i => i.GetComponent<ItemDrop>().m_itemData.m_shared).ToList();
				staminaRegen = items.Where(item => item.m_itemType == ItemDrop.ItemData.ItemType.Consumable && item.m_foodStamina > 0)
					.ToDictionary(item => item.m_name, item => mod.Config.Bind("Food", Localization.instance.Localize(item.m_name).Replace("'", "").Replace("\"", ""), item.m_foodStamina * 0.02f));
			}
		}

		[HarmonyPatch(typeof(Player), "UpdateFood")]
		private class FoodUpdatePatch
		{
			private static float? basisStaminaRegen;

			private static void Prefix(Player __instance)
			{
				if (isEnabled.Value)
				{
					if (basisStaminaRegen == null)
					{
						basisStaminaRegen = __instance.m_staminaRegen;
					}

					float totalStaminaRegen = (float) basisStaminaRegen;

					List<Player.Food> foods = (List<Player.Food>) AccessTools.DeclaredField(typeof(Player), "m_foods").GetValue(__instance);
					foreach (Player.Food food in foods)
					{
						if (staminaRegen.TryGetValue(food.m_item.m_shared.m_name, out ConfigEntry<float> regen))
						{
							totalStaminaRegen += regen.Value;
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
				if (isEnabled.Value && staminaRegen.TryGetValue(item.m_shared.m_name, out ConfigEntry<float> regen))
				{
					__result += "\nEndurance: <color=orange>" + regen.Value * 10 + "%</color>";
				}
			}
		}
	}
}