using System;
using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    internal class OpControlBox : OpScrollBox
    {
        private OpMapBox mapBox;

        public OpControlBox(OpTab tab, float contentSize, bool horizontal = false, bool hasSlideBar = true) : base(tab, contentSize, horizontal, hasSlideBar)
        {
            throw new NotImplementedException(); // nope, not for you :3
        }

        public OpControlBox(Vector2 pos, Vector2 size) : base(pos, size, size.x, true, true, true)
        {
        }

        public void Initialize(OpMapBox mapBox)
        {
            this.mapBox = mapBox;
        }

        public override void Update()
        {
            base.Update();

            const float SCROLLBAR_WIDTH = BaseTab.SCROLLBAR_WIDTH;
            const float INNER_PAD = 6f;
            float optionsHeight = size.y - SCROLLBAR_WIDTH - INNER_PAD * 2f;
        }
    }
}
