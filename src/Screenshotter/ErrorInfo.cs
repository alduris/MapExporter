using System;

namespace MapExporterNew.Screenshotter
{
    internal struct ErrorInfo
    {
        public string title;
        public string message;
        public bool canContinue;
        public Action onContinue;
    }
}
