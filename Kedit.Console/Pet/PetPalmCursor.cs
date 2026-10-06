using System.Windows.Input;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Kedit.Console {
    internal static class PetPalmCursor {
        private static Cursor cursor;
        internal static Cursor Value { get { return cursor ?? (cursor=Create()); } }
        private static Cursor Create() {
            // Open palm: original outline, matching the requested five-finger silhouette.
            using(var bitmap=new Bitmap(48,48))using(var g=Graphics.FromImage(bitmap))using(var p=new GraphicsPath()) {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                p.StartFigure();
                p.AddBezier(14,24,12,21,10,18,8,19);
                p.AddBezier(8,19,5,20,6,23,7,25);
                p.AddLine(7,25,14,38);
                p.AddBezier(14,38,17,44,22,46,29,45);
                p.AddBezier(29,45,38,45,42,39,42,32);
                p.AddLine(42,32,42,17);
                p.AddBezier(42,17,42,12,36,12,36,17);
                p.AddLine(36,17,36,25);
                p.AddLine(36,25,36,11);
                p.AddBezier(36,11,36,6,30,6,30,11);
                p.AddLine(30,11,30,24);
                p.AddLine(30,24,30,7);
                p.AddBezier(30,7,30,2,23,2,23,7);
                p.AddLine(23,7,23,24);
                p.AddLine(23,24,23,10);
                p.AddBezier(23,10,23,5,16,5,16,10);
                p.AddLine(16,10,16,25);
                p.AddLine(16,25,14,24);p.CloseFigure();
                using(var fill=new SolidBrush(Color.White))g.FillPath(fill,p);
                using(var pen=new Pen(Color.FromArgb(32,30,30),1.8f)){pen.LineJoin=LineJoin.Round;pen.StartCap=pen.EndCap=LineCap.Round;g.DrawPath(pen,p);}
                using(var png=new MemoryStream())using(var file=new MemoryStream()) {
                    bitmap.Save(png,ImageFormat.Png);var bytes=png.ToArray();
                    using(var writer=new BinaryWriter(file,System.Text.Encoding.UTF8,true)) {
                        writer.Write((ushort)0);writer.Write((ushort)2);writer.Write((ushort)1);
                        writer.Write((byte)48);writer.Write((byte)48);writer.Write((byte)0);writer.Write((byte)0);
                        writer.Write((ushort)25);writer.Write((ushort)26);writer.Write(bytes.Length);writer.Write(22);writer.Write(bytes);
                    }
                    file.Position=0;return new Cursor(file);
                }
            }
        }
    }
}
