using System;
using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    internal class OpControlBox : OpScrollBox
    {
        private OpMapBox mapBox;
        private OpSimpleImageButton upArrow, rightArrow, downArrow, leftArrow;
        private bool _lhu, _lhr, _lhd, _lhl;

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

            const float SCROLLBAR_WIDTH = BaseTab.SCROLLBAR_WIDTH;
            const float INNER_PAD = 4f;
            float optionsHeight = size.y - SCROLLBAR_WIDTH - INNER_PAD * 2f;
            float arrowSize = (optionsHeight - INNER_PAD * 2f) / 3f;

            // Arrow keys
            upArrow = new OpSimpleImageButton(new Vector2(INNER_PAD * 2f + arrowSize, INNER_PAD * 3f + arrowSize * 2f + SCROLLBAR_WIDTH), new Vector2(arrowSize, arrowSize), "keyArrowA");
            rightArrow = new OpSimpleImageButton(new Vector2(INNER_PAD * 3f + arrowSize * 2f, INNER_PAD * 2f + arrowSize + SCROLLBAR_WIDTH), new Vector2(arrowSize, arrowSize), "keyArrowA");
            downArrow = new OpSimpleImageButton(new Vector2(INNER_PAD * 2f + arrowSize, INNER_PAD + SCROLLBAR_WIDTH), new Vector2(arrowSize, arrowSize), "keyArrowA");
            leftArrow = new OpSimpleImageButton(new Vector2(INNER_PAD, INNER_PAD * 2f + arrowSize + SCROLLBAR_WIDTH), new Vector2(arrowSize, arrowSize), "keyArrowA");

            upArrow.sprite.rotation = 0f;
            rightArrow.sprite.rotation = 90f;
            downArrow.sprite.rotation = 180f;
            leftArrow.sprite.rotation = 270f;

            upArrow.OnUpdate += UpArrow_OnUpdate;
            rightArrow.OnUpdate += RightArrow_OnUpdate;
            downArrow.OnUpdate += DownArrow_OnUpdate;
            leftArrow.OnUpdate += LeftArrow_OnUpdate;

            AddItems(upArrow, rightArrow, downArrow, leftArrow);
        }

        public override void Update()
        {
            base.Update();
        }

        private void UpArrow_OnUpdate()
        {
            if (upArrow.held && mapBox.activeRoom != null)
            {
                mapBox.activeRegion.rooms[mapBox.activeRoom].devPos += Vector2.up;
                mapBox.UpdateMap();
            }

            if (upArrow.held != _lhu)
            {
                upArrow.sprite.SetElementByName(upArrow.held ? "keyArrowB" : "keyArrowA");
            }
            _lhu = upArrow.held;
        }

        private void RightArrow_OnUpdate()
        {
            if (rightArrow.held && mapBox.activeRoom != null)
            {
                mapBox.activeRegion.rooms[mapBox.activeRoom].devPos += Vector2.right;
                mapBox.UpdateMap();
            }

            if (rightArrow.held != _lhr)
            {
                rightArrow.sprite.SetElementByName(rightArrow.held ? "keyArrowB" : "keyArrowA");
            }
            _lhr = rightArrow.held;
        }

        private void DownArrow_OnUpdate()
        {
            if (downArrow.held && mapBox.activeRoom != null)
            {
                mapBox.activeRegion.rooms[mapBox.activeRoom].devPos += Vector2.down;
                mapBox.UpdateMap();
            }

            if (downArrow.held != _lhd)
            {
                downArrow.sprite.SetElementByName(downArrow.held ? "keyArrowB" : "keyArrowA");
            }
            _lhd = downArrow.held;
        }

        private void LeftArrow_OnUpdate()
        {
            if (leftArrow.held && mapBox.activeRoom != null)
            {
                mapBox.activeRegion.rooms[mapBox.activeRoom].devPos += Vector2.left;
                mapBox.UpdateMap();
            }

            if (leftArrow.held != _lhl)
            {
                leftArrow.sprite.SetElementByName(leftArrow.held ? "keyArrowB" : "keyArrowA");
            }
            _lhl = leftArrow.held;
        }
    }
}
