using System;
using System.Collections.Generic;
using System.Linq;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class EditTab : BaseTab
    {
        private OpComboBox scugSelector;
        private OpComboBox regionSelector;
        private OpScrollBox roomSelector;
        private OpScrollBox roomOptions;


        public EditTab(OptionInterface owner) : base(owner, "Editor")
        {
        }

        public override void Initialize()
        {
            const float SIDE_PADDING = 10f;
            const float ITEM_GAP = 20f;
            const float MENU_SIZE = 600f;
            const float COMBOBOX_OFFSET = 4f;
            var scugList = Data.RenderedRegions.Keys.ToList();

            // Top of menu
            const float TOPBAR_UNIT_WIDTH = (MENU_SIZE - SIDE_PADDING * 2f - ITEM_GAP * 2) / 4f;
            scugSelector = new(OIUtil.CosmeticBind(""), new(SIDE_PADDING, MENU_SIZE - SIDE_PADDING - 30f + COMBOBOX_OFFSET), TOPBAR_UNIT_WIDTH, scugList.Select(x => x.value).ToArray());
            scugSelector.OnValueChanged += ScugSelector_OnValueChanged;
            regionSelector = new(OIUtil.CosmeticBind(""), new(scugSelector.pos.x + scugSelector.size.x + ITEM_GAP + COMBOBOX_OFFSET, scugSelector.pos.y), TOPBAR_UNIT_WIDTH * 2, [""]);

            // Input boxes and such
            const float BODY_LEFT_WIDTH = MENU_SIZE / 3;
            const float BODY_RIGHT_WIDTH = MENU_SIZE - BODY_LEFT_WIDTH;
            const float TOPBAR_HEIGHT = 30f + SIDE_PADDING + ITEM_GAP;
            roomSelector = new(new Vector2(SIDE_PADDING, SIDE_PADDING), new Vector2(BODY_LEFT_WIDTH - SIDE_PADDING - ITEM_GAP / 2f, MENU_SIZE - TOPBAR_HEIGHT), 0, false, true, true);
            var mapSize = BODY_RIGHT_WIDTH - SIDE_PADDING - ITEM_GAP / 2; // trying to make it a square
            roomOptions = new(new Vector2(BODY_LEFT_WIDTH + ITEM_GAP / 2, SIDE_PADDING), new Vector2(BODY_RIGHT_WIDTH - SIDE_PADDING - ITEM_GAP / 2, roomSelector.size.y - mapSize - ITEM_GAP), 0, false, true, true);
            var mapBox = new OpRect(new(roomOptions.pos.x, roomSelector.pos.y + roomSelector.size.y - mapSize), new(mapSize, mapSize));

            // Add the items
            AddItems(
                // Input boxes and such
                roomSelector,
                roomOptions,
                mapBox,

                // Place the top things last for z-index reasons
                regionSelector,
                scugSelector,
                new OpLabel(new Vector2(MENU_SIZE - SIDE_PADDING - TOPBAR_UNIT_WIDTH, MENU_SIZE - SIDE_PADDING - 30f), new(TOPBAR_UNIT_WIDTH, 30f), "EDIT MAP", FLabelAlignment.Center, true)
                // new OpLabel(590f - LabelTest.GetWidth("EDIT", true), 560f, "EDIT", true)
            );
        }

        private void ScugSelector_OnValueChanged(UIconfig config, string value, string oldValue)
        {
            const string PLACEHOLDERSTR = ":"; // impossible for region to have as its name because Windows file names can't have colons
            if (oldValue == value) return;

            var scug = new SlugcatStats.Name(value);

            // Clear old list
            var oldList = regionSelector.GetItemList();
            regionSelector.AddItems(false, new ListItem(PLACEHOLDERSTR));
            foreach (var region in oldList)
            {
                regionSelector.RemoveItems(true, region.name);
            }

            // Add new items
            if (Data.RenderedRegions.ContainsKey(scug))
            {
                var regions = Data.RenderedRegions[scug];
                foreach (var region in regions)
                {
                    regionSelector.AddItems(false, new ListItem(region, $"({region}) {Region.GetRegionFullName(region, scug)}"));
                }
            }
            else
            {
                regionSelector.AddItems(false, new ListItem("")); // there has to be at least 1 for some reason
            }

            regionSelector.RemoveItems(true, PLACEHOLDERSTR);
        }

        public override void Update()
        {
            // throw new NotImplementedException();
        }
    }
}
