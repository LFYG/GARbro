//! \file       ImageAKB.cs
//! \date       Sat Nov 28 13:09:20 2015
//! \brief      Ai6Win engine image format.
//
// Copyright (C) 2015 by morkt
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

using GameRes.Compression;
using GameRes.Utility;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Media;

namespace GameRes.Formats.Silky
{
    internal class AkbMetaData : ImageMetaData
    {
        public byte[]   Background;
        public int      InnerWidth;
        public int      InnerHeight;
    }

    [Export(typeof(ImageFormat))]
    public class AkbFormat : ImageFormat
    {
        public override string         Tag { get { return "AKB"; } }
        public override string Description { get { return "AI6WIN engine image format"; } }
        public override uint     Signature { get { return 0x20424B41; } } // 'AKB '

        public override ImageMetaData ReadMetaData (Stream stream)
        {
            using (var reader = new ArcView.Reader (stream))
            {
                reader.ReadInt32();
                var info = new AkbMetaData();
                info.Width = reader.ReadUInt16();
                info.Height = reader.ReadUInt16();
                int flags = reader.ReadInt32() & 0xFFFF;
                info.BPP = 0 == flags ? 32 : 24;
                info.Background = reader.ReadBytes (4);
                info.OffsetX = reader.ReadInt32();
                info.OffsetY = reader.ReadInt32();
                info.InnerWidth = reader.ReadInt32() - info.OffsetX;
                info.InnerHeight = reader.ReadInt32() - info.OffsetY;
                if (info.InnerWidth > info.Width || info.InnerHeight > info.Height)
                    return null;
                return info;
            }
        }

        public override ImageData Read (Stream stream, ImageMetaData info)
        {
            var meta = (AkbMetaData)info;
            int pixel_size = meta.BPP / 8;
            int stride = (int)meta.Width * pixel_size;
            byte[] image;
            if (meta.InnerWidth != 0 && meta.InnerHeight != 0)
            {
                stream.Position = 0x20;
                int inner_stride = meta.InnerWidth * pixel_size;
                var pixels = new byte[meta.InnerHeight * inner_stride];
                using (var lz = new LzssStream (stream, LzssMode.Decompress, true))
                {
                    for (int dst = pixels.Length - inner_stride; dst >= 0; dst -= inner_stride)
                    {
                        if (inner_stride != lz.Read (pixels, dst, inner_stride))
                            throw new InvalidFormatException();
                    }
                }
                int src = 0;
                for (int i = pixel_size; i < inner_stride; ++i)
                    pixels[i] += pixels[src++];
                src = 0;
                for (int i = inner_stride; i < pixels.Length; ++i)
                    pixels[i] += pixels[src++];
                if (meta.InnerWidth != meta.Width || meta.InnerHeight != meta.Height)
                {
                    image = CreateBackground (meta);
                    src = 0;
                    int dst = meta.OffsetY * stride + meta.OffsetX * pixel_size;
                    for (int y = 0; y < meta.InnerHeight; ++y)
                    {
                        Buffer.BlockCopy (pixels, src, image, dst, inner_stride);
                        dst += stride;
                        src += inner_stride;
                    }
                }
                else
                {
                    image = pixels;
                }
            }
            else
            {
                image = CreateBackground (meta);
            }
            var format = 24 == meta.BPP ? PixelFormats.Bgr24 : PixelFormats.Bgra32;
            return ImageData.Create (info, format, null, image, stride);
        }

        private byte[] CreateBackground (AkbMetaData info)
        {
            int pixel_size = info.BPP / 8;
            int stride = (int)info.Width * pixel_size;
            var pixels = new byte[stride * (int)info.Height];
            if (0 != LittleEndian.ToInt32 (info.Background, 0))
            {
                for (int i = 0; i < stride; i += pixel_size)
                    Buffer.BlockCopy (info.Background, 0, pixels, i, pixel_size);
                Binary.CopyOverlapped (pixels, 0, stride, pixels.Length-stride);
            }
            return pixels;
        }

        public override void Write (Stream file, ImageData image)
        {
            throw new System.NotImplementedException ("AkbFormat.Write not implemented");
        }
    }
}
