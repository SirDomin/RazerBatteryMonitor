using System;
using RazerBatteryMonitor;

internal static class ProtocolTests
{
    private static int failures;

    private static void Assert(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FAIL: " + message);
    }

    private static byte[] Reply(byte command, byte value)
    {
        byte[] packet = new byte[RazerProtocol.PacketLength];
        packet[0] = RazerProtocol.StatusOk;
        packet[1] = RazerProtocol.TransactionId;
        packet[5] = 2;
        packet[6] = 7;
        packet[7] = command;
        packet[8] = 0;
        packet[9] = value;
        packet[88] = RazerProtocol.Checksum(packet);
        return packet;
    }

    private static void Protocol()
    {
        byte[] request = RazerProtocol.BuildGetter(0x80);
        Assert(request.Length == 90, "request length");
        Assert(request[1] == 0x1f, "transaction id");
        Assert(request[5] == 2 && request[6] == 7 && request[7] == 0x80, "battery command");
        Assert(request[88] == RazerProtocol.Checksum(request), "request checksum");
        Assert(RazerProtocol.DecodeBattery(RazerProtocol.Decode(Reply(0x80, 128), 0x80)) == 50, "battery scale");
        Assert(RazerProtocol.DecodeCharging(RazerProtocol.Decode(Reply(0x84, 1), 0x84)), "charging flag");
        bool rejected = false;
        try { RazerProtocol.BuildGetter(0x02); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Assert(rejected, "unsafe command rejected");
    }

    private static void LogParsing()
    {
        string sample = "[2026/11/23 21:11:18.606] warn: ON_BC_EVENT: {\"type\":\"GET_BATTERY_STATE\",\"payload\":{\"chargingStatus\":\"Charging\",\"level\":70}}\r\n" +
                        "[2026/11/23 21:12:18.606] info: setBatteryState GET_BATTERY_STATE {\"chargingStatus\":\"NoCharge_BatteryFull\",\"level\":69}\r\n";
        SynapseLogReader.BatteryRecord record = SynapseLogReader.ParseLatest(sample);
        Assert(record != null, "log event found");
        Assert(record != null && record.Level == 69, "latest log level");
        Assert(record != null && record.Status == "NoCharge_BatteryFull", "latest charge status");

        string currentSynapse = "[2026/11/23 21:43:40.199] warn: [CHECK BATTERY STATUS] Result {\"chargingStatus\":\"Charging\",\"level\":70}\r\n";
        SynapseLogReader.BatteryRecord current = SynapseLogReader.ParseLatest(currentSynapse);
        Assert(current != null && current.Level == 70, "current Synapse battery event");
        Assert(current != null && current.Status == "Charging", "current Synapse charging status");
    }

    private static void SteelSeries()
    {
        byte[] request = SteelSeriesProtocol.BuildStatusRequest(64);
        Assert(request.Length == 64 && request[0] == 0 && request[1] == 0xB0, "SteelSeries read-only status request");

        SteelSeriesProtocol.Status charging = SteelSeriesProtocol.DecodeStatus(new byte[] { 0xB0, 3, 77, 1 }, 4);
        Assert(charging.IsConnected && charging.Level == 77 && charging.IsCharging, "SteelSeries charging status");

        SteelSeriesProtocol.Status battery = SteelSeriesProtocol.DecodeStatus(new byte[] { 0, 0xB0, 3, 55, 3 }, 5);
        Assert(battery.IsConnected && battery.Level == 55 && !battery.IsCharging, "SteelSeries Windows report prefix");

        SteelSeriesProtocol.Status offline = SteelSeriesProtocol.DecodeStatus(new byte[] { 0xB0, 0, 80, 0 }, 4);
        Assert(!offline.IsConnected, "SteelSeries disconnected status");
    }

    private static void UsbPresenceMatching()
    {
        Assert(UsbDevicePresence.MatchesInstanceId(@"USB\VID_1532&PID_052C&MI_04\7&ABC&0&0004", 0x1532, 0x052C), "Kraken USB instance match");
        Assert(UsbDevicePresence.MatchesInstanceId(@"hid\vid_1532&pid_052c\sample", 0x1532, 0x052C), "USB instance match is case-insensitive");
        Assert(!UsbDevicePresence.MatchesInstanceId(@"USB\VID_1532&PID_00B7\sample", 0x1532, 0x052C), "wrong product is rejected");
    }

    public static int Main()
    {
        Protocol();
        SteelSeries();
        LogParsing();
        UsbPresenceMatching();
        if (failures != 0) return 1;
        Console.WriteLine("All protocol and log parsing tests passed.");
        return 0;
    }
}
