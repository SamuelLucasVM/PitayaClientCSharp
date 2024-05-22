using System.IO;
using System;
using System.Collections.Generic;

namespace Pitaya.NativeImpl
{
    public static class EncoderDecoder
    {
        private static (int, uint) Forward(ref MemoryStream buf)
        {
            byte[] header = new byte[PitayaGoToCSConstants.HeadLength];
            buf.Read(header, 0, PitayaGoToCSConstants.HeadLength);

            Utils.NextMemoryStream(ref buf, header.Length);

            return Utils.ParseHeader(header);
        }

        public static Packet[] DecodePacket(byte[] data)
        {
            MemoryStream buf = new MemoryStream(data);

            List<Packet> packets = new List<Packet>();
            // check length
            if (buf.Length < PitayaGoToCSConstants.HeadLength)
            {
                return null;
            }

            // first time
            (int size, uint typ) = Forward(ref buf);

            while (size <= buf.Length)
            {
                byte[] packetData = new byte[size];
                buf.Read(packetData, 0, size);

                Utils.NextMemoryStream(ref buf, size);

                Packet p = new Packet { Type = typ, Length = size, Data = packetData };
                packets.Add(p);

                // if no more packets, break
                if (buf.Length < PitayaGoToCSConstants.HeadLength)
                {
                    break;
                }

                (size, typ) = Forward(ref buf);
                if (size == 0 && typ == 0x00)
                {
                    return null;
                }
            }

            return packets.ToArray();
        }


        public static byte[] EncodePacket(uint typ, byte[] data)
        {
            if (typ < PitayaGoToCSConstants.Handshake || typ > PitayaGoToCSConstants.Kick)
            {
                throw new ErrWrongPomeloPacketType();
            }

            if (data.Length > PitayaGoToCSConstants.MaxPacketSize)
            {
                throw new ErrPacketSizeExcced();
            }

            Packet p = new Packet() { Type = typ, Length = data.Length };
            byte[] buf = new byte[p.Length + PitayaGoToCSConstants.HeadLength];
            buf[0] = (byte)(p.Type);

            byte[] source = Utils.IntToBytes(p.Length);

            Array.Copy(source, 0, buf, 1, PitayaGoToCSConstants.HeadLength - 1);

            Array.Copy(data, 0, buf, PitayaGoToCSConstants.HeadLength, data.Length);

            return buf;
        }

        public static byte[] EncodeMsg(Message message)
        {
            if (InvalidType(message.Type)) {
                throw new ErrWrongMessageType();
            }

            List<byte> buf = new List<byte>();
            byte flag = (byte)(message.Type << 1);

            bool compressed = false;

            ushort code = 0;
            lock(RoutesCodesManager.routesCodesLock){
                if(RoutesCodesManager.routes.TryGetValue(message.Route, out ushort c)){
                    code = c;
                    compressed = true;
                }
            }

            if (compressed) {
                flag |= PitayaGoToCSConstants.msgRouteCompressMask;
            }

            if (message.Err) {
                flag |= PitayaGoToCSConstants.errorMask;
            }

            buf.Add(flag);

            if (message.Type == PitayaGoToCSConstants.Request || message.Type == PitayaGoToCSConstants.Response)
            {
                ulong n = message.Id;
                byte b;
                // variant length encode
                while (true)
                {
                    b = (byte)(n % 128);
                    n >>= 7;
                    if (n != 0)
                    {
                        buf.Add((byte)(b + 128));
                    }
                    else
                    {
                        buf.Add(b);
                        break;
                    }
                }
            }

            if (Routable(message.Type))
            {
                if (compressed) {
                    buf.Add((byte)((code>>8)&0xFF));
                    buf.Add((byte)(code&0xFF));
                } else {
                    buf.Add((byte)message.Route.Length);
                    buf.AddRange(System.Text.Encoding.UTF8.GetBytes(message.Route));
                }
            }

            buf.AddRange(message.Data);
            return buf.ToArray();
        }
        public static Message DecodeMsg(byte[] data)
        {
            if (data.Length < PitayaGoToCSConstants.msgHeadLength)
            {
                throw new ErrInvalidMessage();
            }

            Message message = new Message();
            byte flag = data[0];

            int offset = 1;

            // it's a cast to Type in golang
            message.Type = (byte)((flag >> 1) & PitayaGoToCSConstants.msgTypeMask);

            // Func invalidType
            if (message.Type < PitayaGoToCSConstants.Request || message.Type > PitayaGoToCSConstants.Push)
            {
                throw new ErrWrongMessageType();
            }

            if (message.Type == PitayaGoToCSConstants.Request || message.Type == PitayaGoToCSConstants.Response)
            {
                // uint in Golang is either 32 or 64 bits
                // uint here is just 32 bits
                uint id = 0;
                // little end byte order
                // WARNING: must can be stored in 64 bits integer
                // variant length encode
                for (int i = offset; i < data.Length; i++)
                {
                    byte b = data[i];
                    id += (uint)(b & 0x7F) << (7 * (i - offset));
                    if (b < 128)
                    {
                        offset = i + 1;
                        break;
                    }
                }
                message.Id = id;
            }

            message.Err = (flag & PitayaGoToCSConstants.errorMask) == PitayaGoToCSConstants.errorMask;

            int size = data.Length;

            //func routable
            if (message.Type == PitayaGoToCSConstants.Request || message.Type == PitayaGoToCSConstants.Notify || message.Type == PitayaGoToCSConstants.Push)
            {
                if ((flag & PitayaGoToCSConstants.msgRouteCompressMask) == 1)
                {
                    if (offset > size || (offset + 2) > size)
                    {
                        throw new ErrInvalidMessage();
                    }

                    message.Compressed = true;

                    // The line below should be the same as: code := binary.BigEndian.Uint16(data[offset:(offset + 2)])
                    ushort code = (ushort)((data[offset] << 8) | data[offset + 1]);

                    lock (RoutesCodesManager.routesCodesLock)
                    {
                        if (RoutesCodesManager.codes.TryGetValue(code, out string route))
                        {
                            message.Route = route;
                        }
                        else
                        {
                            throw new ErrRouteInfoNotFound();
                        }
                    }
                    offset += 2;
                }
                else
                {
                    message.Compressed = false;
                    byte rl = data[offset];
                    offset++;

                    if (offset > size || (offset + rl) > size)
                    {
                        throw new ErrInvalidMessage();
                    }
                    message.Route = System.Text.Encoding.UTF8.GetString(data, offset, offset + rl);
                    offset += rl;
                }
            }

            if (offset > size)
            {
                throw new ErrInvalidMessage();
            }

            byte[] decodedMessageData = new byte[data.Length - offset];
            Array.Copy(data, offset, decodedMessageData, 0, decodedMessageData.Length);
            message.Data = decodedMessageData;

            if ((flag & PitayaGoToCSConstants.gzipMask) == PitayaGoToCSConstants.gzipMask)
            {
                message.Data = Compression.InflateData(message.Data);
            }

            return message;
        }

        static bool InvalidType(byte t) {
            return t < PitayaGoToCSConstants.Request || t > PitayaGoToCSConstants.Push;
        }

        static bool Routable(byte t) {
            return t == PitayaGoToCSConstants.Request || t == PitayaGoToCSConstants.Notify || t == PitayaGoToCSConstants.Push;
        }
    }

}