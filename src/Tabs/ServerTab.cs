using System.Collections.Generic;
using MapExporter.Server;
using Menu;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class ServerTab : BaseTab
    {
        private static LocalServer server;
        private Queue<string> undisplayedMessages = [];

        public ServerTab(OptionInterface owner) : base(owner, "Run")
        {
            OnPreDeactivate += ServerTab_OnPreDeactivate;
        }

        private void ServerTab_OnPreDeactivate()
        {
            if (server != null)
            {
                server.OnMessage -= Server_OnMessage;
            }
        }

        public override void Initialize()
        {
            var button = new OpSimpleButton(new Vector2(300f - 30f, 300f - 12f), new Vector2(60f, 24f), server == null ? "RUN" : "STOP");
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
            AddItems(
                new OpLabel(10f, 560f, "SERVER", true),
                button,
                new OpImage(new Vector2(10f, 299f), "pixel") { scale = new Vector2(580f, 2f), color = MenuColorEffect.rgbMediumGrey }
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
