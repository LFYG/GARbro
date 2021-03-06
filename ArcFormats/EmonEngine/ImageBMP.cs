//! \file       ImageBMP.cs
//! \date       Wed Mar 16 01:08:47 2016
//! \brief      Emon Engine compressed images.
//
// Copyright (C) 2016 by morkt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameRes.Compression;

namespace GameRes.Formats.EmonEngine
{
    internal class EmMetaData : ImageMetaData
    {
        public int Colors;
        public int Stride;
        public int LzssFrameSize;
        public int LzssInitPos;
        public int DataOffset;
    }

    [Export(typeof(ImageFormat))]
    public class EmFormat : ImageFormat
    {
        public override string         Tag { get { return "EM/BMP"; } }
        public override string Description { get { return "Emon Engine image format"; } }
        // this is an artificial prefix embedded into stream by EmeOpener.OpenImage
        public override uint     Signature { get { return 0x4D424D45; } } // 'EMBM'

        public EmFormat ()
        {
            Extensions = new string[] { "bmp" };
        }

        public override ImageMetaData ReadMetaData (IBinaryStream stream)
        {
            stream.ReadInt32();
            var info = new EmMetaData();
            info.LzssFrameSize = stream.ReadUInt16();
            info.LzssInitPos = stream.ReadUInt16();
            info.BPP = stream.ReadUInt16() & 0xFF;
            info.Width = stream.ReadUInt16();
            info.Height = stream.ReadUInt16();
            info.Colors = stream.ReadUInt16();
            info.Stride = stream.ReadInt32();
            info.OffsetX = stream.ReadInt32();
            info.OffsetY = stream.ReadInt32();
            info.DataOffset = 40;
            return info;
        }

        public override ImageData Read (IBinaryStream stream, ImageMetaData info)
        {
            var meta = (EmMetaData)info;
            stream.Position = meta.DataOffset;
            BitmapPalette palette = null;
            if (meta.Colors != 0)
                palette = ReadPalette (stream.AsStream, Math.Max (meta.Colors, 3), PaletteFormat.RgbX);
            var pixels = new byte[meta.Stride * (int)info.Height];
            if (meta.LzssFrameSize != 0)
            {
                using (var lzss = new LzssStream (stream.AsStream, LzssMode.Decompress, true))
                {
                    lzss.Config.FrameSize = meta.LzssFrameSize;
                    lzss.Config.FrameInitPos = meta.LzssInitPos;
                    if (pixels.Length != lzss.Read (pixels, 0, pixels.Length))
                        throw new EndOfStreamException();
                }
            }
            else
            {
                if (pixels.Length != stream.Read (pixels, 0, pixels.Length))
                    throw new EndOfStreamException();
            }
            if (7 == meta.BPP)
                return ImageData.Create (info, PixelFormats.Gray8, palette, pixels, meta.Stride);

            PixelFormat format;
            if (32 == meta.BPP)
                format = PixelFormats.Bgr32;
            else if (24 == meta.BPP)
                format = PixelFormats.Bgr24;
            else
                format = PixelFormats.Indexed8;
            return ImageData.CreateFlipped (info, format, palette, pixels, meta.Stride);
        }

        public override void Write (Stream file, ImageData image)
        {
            throw new System.NotImplementedException ("EmFormat.Write not implemented");
        }
    }
}
