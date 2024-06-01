using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    /// <summary>
    /// A system to recycle old labels as needed so as to not produce waste or the weird flickering
    /// bug from labels being replaced every frame (due to lastPos shenanigans)
    /// </summary>
    /// <param name="scrollBox"></param>
    public class LabelBorrower(OpScrollBox scrollBox)
    {
        public readonly OpScrollBox owner = scrollBox;
        public readonly HashSet<OpLabel> labels = [];
        private readonly HashSet<OpLabel> oldLabels = [];

        public void Update()
        {
            foreach (var label in oldLabels)
            {
                label.Deactivate();
                owner.items.Remove(owner);
                owner.tab.items.Remove(label);
                labels.Remove(label);
            }
            oldLabels.Clear();
            foreach (var label in labels)
            {
                label.Hide();
                oldLabels.Add(label);
            }
        }

        public OpLabel AddLabel(string text, Vector2 pos)
        {
            OpLabel label = null;
            foreach (var item in oldLabels)
            {
                if (item.text == text)
                {
                    label = item;
                    break;
                }
            }

            if (label == null)
            {
                label = new OpLabel(pos.x, pos.y, text, false);
                owner.AddItems(label);
                labels.Add(label);
            }
            else
            {
                label.SetPos(pos);
                label.lastScreenPos = label.pos;
                oldLabels.Remove(label);
            }
            label.Show();
            return label;
        }
    }
}
