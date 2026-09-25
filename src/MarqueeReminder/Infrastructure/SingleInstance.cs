using System;
using System.Threading;

namespace MarqueeReminder.Infrastructure
{
    /// <summary>
    /// Ensures only one copy of the app runs per user session. Launching it again brings the
    /// running copy's window to the front instead.
    /// </summary>
    internal sealed class SingleInstance : IDisposable
    {
        private const string MutexName = @"Local\MarqueeReminder.Instance.{6F1B7C2E-3D1A-4B8E-9C55-2A7E0D4F81B3}";
        private const string ActivateEventName = @"Local\MarqueeReminder.Activate.{6F1B7C2E-3D1A-4B8E-9C55-2A7E0D4F81B3}";

        private readonly Mutex _mutex;
        private readonly EventWaitHandle _activateEvent;
        private RegisteredWaitHandle? _registration;

        private SingleInstance(Mutex mutex)
        {
            _mutex = mutex;
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        }

        /// <summary>Returns the instance lock, or null if another instance already holds it.</summary>
        public static SingleInstance? TryAcquire()
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return null;
            }

            return new SingleInstance(mutex);
        }

        /// <summary>Asks the already-running instance to show its window.</summary>
        public static void SignalRunningInstance()
        {
            if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var handle))
            {
                using (handle)
                {
                    // This process was just launched by the user, so Windows lets it own the
                    // foreground. Hand that right on, or the running copy's window opens behind others.
                    NativeMethods.AllowSetForegroundWindow(NativeMethods.AsfwAny);

                    handle.Set();
                }
            }
        }

        /// <summary>Invokes <paramref name="onActivate"/> (on a thread-pool thread) whenever another launch is attempted.</summary>
        public void ListenForActivation(Action onActivate)
        {
            _registration?.Unregister(null);
            _registration = ThreadPool.RegisterWaitForSingleObject(
                _activateEvent,
                (state, timedOut) => onActivate(),
                state: null,
                millisecondsTimeOutInterval: Timeout.Infinite,
                executeOnlyOnce: false);
        }

        public void Dispose()
        {
            _registration?.Unregister(null);
            _activateEvent.Dispose();
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
