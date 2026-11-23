using System;

namespace RazerBatteryMonitor
{
    internal static class RazerProtocol
    {
        internal const int PacketLength = 90;
        internal const byte TransactionId = 0x1f;
        internal const byte StatusBusy = 0x01;
        internal const byte StatusOk = 0x02;
        internal const byte StatusUnsupported = 0x05;

        internal static byte[] BuildGetter(byte commandId)
        {
            if (commandId != 0x80 && commandId != 0x84)
                throw new ArgumentOutOfRangeException("commandId", "Only the battery and charging getter commands are allowed.");
            byte[] packet = new byte[PacketLength];
            packet[1] = TransactionId;
            packet[5] = 0x02;
            packet[6] = 0x07;
            packet[7] = commandId;
            packet[88] = Checksum(packet);
            return packet;
        }

        internal static byte Checksum(byte[] packet)
        {
            if (packet == null || packet.Length < 89) throw new ArgumentException("Razer packet is too short.");
            byte checksum = 0;
            for (int i = 2; i < 88; i++) checksum ^= packet[i];
            return checksum;
        }

        internal static byte[] Decode(byte[] packet, byte commandId)
        {
            if (packet == null || packet.Length != PacketLength) throw new InvalidOperationException("Invalid Razer reply length.");
            if (packet[88] != Checksum(packet)) throw new InvalidOperationException("Invalid Razer reply checksum.");
            if (packet[1] != TransactionId) throw new InvalidOperationException("A reply for a different Razer transaction was received.");
            if (packet[0] == StatusUnsupported) throw new NotSupportedException("The device does not support this battery query.");
            if (packet[0] != StatusOk) throw new InvalidOperationException(string.Format("Razer device returned status 0x{0:X2}.", packet[0]));
            if (packet[6] != 0x07 || packet[7] != commandId) throw new InvalidOperationException("A stale Razer reply was received.");
            int length = Math.Min(packet[5], (byte)82);
            byte[] data = new byte[length];
            Buffer.BlockCopy(packet, 8, data, 0, length);
            return data;
        }

        internal static int DecodeBattery(byte[] arguments)
        {
            if (arguments == null || arguments.Length < 2) throw new InvalidOperationException("Battery reply has no value.");
            return (int)Math.Round(arguments[1] * 100.0 / 255.0, MidpointRounding.AwayFromZero);
        }

        internal static bool DecodeCharging(byte[] arguments)
        {
            if (arguments == null || arguments.Length < 2) throw new InvalidOperationException("Charging reply has no value.");
            return arguments[1] == 1;
        }
    }
}

