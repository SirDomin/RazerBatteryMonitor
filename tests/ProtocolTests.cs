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

    public static int Main()
    {
        Protocol();
        LogParsing();
        if (failures != 0) return 1;
        Console.WriteLine("All protocol and log parsing tests passed.");
        return 0;
    }
}
