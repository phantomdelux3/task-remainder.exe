using System;

namespace MarqueeReminder.Core
{
    public sealed class SaveFailedEventArgs : EventArgs
    {
        public SaveFailedEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; }
    }
}
