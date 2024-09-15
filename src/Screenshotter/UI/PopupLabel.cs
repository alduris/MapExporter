using System;
using RWCustom;
using UnityEngine;

// shut up visual studio I don't give a fuck about property naming styles
#pragma warning disable IDE1006 // Naming Styles

namespace MapExporter.Screenshotter.UI
{
    internal class PopupLabel : IPopupUI
    {
        private readonly FLabel label;
        public string text
        {
            get => label.text;
            set => label.text = value;
        }
        public Vector2 pos;
        public FLabelAlignment alignment
        {
            get => label.alignment;
            set => label.alignment = value;
        }
        public Color color
        {
            get => label.color;
            set => label.color = value;
        }

        public PopupLabel(FContainer container, Vector2 pos, string message, bool bigText, FTextParams textParams = null)
        {
            this.pos = pos;
            label = new FLabel(bigText ? Custom.GetDisplayFont() : Custom.GetFont(), text, (textParams == null) ? new FTextParams() : textParams)
            {
                alignment = FLabelAlignment.Center
            };
            container.AddChild(label);
        }

        public void Update()
        {
            label.x = pos.x;
            label.y = pos.y;
        }

        public void Unload()
        {
            label.RemoveFromContainer();
        }
    }
}
#pragma warning restore IDE1006 // Naming Styles
