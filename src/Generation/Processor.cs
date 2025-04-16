using System;
using System.Collections;
using System.Collections.Generic;

namespace MapExporterNew.Generation
{
    internal abstract class Processor(Generator owner) : IEnumerator<float>
    {
        // For IEnumerator
        public float Current => Progress;
        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            try
            {
                if (process == null)
                {
                    process = Process();
                    return true;
                }

                return !Done && process.MoveNext();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
                Failed = true;
                return false;
            }
        }

        /// <summary>
        /// Do not call Reset on a generation processor
        /// </summary>
        public void Reset()
        {
            throw new NotImplementedException();
        }

        // For implementation
        public Generator owner = owner;
        protected abstract IEnumerator<float> Process();
        public float Progress => process.Current;
        public bool Done => Progress >= 1 || Failed;
        public bool Failed { get; private set; }
        public abstract string ProcessName { get; }
        private IEnumerator<float> process = null;
    }
}
