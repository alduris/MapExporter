using System;
using MapExporter.Generation;

namespace MapExporter.Tabs
{
    internal class GenerateTab(OptionInterface owner) : BaseTab(owner, "Generator")
    {
        private Generator generator;
        public override void Initialize()
        {
            // throw new NotImplementedException();
        }

        public override void Update()
        {
            if (generator != null)
            {
                generator.Update();
                // todo: put progress update here and also remove generator if it is finished
            }
            // throw new NotImplementedException();
        }
    }
}
