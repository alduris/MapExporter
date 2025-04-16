using System;
using Menu;
using UnityEngine;

namespace MapExporterNew.Screenshotter.UI
{
    internal class PopupButton : IPopupUI
    {
        private readonly PopupRoundedRect buttonRect;
        private readonly PopupRoundedRect selectRect;
        private readonly PopupLabel label;

        public Vector2 pos;
        public Vector2 size;
        public Color bgColor = new Color(0f, 0f, 0f);
        public Color borderColor = Color.white;
        public Color? selectColor = null;
        public Color labelColor = MenuColorEffect.rgbMediumGrey;

        public event Action OnClick;

        public bool MouseOver
        {
            get => new Rect(pos - size / 2f, size).Contains(Futile.mousePosition);
        }
        private bool lastMouseOver = false;
        private bool lastMouseDown = false;
        private bool mouseDown = false;

        public PopupButton(FContainer container, Vector2 pos, Vector2 size, string text)
        {
            this.pos = pos;
            this.size = size;
            buttonRect = new PopupRoundedRect(container, pos, size, true);
            selectRect = new PopupRoundedRect(container, pos, size, false);
            label = new PopupLabel(container, pos, text, false);
            Update();
        }

        public void Update()
        {
            // Update some things
            buttonRect.size = size;
            buttonRect.pos = pos;
            buttonRect.bgColor = bgColor;
            buttonRect.borderColor = borderColor;
            selectRect.pos = pos;
            label.color = labelColor;

            // Check if mouse is down and deal with that
            mouseDown = Input.GetMouseButton(0);
            bool clicked = mouseDown && !lastMouseDown && MouseOver && lastMouseOver;

            if (clicked)
            {
                OnClick?.Invoke();
            }
            selectRect.size = buttonRect.size + (MouseOver ? new Vector2(10f, 6f) * (clicked ? 0.667f : 1f) : Vector2.zero);
            if (selectColor != null)
            {
                selectRect.borderColor = MouseOver ? selectColor.Value : borderColor;
            }

            // Update subelements
            buttonRect.Update();
            selectRect.Update();
            label.Update();

            // Update last values
            lastMouseOver = MouseOver;
            lastMouseDown = mouseDown;
        }

        public void Unload()
        {
            buttonRect.Unload();
            selectRect.Unload();
            label.Unload();
        }
    }
}
