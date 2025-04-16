using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporterNew.Tabs.UI
{
    public class OpTextButton(Vector2 pos, Vector2 size, string displayText = "") : OpSimpleButton(pos, size, displayText)
    {
        private readonly string _origText = displayText;

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);
            _rect.Hide();
            _rectH.Hide();
        }

        public override void Update()
        {
            base.Update();

            if (Active)
                bumpBehav.col = 1f;
        }

        private bool _active = false;
        public bool Active
        {
            get => _active;
            set
            {
                if (_active != value)
                {
                    _active = value;
                    _label.text = value ? "> " + _origText : _origText;
                }
            }
        }
    }
}
