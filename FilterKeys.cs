using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyDebounce
{
    /// <summary>Current Windows Filter Keys state, with all times in milliseconds.</summary>
    internal struct FilterKeysState
    {
        public bool Enabled;
        public int WaitMs;
        public int DelayMs;
        public int RepeatMs;
        public int BounceMs;
    }

    /// <summary>
    /// Thin wrapper around SystemParametersInfo(SPI_GET/SETFILTERKEYS).
    /// Windows' Settings app only offers bounce times of 0.3 s and up; the API accepts any value.
    /// </summary>
    internal static class FilterKeys
    {
        const uint SPI_GETKEYBOARDSPEED = 0x000A;
        const uint SPI_GETKEYBOARDDELAY = 0x0016;
        const uint SPI_GETFILTERKEYS = 0x0032;
        const uint SPI_SETFILTERKEYS = 0x0033;

        const uint SPIF_UPDATEINIFILE = 0x1; // persist to the user profile
        const uint SPIF_SENDCHANGE = 0x2;    // notify running apps

        const uint FKF_FILTERKEYSON = 0x1;
        const uint FKF_AVAILABLE = 0x2;

        [StructLayout(LayoutKind.Sequential)]
        struct FILTERKEYS
        {
            public uint cbSize;
            public uint dwFlags;
            public uint iWaitMSec;
            public uint iDelayMSec;
            public uint iRepeatMSec;
            public uint iBounceMSec;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint action, uint param, ref FILTERKEYS value, uint winIni);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);

        public static FilterKeysState Read()
        {
            var fk = Raw();
            return new FilterKeysState
            {
                Enabled = (fk.dwFlags & FKF_FILTERKEYSON) != 0,
                WaitMs = (int)fk.iWaitMSec,
                DelayMs = (int)fk.iDelayMSec,
                RepeatMs = (int)fk.iRepeatMSec,
                BounceMs = (int)fk.iBounceMSec,
            };
        }

        /// <summary>
        /// Turns on bounce filtering. While Filter Keys is on it also owns key auto-repeat,
        /// so delayMs/repeatMs must be non-zero or holding a key will not repeat.
        /// </summary>
        public static void Enable(int bounceMs, int delayMs, int repeatMs)
        {
            var fk = New();
            fk.dwFlags = FKF_FILTERKEYSON | FKF_AVAILABLE; // no beep, no tray icon, no hotkey
            fk.iBounceMSec = (uint)bounceMs;
            fk.iDelayMSec = (uint)delayMs;
            fk.iRepeatMSec = (uint)repeatMs;

            // Windows can drop the timing values on the call that also switches Filter Keys on,
            // so verify and retry once.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Set(fk);
                var now = Read();
                if (now.Enabled && now.BounceMs == bounceMs && now.DelayMs == delayMs && now.RepeatMs == repeatMs)
                    return;
            }
            throw new InvalidOperationException("Windows did not accept the new Filter Keys settings.");
        }

        public static void Disable()
        {
            var fk = New();
            fk.dwFlags = FKF_AVAILABLE;
            Set(fk);
        }

        /// <summary>The user's normal key repeat settings from Control Panel > Keyboard, in milliseconds.</summary>
        public static void GetWindowsRepeat(out int delayMs, out int repeatMs)
        {
            uint delay = 1, speed = 31;
            SystemParametersInfo(SPI_GETKEYBOARDDELAY, 0, ref delay, 0); // 0..3 => 250..1000 ms
            SystemParametersInfo(SPI_GETKEYBOARDSPEED, 0, ref speed, 0); // 0..31 => ~2.5..30 repeats/s
            delayMs = (int)(delay + 1) * 250;
            repeatMs = (int)Math.Round(1000.0 / (2.5 + speed * 27.5 / 31.0));
        }

        static FILTERKEYS New() => new FILTERKEYS { cbSize = (uint)Marshal.SizeOf(typeof(FILTERKEYS)) };

        static FILTERKEYS Raw()
        {
            var fk = New();
            if (!SystemParametersInfo(SPI_GETFILTERKEYS, fk.cbSize, ref fk, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return fk;
        }

        static void Set(FILTERKEYS fk)
        {
            if (!SystemParametersInfo(SPI_SETFILTERKEYS, fk.cbSize, ref fk, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
