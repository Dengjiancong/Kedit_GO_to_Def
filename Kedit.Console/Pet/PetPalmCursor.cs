using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Input;

namespace Kedit.Console {
    // Original vector palm, rasterized locally as a cursor; no global cursor replacement.
    internal static class PetPalmCursor {
        private static Cursor cursor;
        internal static Cursor Value {get {return cursor??(cursor=Create());}}
        private static Cursor Create() {
            using(var bitmap=new Bitmap(48,48))using(var g=Graphics.FromImage(bitmap))using(var path=new GraphicsPath()) {
                g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(1.5f,1.5f);
                path.AddBezier(10,29,8,24,3,19,3,16);path.AddBezier(3,16,3,13,5,13,7,16);
                path.AddLine(7,16,9,19);path.AddLine(9,19,8,8);path.AddBezier(8,8,8,4,12,4,12,8);
                path.AddLine(12,8,13,16);path.AddLine(13,16,13,4);path.AddBezier(13,4,13,0,17,0,17,4);
                path.AddLine(17,4,17,15);path.AddLine(17,15,18,6);path.AddBezier(18,6,18,2,22,3,22,6);
                path.AddLine(22,6,21,16);path.AddLine(21,16,23,10);path.AddBezier(23,10,24,6,27,8,26,11);
                path.AddBezier(26,11,25,17,27,23,22,29);path.CloseFigure();
                using(var fill=new SolidBrush(Color.FromArgb(255,247,231,213)))g.FillPath(fill,path);
                using(var pen=new Pen(Color.FromArgb(34,49,62),1.3f)){g.DrawPath(pen,path);g.DrawArc(pen,11,17,10,8,200,100);g.DrawLine(pen,11,26,21,26);}
                using(var png=new MemoryStream())using(var file=new MemoryStream()) {
                    bitmap.Save(png,ImageFormat.Png);var bytes=png.ToArray();
                    using(var writer=new BinaryWriter(file,System.Text.Encoding.UTF8,true)) {
                        writer.Write((ushort)0);writer.Write((ushort)2);writer.Write((ushort)1);
                        writer.Write((byte)48);writer.Write((byte)48);writer.Write((byte)0);writer.Write((byte)0);
                        writer.Write((ushort)24);writer.Write((ushort)22);writer.Write(bytes.Length);writer.Write(22);writer.Write(bytes);
                    }
                    file.Position=0;return new Cursor(file);
                }
            }
        }
    }
}
