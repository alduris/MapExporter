using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private OpProgressBar progressBar = null;
        private OpLabel progressLabel = null;
        private OpTextBox outputLoc;
        private OpHoldButton exportButton;
        private OpCheckBox zipFileCheckbox;
        private OpResourceSelector modeSelector;
        private bool exportCooldown = false;
        private int exportCooldownCount = 0;

        private string savedDir = null;

        private const float PADDING = 10f;
        private const float MARGIN = 6f;
        private const float DIVIDER = MENU_SIZE * 0.6f;
        private const float DIRPICKER_HEIGHT = DIVIDER - 2 * PADDING - MARGIN * 3 - BIG_LINE_HEIGHT - 48f;
        private const float EXPORT_COLUMN = (MENU_SIZE - PADDING * 2 + MARGIN) / 3;

        public override void Initialize()
        {

            string serverText = "Server controls:";
            float serverTextWidth = LabelTest.GetWidth(serverText, false);
            string openText = "Open in browser:";
            float openTextWidth = LabelTest.GetWidth(openText, false);

            var serverButton = new OpSimpleButton(
                new Vector2(PADDING + serverTextWidth + MARGIN, MENU_SIZE - PADDING - BIG_LINE_HEIGHT - MARGIN - 24f),
                new Vector2(60f, 24f), server == null ? "RUN" : "STOP")
            { colorEdge = YellowColor };
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
                else if (Data.FinishedRegions.Count == 0)
                {
                    serverButton.PlaySound(SoundID.MENU_Error_Ping);
                    undisplayedMessages.Enqueue("No regions ready yet!");
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

            dirPicker = new OpDirPicker(new Vector2(PADDING, PADDING), new Vector2(MENU_SIZE - 2 * PADDING, DIRPICKER_HEIGHT), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            outputLoc = new OpTextBox(OIUtil.CosmeticBind("mapexport"), new Vector2(PADDING, dirPicker.pos.y + dirPicker.size.y + MARGIN * 2 + 24f), MENU_SIZE - PADDING * 2 - 60f - MARGIN);
            outputLoc.OnValueChanged += (_, newVal, _) => outputLoc.value = Exporter.SafeFileName(outputLoc.value);
            exportButton = new OpHoldButton(new Vector2(outputLoc.PosX + outputLoc.size.x + MARGIN, outputLoc.PosY), new Vector2(60f, 24f), "EXPORT")
            {
                colorEdge = BlueColor,
            };
            exportButton.OnPressDone += ExportButton_OnPressDone;

            zipFileCheckbox = new OpCheckBox(OIUtil.CosmeticBind(false), PADDING, PADDING + DIRPICKER_HEIGHT + MARGIN);
            modeSelector = new OpResourceSelector(OIUtil.CosmeticBind(Exporter.ExportType.Server), new Vector2(PADDING + EXPORT_COLUMN, zipFileCheckbox.PosY), EXPORT_COLUMN * 2 - MARGIN);
            modeSelector._itemList = modeSelector._itemList.Select(x => new ListItem(x.name, Translate(Exporter.ExportTypeName(x.name)), x.value)).ToArray();

            AddItems(
                new OpShinyLabel(PADDING, MENU_SIZE - PADDING - BIG_LINE_HEIGHT, "TEST SERVER", true),
                new OpLabel(PADDING, serverButton.pos.y + 2f, serverText, false),
                serverButton,
                new OpLabel(openButton.pos.x - MARGIN - openTextWidth, openButton.pos.y + 2f, openText, false),
                openButton,
                messageBox,
                new OpImage(new Vector2(PADDING, DIVIDER - 1), "pixel") { scale = new Vector2(MENU_SIZE - PADDING * 2, 2f), color = MenuColorEffect.rgbMediumGrey },
                new OpShinyLabel(PADDING, DIVIDER - PADDING - BIG_LINE_HEIGHT, "EXPORTER", true),
                dirPicker,
                outputLoc,
                zipFileCheckbox,
                new OpLabel(PADDING + CHECKBOX_SIZE + MARGIN, zipFileCheckbox.PosY, "Zip result?", false),
                modeSelector,
                exportButton
            );
            if (server == null)
            {
                Server_OnMessage("Press the 'RUN' button to test the map locally");
            }
            else
            {
                Server_OnMessage("Server already running!");
            }
        }

        public override void Update()
        {
            const float PADDING = 6f;
            while (undisplayedMessages.Count > 0) {
                float width = messageBox.size.x - 2 * PADDING - SCROLLBAR_WIDTH;
                string text = undisplayedMessages.Dequeue().Trim();
                text = LabelTest.WrapText(text, false, width, true);
                int lines = text.Split('\n').Length;
                float height = LabelTest._textHeight * lines + 4f;
                messageBoxTotalHeight += height;

                messageBox.AddItems(new OpLabelLong(new Vector2(PADDING, messageBox.size.y - messageBoxTotalHeight), new Vector2(width, height), text, false, FLabelAlignment.Left));
                messageBoxTotalHeight += MB_VERT_PAD;
                messageBox.SetContentSize(messageBoxTotalHeight, true);
            }

            if (!exportCooldown && exportCooldownCount > 0) exportCooldownCount--;
            exportButton.greyedOut = exportCooldown || exportCooldownCount > 0;
            if (exportCooldown || exportCooldownCount > 0) exportButton.held = false;

            if (dirPicker == null)
            {
                progressBar.Progress((float)Exporter.currentProgress / Exporter.FileCount);

                if (!Exporter.inProgress && !exportCooldown)
                {
                    RemoveItems(progressBar, progressLabel);

                    dirPicker = new OpDirPicker(new Vector2(PADDING, PADDING), new Vector2(MENU_SIZE - 2 * PADDING, DIRPICKER_HEIGHT), savedDir);
                    AddItems(dirPicker);
                }
                else if (Exporter.zipping)
                {
                    progressLabel.text = "Zipping... (this will take a while)";
                }
                else
                {
                    progressLabel.text = $"{Translate("Exporting...")} ({Exporter.currentProgress}/{Exporter.FileCount})";
                }
            }
        }

        private void Server_OnMessage(string message)
        {
            Plugin.Logger.LogMessage("Server: " + message);
            undisplayedMessages.Enqueue(message);
        }

        private void ExportButton_OnPressDone(UIfocusable trigger)
        {
            exportCooldown = true;
            exportButton.held = false;
            exportButton.greyedOut = true;

            savedDir = dirPicker.CurrentDir.FullName;
            
            progressBar = new OpProgressBar(new Vector2(dirPicker.PosX, dirPicker.PosY + dirPicker.size.y / 2), MENU_SIZE - PADDING * 2);
            AddItems(progressBar);
            progressBar.Initialize();

            progressLabel = new OpLabel(progressBar.PosX, progressBar.PosY - 16f, "Exporting...", false);
            AddItems(progressLabel);

            RemoveItems(dirPicker);
            dirPicker = null;


            // Export the server on another thread so as to not freeze game
            Task.Run(() =>
            {
                var exportType = Enum.TryParse<Exporter.ExportType>(modeSelector.value ?? "", out var etr) ? etr : Exporter.ExportType.Server;
                try
                {
                    Exporter.ExportServer(exportType, zipFileCheckbox.GetValueBool(), Path.Combine(savedDir, outputLoc.value));
                    exportButton.PlaySound(SoundID.MENU_Karma_Ladder_Increase_Bump); // yay
                }
                catch (UnauthorizedAccessException uae)
                {
                    Plugin.Logger.LogError(uae);
                    exportButton.PlaySound(SoundID.MENU_Error_Ping); // aw
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError(e);
                    exportButton.PlaySound(SoundID.MENU_Error_Ping); // aw but why did I put it in another catch statement I forgot
                }
                finally
                {
                    exportCooldown = false;
                    exportCooldownCount = 120;
                }
            });
        }
    }
}
