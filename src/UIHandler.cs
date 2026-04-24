using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using HarmonyLib;

namespace TangledeepAccess
{
    public class UIHandler
    {
        public static string LastFocusedText = "";
        public static string LastDialogText = "";
        public static ISelectableUIObject LastFocusedData = null;
        public static UIManagerScript.UIObject LastFocusedObject = null;
        public static int LastSaveSlotIndex = -1;
        public static int LastConfirmOption = -1;

        public void Update() { }

        public static string GetSaveSlotAnnouncement(SaveDataDisplayBlock sddb)
        {
            if (sddb == null) return Loc.Get("save_slot_unknown");

            if (sddb.displayType == SaveDataDisplayBlock.ESaveDataDisplayType.empty_af)
            {
                return Loc.Get("save_slot_empty", sddb.slotIndex + 1);
            }

            string name = sddb.saveInfo.strHeroName;
            int level = sddb.saveInfo.iHeroLevel;
            string job = sddb.saveInfo.strJobName;
            string location = sddb.saveInfo.strLocation;
            string time = sddb.saveInfo.strTimePlayed;

            if (string.IsNullOrEmpty(name))
            {
                return Loc.Get("save_slot_empty", sddb.slotIndex + 1);
            }

            return Loc.Get("save_slot_info", sddb.slotIndex + 1, name, level, job, location, time);
        }

        public static void AnnounceFocusedItemDetails()
        {
            if (LastFocusedData == null) return;
            string info = LastFocusedData.GetInformationForTooltip();
            if (string.IsNullOrEmpty(info)) return;
            ScreenReader.Say(CleanText(info));
        }

        public static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            // Resolve ^tag1^ etc. merge tags before stripping markup
            if (input.Contains("^"))
            {
                input = CustomAlgorithms.ParseLiveMergeTags(input);
            }
            // Strip all XML/HTML/Unity rich text and TMP tags: <color>, <#hex>, <size>, <sprite>, </color>, etc.
            string result = Regex.Replace(input, @"</?[^>]*>", "");
            // Strip stray hex color codes like #fffb00 that may remain outside tags
            result = Regex.Replace(result, @"#[0-9a-fA-F]{6,8}\b", "");
            // Remove orphaned "color" word left from broken/partial tag stripping
            result = Regex.Replace(result, @"\bcolor\b", "", RegexOptions.IgnoreCase);
            // Collapse multiple spaces and trim
            result = Regex.Replace(result, @"\s{2,}", " ");
            return result.Trim();
        }

        /// <summary>
        /// Returns a clean item name with rarity prefix (e.g. "Magical Iron Sword").
        /// Uses the item's actual rarity enum, not color codes.
        /// </summary>
        public static string GetItemNameWithRarity(Item item)
        {
            if (item == null) return "";
            string name = CleanText(item.displayName);
            string rarityPrefix = GetRarityName(item.rarity);
            if (!string.IsNullOrEmpty(rarityPrefix))
                return rarityPrefix + " " + name;
            return name;
        }

