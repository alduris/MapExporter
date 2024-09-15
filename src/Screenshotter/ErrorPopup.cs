using System;
using System.Collections.Generic;
using MapExporter.Screenshotter.UI;
using RWCustom;
using UnityEngine;

namespace MapExporter.Screenshotter
{
    internal class ErrorPopup
    {
        public bool canContinue;
        public string title;
        public string description;
        public bool active = true;

        public event Action OnContinue;

        private readonly FContainer container;
        private readonly List<IPopupUI> elements = [];

        public ErrorPopup(bool canContinue, string title, string description = "")
        {
            this.canContinue = canContinue;
            this.title = title;
            this.description = description;

            container = new FContainer();
            var screenCenter = Custom.rainWorld.options.ScreenSize / 2f;
            var popupSize = new Vector2(600f, 480f);
            elements.Add(new PopupRoundedRect(container, screenCenter, popupSize, true, 0.95f));
            elements.Add(new PopupLabel(container, screenCenter + popupSize * 0.5f * Vector2.up + Vector2.down * 25f, title, true));
            elements.Add(new PopupLabel(container, screenCenter + popupSize * 0.5f * Vector2.up + Vector2.down * 45f, description, false));
            var buttonSize = new Vector2(80f, 24f);
            if (canContinue)
            {
                // We can continue so we want both buttons
                var continueButton = new PopupButton(container, screenCenter + popupSize * 0.5f * Vector2.down + Vector2.up * 25f + buttonSize * 0.5f * Vector2.left + Vector2.left * 5f, buttonSize, "TRY CONTINUE");
                continueButton.OnClick += Continue;
                elements.Add(continueButton);

                var cancelButton = new PopupButton(container, screenCenter + popupSize * 0.5f * Vector2.down + Vector2.up * 25f + buttonSize * 0.5f * Vector2.right + Vector2.right * 5f, buttonSize, "CLOSE GAME");
                cancelButton.OnClick += Cancel;
                elements.Add(cancelButton);
            }
            else
            {
                // We can't continue so we only need the close button
                var cancelButton = new PopupButton(container, screenCenter + popupSize * 0.5f * Vector2.down + Vector2.up * 25f, buttonSize, "CLOSE GAME");
                cancelButton.OnClick += Cancel;
                elements.Add(cancelButton);
            }
        }

        public void Update()
        {
            foreach (var element in elements)
            {
                element.Update();
            }
        }

        public void Continue()
        {
            if (!canContinue)
            {
                throw new Exception("Error popup cannot continue!");
            }
            active = false;

            OnContinue?.Invoke();

            foreach (var element in elements)
            {
                element.Unload();
            }
            container.RemoveAllChildren();
            container.RemoveFromContainer();
        }

        public void Cancel()
        {
            Data.ScreenshotterStatus = Data.SSStatus.Errored;
            Data.SaveData();
            Application.Quit();
        }
    }
}
