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
                return null;
                // return nil, packet.ErrWrongPomeloPacketType
            }

            if (data.Length > PitayaGoToCSConstants.MaxPacketSize)
            {
                return null;
                // return nil, ErrPacketSizeExcced
            }

            Packet p = new Packet() { Type = typ, Length = data.Length };
            byte[] buf = new byte[p.Length + PitayaGoToCSConstants.HeadLength];
            buf[0] = (byte)(p.Type);

            byte[] source = Utils.IntToBytes(p.Length);

            Array.Copy(source, 0, buf, 1, PitayaGoToCSConstants.HeadLength - 1);

            Array.Copy(data, 0, buf, PitayaGoToCSConstants.HeadLength, data.Length);


            // Array.Copy(IntToBytes(p.Length), 0, buf, 1, PitayaGoToCSConstants.HeadLength);
            // Array.Copy(data, 0, buf, PitayaGoToCSConstants.HeadLength, data.Length);

            // copy(buf[1:HeadLength], IntToBytes(p.Length))
            // copy(buf[HeadLength:], data)

            return buf;
        }

        public static byte[] EncodeMsg(Request message)
        {
            // if InvalidType(message.Type) {
            //     return nil, ErrWrongMessageType
            // }

            List<byte> buf = new List<byte>();
            byte flag = (byte)(message.Type << 1);

            // routesCodesMutex.RLock()
            // code, compressed := routes[message.Route]
            // routesCodesMutex.RUnlock()
            // if compressed {
            //     flag |= msgRouteCompressMask
            // }

            // if message.Err {
            //     flag |= errorMask
            // }

            buf.Add(flag);

            if (message.Type == PitayaGoToCSConstants.Request)
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

            if (message.Type == PitayaGoToCSConstants.Request || message.Type == PitayaGoToCSConstants.Notify || message.Type == PitayaGoToCSConstants.Push)
            {
                buf.Add((byte)message.Route.Length);
                buf.AddRange(System.Text.Encoding.UTF8.GetBytes(message.Route));
            }

            // if (DataCompression) {
            //TODO 

            // d, err := compression.De flateData(message.Data)
            // if err != nil {
            //     return nil, err
            // }

            // if len(d) < len(message.Data) {
            //     message.Data = d
            //     buf[0] |= gzipMask
            // }
            // }

            buf.AddRange(message.Data);
            return buf.ToArray();
        }
        public static Request DecodeMsg(byte[] data)
        {
            /*if len(data) < msgHeadLength {
                return nil, ErrInvalidMessage
            }*/

            Request message = new Request();
            byte flag = data[0];

            int offset = 1;

            // it's a cast to Type in golang
            message.Type = (byte)((flag >> 1) & PitayaGoToCSConstants.msgTypeMask);

            //if invalidType(message.Type) {
            //    return nil, ErrWrongMessageType
            //}

            if (message.Type == PitayaGoToCSConstants.Request || message.Type == PitayaGoToCSConstants.Response)
            {
                ulong id = 0;
                // little end byte order
                // WARNING: must can be stored in 64 bits integer
                // variant length encode
                for (int i = offset; i < data.Length; i++)
                {
                    byte b = data[i];
                    id += (ulong)(b & 0x7F) << (7 * (i - offset));
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

            //routable
            if (message.Type == PitayaGoToCSConstants.Request || message.Type == PitayaGoToCSConstants.Notify || message.Type == PitayaGoToCSConstants.Push)
            {
                if ((flag & PitayaGoToCSConstants.msgRouteCompressMask) == 1)
                {
                    //if offset > size || (offset+2) > size {
                    //   return nil, ErrInvalidMessage
                    //}

                    // message.compressed = true

                    // The line below should be the same as: code := binary.BigEndian.Uint16(data[offset:(offset + 2)])
                    ushort code = (ushort)((data[offset] << 8) | data[offset + 1]);

                    //routesCodesMutex.RLock()
                    //route, ok := codes[code]
                    //routesCodesMutex.RUnlock()
                    //if !ok {
                    //    return nil, ErrRouteInfoNotFound
                    //}
                    //message.Route = route
                    offset += 2;
                }
                else
                {
                    //m.compressed = false
                    byte rl = data[offset];
                    offset++;

                    // if offset > size || (offset+int(rl)) > size {
                    //    return nil, ErrInvalidMessage
                    //}
                    message.Route = System.Text.Encoding.UTF8.GetString(data, offset, offset + rl);
                    offset += rl;
                }
            }

            //if offset > size {
            //    return nil, ErrInvalidMessage
            //}

            byte[] decodedMessageData = new byte[data.Length - offset];
            Array.Copy(data, offset, decodedMessageData, 0, decodedMessageData.Length);
            message.Data = decodedMessageData;

            if ((flag & PitayaGoToCSConstants.gzipMask) == PitayaGoToCSConstants.gzipMask)
            {
                //m.Data, err = compression.InflateData(m.Data)
                //if err != nil {
                //    return nil, err
                //}
            }

            return message;
        }
    }

}