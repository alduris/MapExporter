using System;
using System.Collections.Generic;
using Menu;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    /// <summary>
    /// Basically OpScrollBox but remove the scroll bars and allow scrolling in multiple directions :P
    /// I have no clue how this is going to work on controller
    /// </summary>
    internal partial class OpMapBox : UIfocusable, IEquatable<OpMapBox>
    {
        public readonly HashSet<MapItem> mapItems = [];
        private static readonly List<Camera> PanCameras = [];
        public int camIndex;
        public Camera cam;
        public Vector2 camPos;
        public Vector2 childOffset;

        public Color colorBack = MenuColorEffect.rgbBlack;
        public Color colorEdge = MenuColorEffect.rgbMediumGrey;

        protected readonly DyeableRect rectBack;

        public OpMapBox(Vector2 pos, Vector2 size, bool hasBack) : base(pos, size)
        {
            // Create the camera
            camIndex = PanCameras.Count;
            for (int i = 0; i < PanCameras.Count; i++)
            {
                if (PanCameras[i] == null)
                {
                    camIndex = i;
                    break;
                }
            }
            GameObject obj = new("OpPanBox Camera " + camIndex.ToString());
            cam = obj.AddComponent<Camera>();
            if (camIndex == PanCameras.Count)
            {
                PanCameras.Add(cam);
            }
            else
            {
                PanCameras[camIndex] = cam;
            }

            // Positions n stuff
            camPos = new Vector2(-10000f - 20000f * camIndex, 10000);
            childOffset = camPos;

            // Back
            if (hasBack)
            {
                rectBack = new DyeableRect(myContainer, pos, size, true)
                {
                    colorEdge = colorEdge,
                };
            }
        }

        public bool Equals(OpMapBox other)
        {
            return camIndex == other.camIndex;
        }
    }
}
