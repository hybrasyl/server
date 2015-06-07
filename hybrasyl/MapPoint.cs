using C3;
using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Linq;
using hybrasyl.Util;
using Hybrasyl.Enums;

namespace Hybrasyl
{
    public class MapPoint
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Int64 Id { get; set; }
        public string Pointname { get; set; }
        public WorldMap Parent { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Name { get; set; }
        public ushort DestinationMap { get; set; }
        public byte DestinationX { get; set; }
        public byte DestinationY { get; set; }

        public int XOffset { get; set; }
        public int YOffset { get; set; }
        public int XQuadrant { get; set; }
        public int YQuadrant { get; set; }

        public MapPoint()
        {
        }

        public byte[] GetBytes()
        {
            var buffer = Encoding.GetEncoding(949).GetBytes(Name);
            Logger.DebugFormat("buffer is {0} and Name is {1}", BitConverter.ToString(buffer), Name);

            var bytes = new List<Byte>();

            Logger.DebugFormat("{0}, {1}, {2}, {3}, {4}, mappoint ID is {5}", XQuadrant, XOffset, YQuadrant,
                YOffset, Name.Length, Id);



            bytes.Add((byte)XQuadrant);
            bytes.Add((byte)XOffset);
            bytes.Add((byte)YQuadrant);
            bytes.Add((byte)YOffset);
            bytes.Add((byte)Name.Length);
            bytes.AddRange(buffer);
            bytes.AddRange(BitConverter.GetBytes(Id));

            return bytes.ToArray();
        }
    }
}
