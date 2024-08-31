using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Menu;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using RWCustom;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    internal class OpPOIconManager(Vector2 pos, Vector2 size) : OpScrollBox(pos, size, 0f, false, true, true)
    {
        public void Initialize()
        {
            const float PADDING = 4f;
            const float MARGIN = 6f;
            const float ROWHEIGHT = BaseTab.CHECKBOX_SIZE;

            float y = size.y - PADDING + MARGIN;
            float combowidth = Math.Max(80, (int)((size.x - BaseTab.SCROLLBAR_WIDTH - PADDING * 2) * 0.4f));

            var sortedNames = Data.PlacedObjectIcons.Keys.OrderBy(x => x).ToArray();
            var iconOptions = Directory.GetFiles(Resources.ObjectIconPath())
                .Where(x => x.EndsWith(".png"))
                .Select(x =>
                {
                    var start = Math.Max(Math.Max(0, x.LastIndexOf("/")), x.LastIndexOf("\\")) + 1;
                    return x.Substring(start, x.Length - 4 - start);
                })
                .OrderBy(x => x)
                .ToArray();
            float c = 0f;
            foreach (var name in sortedNames)
            {
                y -= ROWHEIGHT + MARGIN;
                var (fileName, enabled) = Data.PlacedObjectIcons[name];
                OpCheckBox check = null;
                OpComboBox combo = null;
                AddItems(
                    check = new OpCheckBox(OIUtil.CosmeticBind(enabled), new Vector2(PADDING, y)),
                    new OpLabel(PADDING + BaseTab.CHECKBOX_SIZE + MARGIN, y, name),
                    combo = new OpComboBox2(OIUtil.CosmeticBind(fileName), new Vector2(size.x - combowidth - PADDING - BaseTab.SCROLLBAR_WIDTH, y), combowidth, iconOptions)
                    {
                        listHeight = 8
                    }
                );
                check.OnValueChanged += (_, _, _) =>
                {
                    Data.PlacedObjectIcons[name] = (combo.value, check.GetValueBool());
                    Data.SaveData();
                };
                combo.OnValueChanged += (_, _, _) =>
                {
                    Data.PlacedObjectIcons[name] = (combo.value, check.GetValueBool());
                };
                combo.OnListOpen += (_) =>
                {
                    combo.MoveToFront();
                };
                c -= 0.15f;
            }
            y -= PADDING;
            SetContentSize(size.y - y, true);
        }
    }

    /**
     * OpComboBox that has no transparent background
     * 
     * Thanks Henpemaz
     */
    public class OpComboBox2(Configurable<string> config, Vector2 pos, float width, string[] array) : OpComboBox(config, pos, width, array)
    {
        public override void Change()
        {
            base.Change();
            OnChanged?.Invoke();
        }
        public event Action OnChanged;

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);
            if (_rectList != null && !_rectList.isHidden)
            {
                myContainer.MoveToFront();

                for (int j = 0; j < 9; j++)
                {
                    _rectList.sprites[j].alpha = 1;
                }
            }
        }
    }
}
