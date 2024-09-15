global using OpComboBox = MapExporter.Tabs.UI.OpComboBox2;
global using OpResourceSelector = MapExporter.Tabs.UI.OpResourceSelector2;

using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs.UI
{

    /**
     * OpComboBox that has no transparent background
     * 
     * Thanks Henpemaz
     */
    public class OpComboBox2 : Menu.Remix.MixedUI.OpComboBox
    {
        public OpComboBox2(Configurable<string> config, Vector2 pos, float width, string[] array) : base(config, pos, width, array) { }
        public OpComboBox2(Configurable<string> config, Vector2 pos, float width, List<ListItem> list) : base(config, pos, width, list) { }

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

    public class OpResourceSelector2 : Menu.Remix.MixedUI.OpResourceSelector
    {
        public OpResourceSelector2(ConfigurableBase config, Vector2 pos, float width) : base(config, pos, width) { }
        public OpResourceSelector2(Configurable<string> config, Vector2 pos, float width, SpecialEnum listType) : base(config, pos, width, listType) { }

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
