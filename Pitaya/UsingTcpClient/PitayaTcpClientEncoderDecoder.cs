namespace Pitaya.NativeImpl {
    public static class EncoderDecoder {
        private static (int, uint) Forward(ref MemoryStream buf) {
            byte[] header = new byte[PitayaGoToCSConstants.HeadLength];
            buf.Read(header, 0, PitayaGoToCSConstants.HeadLength);
            
            Utils.NextMemoryStream(ref buf, header.Length);
            
            return Utils.ParseHeader(header);
        }

        public static Packet[] DecodePacket(byte[] data) {
            MemoryStream buf = new MemoryStream(data);

            List<Packet> packets = new List<Packet>();
            // check length
            if (buf.Length < PitayaGoToCSConstants.HeadLength) {
                return null;
            }

            // first time
            (int size, uint typ) = Forward(ref buf);

            while (size <= buf.Length) {
                byte[] packetData = new byte[size];
                buf.Read(packetData, 0, size);
                
                Utils.NextMemoryStream(ref buf, size);

                Packet p = new Packet{Type=typ, Length=size, Data=packetData};
                packets.Add(p);

                // if no more packets, break
                if (buf.Length < PitayaGoToCSConstants.HeadLength) {
                    break;
                }

                (size, typ) = Forward(ref buf);
                if (size == 0 && typ == 0x00) {
                    return null;
                }
            }

            return packets.ToArray();
        }


        public static byte[] EncodePacket(uint typ, byte[] data) {
            if (typ < PitayaGoToCSConstants.Handshake || typ > PitayaGoToCSConstants.Kick) {
                return null;
                // return nil, packet.ErrWrongPomeloPacketType
            }

            if (data.Length > PitayaGoToCSConstants.MaxPacketSize) {
                return null;
                // return nil, ErrPacketSizeExcced
            }

            Packet p = new Packet(){Type=typ, Length=data.Length};
            byte[] buf = new byte[p.Length+PitayaGoToCSConstants.HeadLength];
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

        public static byte[] EncodeMsg(Request message) {
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

            if (message.Type == PitayaGoToCSConstants.Request) {
                ulong n = message.Id;
                byte b;
                // variant length encode
                while (true) {
                    b = (byte)(n % 128);
                    n >>= 7;
                    if (n != 0) {
                        buf.Add((byte)(b+128));
                    } else {
                        buf.Add(b);
                        break;
                    }
                }
            }

            if (message.Type == PitayaGoToCSConstants.Request || message.Type == PitayaGoToCSConstants.Notify || message.Type == PitayaGoToCSConstants.Push) {
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
    }
}