        private static string GetRarityName(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.UNCOMMON: return Loc.Get("rarity_uncommon");
                case Rarity.MAGICAL: return Loc.Get("rarity_magical");
                case Rarity.ANCIENT: return Loc.Get("rarity_ancient");
                case Rarity.ARTIFACT: return Loc.Get("rarity_artifact");
                case Rarity.LEGENDARY: return Loc.Get("rarity_legendary");
                case Rarity.GEARSET: return Loc.Get("rarity_gearset");
                default: return "";
            }
        }

        public static string GetTextFromUIObject(UIManagerScript.UIObject obj)
        {
            if (obj == null || obj.gameObj == null) return null;
            LastFocusedData = null;

            // 1. Inventory Items
            var invBtn = obj.gameObj.GetComponent<Switch_InvItemButton>();
            if (invBtn != null)
            {
                var data = invBtn.GetContainedData();
                if (data != null)
                {
                    LastFocusedData = data;
                    string name = (data is Item dataItem) ? GetItemNameWithRarity(dataItem) : CleanText(data.GetNameForUI());
                    if (data is Equipment eq)
                    {
                        string deltas = EquipmentComparer.GetComparisonString(eq);
                        if (!string.IsNullOrEmpty(deltas)) name += ". " + deltas;
                    }
                    return name;
                }
            }

            // 2. Shop Items
            if (obj.gameObj.name.StartsWith("Shop Item Button"))
            {
                var eqScript = obj.gameObj.GetComponent<EQItemButtonScript>();
                if (eqScript != null)
                {
                    int id = eqScript.myID + UIManagerScript.listArrayIndexOffset;
                    Item item = null;
                    if (ShopUIScript.shopState == ShopState.BUY)
                    {
                        var npc = UIManagerScript.currentConversation?.whichNPC;
                        if (npc != null)
                        {
                            var items = npc.myInventory.GetInventory();
                            if (items != null && id >= 0 && id < items.Count) item = items[id];
                        }
                    }
                    else if (ShopUIScript.shopState == ShopState.SELL)
                    {
                        if (ShopUIScript.playerItemList != null && id >= 0 && id < ShopUIScript.playerItemList.Count) item = ShopUIScript.playerItemList[id];
                    }

                    if (item != null)
                    {
                        LastFocusedData = item;
                        string itemName = GetItemNameWithRarity(item);
                        // Add price info
                        if (ShopUIScript.shopState == ShopState.BUY)
                            itemName += ", " + Loc.Get("shop_price", item.GetIndividualShopPrice());
                        else if (ShopUIScript.shopState == ShopState.SELL)
                            itemName += ", " + Loc.Get("shop_sell_price", item.GetIndividualSalePrice());

                        if (item is Equipment eq)
                        {
                            string deltas = EquipmentComparer.GetComparisonString(eq);
                            if (!string.IsNullOrEmpty(deltas)) itemName += ". " + deltas;
                        }
                        return itemName;
                    }
                }
            }

            // 3. Job Buttons (Refactored)
            if (obj.gameObj.name.Contains("Job") && obj.gameObj.name.Contains("Image"))
            {
                int jobIdx = ExtractNumber(obj.gameObj.name) - 1;
                var jobOrder = (int[])AccessTools.Field(typeof(CharCreation), "jobEnumOrder").GetValue(null);
                if (jobOrder != null && jobIdx >= 0 && jobIdx < jobOrder.Length)
                {
                    var jobData = CharacterJobData.GetJobDataByEnum(jobOrder[jobIdx]);
                    if (jobData != null) return jobData.DisplayName;
                }
            }

            if (obj.gameObj.name == "CreateCharacter")
            {
                return StringManager.GetString("ui_btn_selectjob");
            }

            // 3. Cooking Ingredients
            if (obj.gameObj.name.StartsWith("Ingredient") || obj.gameObj.name.StartsWith("Seasoning"))
            {
                if (obj.onSelectValue >= 0)
                {
                    Item cookItem = null;
                    if (obj.onSelectValue >= 100)
                    {
                        int idx = obj.onSelectValue - 100;
                        if (UIManagerScript.cookingPlayerSeasoningList != null && idx >= 0 && idx < UIManagerScript.cookingPlayerSeasoningList.Length)
                            cookItem = UIManagerScript.cookingPlayerSeasoningList[idx];
                    }
                    else
                    {
                        int idx = obj.onSelectValue;
                        if (UIManagerScript.cookingPlayerIngredientList != null && idx >= 0 && idx < UIManagerScript.cookingPlayerIngredientList.Length)
                            cookItem = UIManagerScript.cookingPlayerIngredientList[idx];
                    }
                    if (cookItem != null)
                    {
                        LastFocusedData = cookItem;
                        return CleanText(cookItem.displayName);
                    }
                }
            }

            // 4. Options Menu: Sliders and Toggles
            if (UIManagerScript.GetWindowState(UITabs.OPTIONS))
            {
                string optionText = GetOptionsItemText(obj);
                if (optionText != null) return optionText;
            }

            // 5. Dialog / Feat Buttons
            var dbs = obj.gameObj.GetComponent<DialogButtonScript>();
            if (dbs != null)
            {
                string text = (dbs.headerText != null ? dbs.headerText.text + ". " : "") +
                              (dbs.bodyText != null ? dbs.bodyText.text : "");
                if (obj.button != null && obj.button.toggled) text = Loc.Get("ui_checked") + " " + text;
                string cleanedText = CleanText(text);

                return cleanedText;
            }

            // 6. Default Text components
            if (obj.subObjectTMPro != null) return CleanText(obj.subObjectTMPro.text);
            var tmp = obj.gameObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) return CleanText(tmp.text);

            return obj.gameObj.name;
        }

        /// <summary>
        /// Reads an options menu item: gets the label text plus the current Slider value or Toggle state.
        /// </summary>
        public static string GetOptionsItemText(UIManagerScript.UIObject obj)
        {
            if (obj == null || obj.gameObj == null) return null;

            // Get the option label from TMPro text on the GameObject or its parent
            string label = null;
            var labelTmp = obj.gameObj.GetComponentInChildren<TextMeshProUGUI>();
            if (labelTmp != null)
                label = CleanText(labelTmp.text);
            if (string.IsNullOrEmpty(label))
                label = obj.gameObj.name;

            // Check for Slider
            var slider = obj.gameObj.GetComponent<Slider>();
            if (slider != null)
            {
                int val = Mathf.RoundToInt(slider.value);
                int max = Mathf.RoundToInt(slider.maxValue);
                return label + ": " + val + " " + Loc.Get("option_of") + " " + max + ". " + Loc.Get("option_slider_hint");
            }

            // Check for Toggle
            var toggle = obj.gameObj.GetComponent<Toggle>();
            if (toggle != null)
            {
                string state = toggle.isOn ? Loc.Get("option_on") : Loc.Get("option_off");
                return label + ": " + state;
            }

            return null;
        }

        /// <summary>
        /// Announces the current slider value when adjusting with keyboard.
        /// </summary>
        public static void AnnounceSliderValue()
        {
            var focus = UIManagerScript.uiObjectFocus;
            if (focus == null || focus.gameObj == null) return;

            var slider = focus.gameObj.GetComponent<Slider>();
            if (slider == null) return;

            ScreenReader.Say(Mathf.RoundToInt(slider.value).ToString());
        }

        private static int ExtractNumber(string text)
        {
            string num = Regex.Match(text, @"\d+").Value;
            return int.TryParse(num, out int result) ? result : 0;
        }
    }
}
