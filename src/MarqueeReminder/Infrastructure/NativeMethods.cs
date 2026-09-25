using System.Runtime.InteropServices;

namespace MarqueeReminder.Infrastructure
{
    internal static class NativeMethods
    {
        /// <summary>Lets any process take the foreground; see AllowSetForegroundWindow.</summary>
        public const int AsfwAny = -1;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllowSetForegroundWindow(int processId);
    }
}
