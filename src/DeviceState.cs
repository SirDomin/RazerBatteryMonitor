using System;

namespace RazerBatteryMonitor
{
    internal enum DeviceKind
    {
        Mouse,
        Headset
    }

    internal sealed class DeviceState
    {
        public string Name { get; set; }
        public DeviceKind Kind { get; set; }
        public int? Level { get; set; }
        public bool IsCharging { get; set; }
        public bool IsOnline { get; set; }
        public bool IsStale { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Source { get; set; }
        public string Error { get; set; }

        public DeviceState()
        {
            Name = string.Empty;
            Source = string.Empty;
            Error = string.Empty;
        }

        public string ShortStatus()
        {
            if (!IsOnline || !Level.HasValue) return "brak danych";
            return string.Format("{0}%{1}", Level.Value, IsCharging && !IsStale ? " · ładowanie" : string.Empty);
        }
    }
}

