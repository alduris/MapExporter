using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using MapExporter.Server;
using MapExporter.Tabs.UI;
using Menu;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class ServerTab(OptionInterface owner) : BaseTab(owner, "Test/Export")
    {
        private const float MB_VERT_PAD = 8f;
        private static LocalServer server;
        private Queue<string> undisplayedMessages = [];
        private OpScrollBox messageBox;
        private float messageBoxTotalHeight = MB_VERT_PAD;

        private OpDirPicker dirPicker;

        public override void Initialize()
        {
            const float PADDING = 10f;
            const float MARGIN = 6f;
            const float DIVIDER = MENU_SIZE * 0.5f;

            string serverText = "Server controls:";
            float serverTextWidth = LabelTest.GetWidth(serverText, false);
            string openText = "Open in browser:";
            float openTextWidth = LabelTest.GetWidth(openText, false);

            var serverButton = new OpSimpleButton(
                new Vector2(PADDING + serverTextWidth + MARGIN, MENU_SIZE - PADDING - BIG_LINE_HEIGHT - MARGIN - 24f),
                new Vector2(60f, 24f), server == null ? "RUN" : "STOP");
            var openButton = new OpSimpleButton(
                new Vector2(serverButton.pos.x + serverButton.size.x + MARGIN * 2 + openTextWidth, serverButton.pos.y),
                new Vector2(60f, 24f), "OPEN");
            serverButton.OnClick += (_) =>
            {
                if (server != null)
                {
                    server.Dispose();
                    server.OnMessage -= Server_OnMessage;
                    server = null;
                    serverButton.text = "RUN";
                }
                else
                {
                    server = new LocalServer();
                    server.OnMessage += Server_OnMessage;
                    server.Initialize();
                    serverButton.text = "STOP";

                    foreach (var item in messageBox.items)
                    {
                        _RemoveItem(item);
                    }
                    messageBox.items.Clear();
                    messageBox.SetContentSize(0f);
                    messageBoxTotalHeight = MB_VERT_PAD;
                }
            };
            openButton.OnClick += (self) =>
            {
                if (server != null)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = LocalServer.URL,
                        UseShellExecute = true
                    });
                }
                else
                {
                    self.PlaySound(SoundID.MENU_Error_Ping);
                }
            };
            messageBox = new OpScrollBox(
                new Vector2(PADDING, DIVIDER + PADDING),
                new Vector2(MENU_SIZE - 2 * PADDING, MENU_SIZE - DIVIDER - PADDING * 2 - MARGIN * 2 - BIG_LINE_HEIGHT - 24f),
                0f);

            dirPicker = new OpDirPicker(new Vector2(PADDING, PADDING), new Vector2(MENU_SIZE - 2 * PADDING, DIVIDER - 2 * PADDING - MARGIN - BIG_LINE_HEIGHT - 24f));
            AddItems(
                new OpLabel(PADDING, MENU_SIZE - PADDING - BIG_LINE_HEIGHT, "TEST", true),
                new OpLabel(PADDING, serverButton.pos.y + 2f, serverText, false),
                serverButton,
                new OpLabel(openButton.pos.x - MARGIN - openTextWidth, openButton.pos.y + 2f, openText, false),
                openButton,
                messageBox,
                new OpImage(new Vector2(PADDING, DIVIDER - 1), "pixel") { scale = new Vector2(MENU_SIZE - PADDING * 2, 2f), color = MenuColorEffect.rgbMediumGrey },
                new OpLabel(PADDING, DIVIDER - PADDING - BIG_LINE_HEIGHT, "EXPORT", true),
                dirPicker
            );

            Resources.CopyFrontendFiles();
        }

        public override void Update()
        {
            const float PADDING = 6f;
            while (undisplayedMessages.Count > 0) {
                float width = messageBox.size.x - 2 * PADDING - SCROLLBAR_WIDTH;
                string text = undisplayedMessages.Dequeue();
                text = LabelTest.WrapText(text, false, width, true);
                int lines = text.Split('\n').Length;
                float height = LabelTest._lineHeight * lines;
                messageBoxTotalHeight += height;

                messageBox.AddItems(
                    new OpLabelLong(new Vector2(PADDING, messageBox.size.y - messageBoxTotalHeight), new Vector2(width, height), text, false, FLabelAlignment.Left)
                    );
                messageBoxTotalHeight += MB_VERT_PAD;
                messageBox.SetContentSize(messageBoxTotalHeight, false);
            }
        }

        private void Server_OnMessage(string message)
        {
            Plugin.Logger.LogMessage("Server: " + message);
            undisplayedMessages.Enqueue(message);
        }
    }
}
