using MapExporter.Server;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class ServerTab(OptionInterface owner) : BaseTab(owner, "Server")
    {
        private static LocalServer server;

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
                    button.text = "STOP";
                }
            };
            AddItems(
                button
            );

            // Resources.CopyFrontendFiles();
        }

        public override void Update()
        {
        }
    }
}
