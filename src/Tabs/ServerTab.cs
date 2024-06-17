using System.Collections.Generic;
using MapExporter.Server;
using MapExporter.Tabs.UI;
using Menu;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class ServerTab(OptionInterface owner) : BaseTab(owner, "Run")
    {
        private static LocalServer server;
        private Queue<string> undisplayedMessages = [];
        private OpScrollBox messageBox;

        private OpDirPicker dirPicker;

        public override void Initialize()
        {
            const float PADDING = 10f;
            const float MARGIN = 6f;
            const float DIVIDER = MENU_SIZE * 0.5f;

            var button = new OpSimpleButton(new Vector2(300f - 30f, 500f - 12f), new Vector2(60f, 24f), server == null ? "RUN" : "STOP");
            button.OnClick += (_) =>
            {
                if (server != null)
                {
                    server.Dispose();
                    server = null;
                    button.text = "RUN";
                }
                else
                {
                    server = new LocalServer();
                    server.Initialize();
                    server.OnMessage += Server_OnMessage;
                    button.text = "STOP";
                }
            };
            messageBox = new OpScrollBox(
                new Vector2(PADDING, MENU_SIZE - PADDING - BIG_LINE_HEIGHT - MARGIN),
                new Vector2(MENU_SIZE - 2 * PADDING, MENU_SIZE - DIVIDER - 2 * PADDING - MARGIN - BIG_LINE_HEIGHT - LINE_HEIGHT),
                0f);
            dirPicker = new OpDirPicker(new Vector2(PADDING, PADDING), new Vector2(MENU_SIZE - 2 * PADDING, DIVIDER - 2 * PADDING - MARGIN - BIG_LINE_HEIGHT - 24f));
            AddItems(
                new OpLabel(PADDING, MENU_SIZE - PADDING - BIG_LINE_HEIGHT, "SERVER", true),
                button,
                messageBox,
                new OpImage(new Vector2(PADDING, DIVIDER - 1), "pixel") { scale = new Vector2(MENU_SIZE - PADDING * 2, 2f), color = MenuColorEffect.rgbMediumGrey },
                new OpLabel(PADDING, DIVIDER - PADDING - BIG_LINE_HEIGHT, "EXPORT", true),
                dirPicker
            );

            Resources.CopyFrontendFiles();
        }

        public override void Update()
        {
        }

        private void Server_OnMessage(string message)
        {
            undisplayedMessages.Enqueue(message);
        }
    }
}
