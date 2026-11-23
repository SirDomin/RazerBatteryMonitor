using System;
using System.Collections.Generic;
using System.Linq;

namespace RazerBatteryMonitor
{
    internal sealed class MonitorService
    {
        internal IList<DeviceState> ReadAll()
        {
            List<DeviceState> states = new List<DeviceState>();

            DeviceState naga = SynapseLogReader.ReadProduct(167, "Razer Naga V2 Pro", DeviceKind.Mouse);
            if (!naga.IsOnline)
            {
                DeviceState wirelessNaga = SynapseLogReader.ReadProduct(168, "Razer Naga V2 Pro", DeviceKind.Mouse);
                if (wirelessNaga.UpdatedAt > naga.UpdatedAt) naga = wirelessNaga;
            }
            if (naga.IsOnline || naga.Level.HasValue) states.Add(naga);

            DeviceState kraken = SynapseLogReader.ReadProduct(1325, "Razer Kraken V3 Pro", DeviceKind.Headset);
            if (kraken.IsOnline || kraken.Level.HasValue) states.Add(kraken);

            DeviceState deathAdder = ReadDeathAdderLog();
            if (deathAdder != null && (deathAdder.IsOnline || deathAdder.Level.HasValue)) states.Add(deathAdder);

            states.AddRange(SteelSeriesHeadsetReader.ReadSupported());

            bool needDirectNaga = !states.Any(delegate(DeviceState s) { return s.IsOnline && s.Name.IndexOf("Naga", StringComparison.OrdinalIgnoreCase) >= 0; });
            bool needDirectDeathAdder = !states.Any(delegate(DeviceState s) { return s.IsOnline && s.Name.IndexOf("DeathAdder", StringComparison.OrdinalIgnoreCase) >= 0; });
            if (needDirectNaga || needDirectDeathAdder)
            {
                IList<DeviceState> direct = RazerMouseReader.ReadSupported();
                foreach (DeviceState item in direct)
                {
                    bool isNaga = item.Name.IndexOf("Naga", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isDeathAdder = item.Name.IndexOf("DeathAdder", StringComparison.OrdinalIgnoreCase) >= 0;
                    if ((isNaga && needDirectNaga) || (isDeathAdder && needDirectDeathAdder))
                    {
                        states.RemoveAll(delegate(DeviceState existing) { return existing.Name.IndexOf(isNaga ? "Naga" : "DeathAdder", StringComparison.OrdinalIgnoreCase) >= 0; });
                        states.Add(item);
                    }
                }
            }

            EnsurePlaceholder(states, "Razer Naga V2 Pro", "Naga", DeviceKind.Mouse);
            EnsurePlaceholder(states, "Razer DeathAdder V3 Pro", "DeathAdder", DeviceKind.Mouse);
            EnsurePlaceholder(states, "Razer Kraken V3 Pro", "Kraken", DeviceKind.Headset);
            EnsurePlaceholder(states, "SteelSeries Arctis Nova 7 Gen 2", "Arctis Nova 7", DeviceKind.Headset);
            states.Sort(delegate(DeviceState a, DeviceState b)
            {
                int kind = a.Kind.CompareTo(b.Kind);
                return kind != 0 ? kind : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return states;
        }

        private static DeviceState ReadDeathAdderLog()
        {
            int[] ids = { 182, 183, 194, 195 };
            DeviceState latest = null;
            foreach (int id in ids)
            {
                DeviceState state = SynapseLogReader.ReadProduct(id, "Razer DeathAdder V3 Pro", DeviceKind.Mouse);
                if (state.Level.HasValue && (latest == null || state.UpdatedAt > latest.UpdatedAt)) latest = state;
            }
            return latest;
        }

        private static void EnsurePlaceholder(List<DeviceState> states, string name, string family, DeviceKind kind)
        {
            if (states.Exists(delegate(DeviceState s) { return s.Name.IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0; })) return;
            states.Add(new DeviceState { Name = name, Kind = kind, IsOnline = false, Source = "oczekiwanie na urządzenie" });
        }
    }
}

