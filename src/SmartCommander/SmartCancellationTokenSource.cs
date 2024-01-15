using System.Threading;

namespace SmartCommander
{
    class SmartCancellationTokenSource : CancellationTokenSource
    {
        public bool IsDisposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            IsDisposed = true;
        }
    }
}
