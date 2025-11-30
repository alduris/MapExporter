using System;
using RWCustom;
using UnityEngine;

namespace MapExporterNew.Screenshotter
{
    internal class ErrorPopup
    {
        public bool canContinue;
        public bool active = true;

        private Rect popupRect;
        private Vector2 scrollPos;
        private Vector2 buttonSize;

        private readonly string titleText;
        private readonly string descriptionText;
        private readonly string continueText;
        private readonly string exitText;

        public event Action OnContinue;

        public ErrorPopup(bool canContinue, string title, string description = "")
        {
            this.canContinue = canContinue;

            var screenCenter = Custom.rainWorld.options.ScreenSize / 2f;
            var popupSize = new Vector2(600f, 300f);
            var popupPos = screenCenter - popupSize / 2;
            popupRect = new Rect(popupPos, popupSize);

            buttonSize = new Vector2(160f, 24f);

            titleText = Translate(title);
            descriptionText = Translate(description);
            continueText = Translate("TRY CONTINUE");
            exitText = Translate("CLOSE GAME");
        }

        public void GuiUpdate()
        {
            GUI.Window(0, popupRect, GuiWindow, titleText);
        }

        private void GuiWindow(int windowID)
        {
            var scrollSize = new Vector2(popupRect.width - 40f, GUI.skin.label.CalcHeight(new GUIContent(descriptionText), popupRect.width - 40f));
            scrollPos = GUI.BeginScrollView(new Rect(10f, 30f, popupRect.width - 20f, popupRect.height - 70f), scrollPos, new Rect(Vector2.zero, scrollSize));
            GUI.Label(new Rect(Vector2.zero, scrollSize), descriptionText);
            GUI.EndScrollView();

            if (canContinue)
            {
                if (GUI.Button(new Rect(new Vector2(popupRect.width / 2 - 5f - buttonSize.x, popupRect.height - 10f - buttonSize.y), buttonSize), continueText))
                {
                    Continue();
                }

                if (GUI.Button(new Rect(new Vector2(popupRect.width / 2 + 5f, popupRect.height - 10f - buttonSize.y), buttonSize), exitText))
                {
                    Cancel();
                }
            }
            else if (GUI.Button(new Rect(new Vector2(popupRect.width / 2 - buttonSize.x / 2, popupRect.height - 10f - buttonSize.y), buttonSize), exitText))
            {
                // Can't continue so only close button is needed
                Cancel();
            }
        }

        private string Translate(string text) => Custom.rainWorld.inGameTranslator.Translate(text);

        public void Continue()
        {
            if (!canContinue)
            {
                return;
            }
            active = false;

            OnContinue?.Invoke();
        }

        public void Cancel()
        {
            Data.ScreenshotterStatus = Data.SSStatus.Errored;
            Data.SaveData();
            Application.Quit();
        }
    }
}
