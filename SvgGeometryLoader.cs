using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace Toolbox
{
    /// <summary>
    /// 把内置 IconPack 的 SVG（单色 currentColor）解析成 WPF Geometry，方便用 Fill 随主题染色。
    /// 支持 path / rect / circle / ellipse；transform、stroke 等高级特性暂不处理（游戏图标包均为扁平填充）。
    /// </summary>
    public static class SvgGeometryLoader
    {
        public static Geometry LoadGeometry(string rel)
        {
            if (string.IsNullOrWhiteSpace(rel)) return null;
            try
            {
                var encRel = string.Join("/", rel.Split('/').Select(p => Uri.EscapeDataString(p)));
                var uri = new Uri("pack://application:,,,/Toolbox;component/IconPack/" + encRel, UriKind.Absolute);
                var info = Application.GetResourceStream(uri);
                if (info == null) return null;
                using (var sr = new StreamReader(info.Stream))
                {
                    var doc = XDocument.Parse(sr.ReadToEnd());
                    var group = new GeometryGroup { FillRule = FillRule.Nonzero };
                    foreach (var el in doc.Descendants())
                    {
                        string local = el.Name.LocalName;
                        try
                        {
                            if (local == "path")
                            {
                                var d = el.Attribute("d")?.Value;
                                if (!string.IsNullOrWhiteSpace(d))
                                    group.Children.Add(Geometry.Parse(d));
                            }
                            else if (local == "rect")
                                group.Children.Add(ParseRect(el));
                            else if (local == "circle")
                                group.Children.Add(ParseCircle(el));
                            else if (local == "ellipse")
                                group.Children.Add(ParseEllipse(el));
                        }
                        catch { /* 单个形状解析失败忽略，继续下一个 */ }
                    }
                    return group.Bounds.IsEmpty ? null : group;
                }
            }
            catch { return null; }
        }

        private static Geometry ParseRect(XElement el)
        {
            double x = ParseDouble(el.Attribute("x")?.Value);
            double y = ParseDouble(el.Attribute("y")?.Value);
            double w = ParseDouble(el.Attribute("width")?.Value);
            double h = ParseDouble(el.Attribute("height")?.Value);
            double rx = ParseDouble(el.Attribute("rx")?.Value, -1);
            if (rx > 0) return new RectangleGeometry(new Rect(x, y, w, h), rx, rx);
            return new RectangleGeometry(new Rect(x, y, w, h));
        }

        private static Geometry ParseCircle(XElement el)
        {
            double cx = ParseDouble(el.Attribute("cx")?.Value);
            double cy = ParseDouble(el.Attribute("cy")?.Value);
            double r = ParseDouble(el.Attribute("r")?.Value);
            return new EllipseGeometry(new Point(cx, cy), r, r);
        }

        private static Geometry ParseEllipse(XElement el)
        {
            double cx = ParseDouble(el.Attribute("cx")?.Value);
            double cy = ParseDouble(el.Attribute("cy")?.Value);
            double rx = ParseDouble(el.Attribute("rx")?.Value);
            double ry = ParseDouble(el.Attribute("ry")?.Value);
            return new EllipseGeometry(new Point(cx, cy), rx, ry);
        }

        private static double ParseDouble(string s, double def = 0)
        {
            if (string.IsNullOrWhiteSpace(s)) return def;
            return double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
        }
    }
}
