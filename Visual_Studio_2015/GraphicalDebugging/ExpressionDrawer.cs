﻿//------------------------------------------------------------------------------
// <copyright file="ExpressionDrawer.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace GraphicalDebugging
{
    class ExpressionDrawer
    {
        public class Settings
        {
            public Settings()
                : this(Color.Black)
            { }

            public Settings(Color color, bool showDir = false, bool showLabels = false)
            {
                this.color = color;
                this.showDir = showDir;
                this.showLabels = showLabels;
            }

            public Color color;
            public bool showDir;
            public bool showLabels;
        }

        public interface IDrawable
        {
            void Draw(Geometry.Box box, Graphics graphics);
            void Draw(Geometry.Box box, Graphics graphics, Settings settings);
            Geometry.Box Aabb { get; }
        }

        public class Point : Geometry.Point, IDrawable
        {
            public Point(double x, double y)
                : base(x, y)
            {}

            public static Point Load(Debugger debugger, string name)
            {
                string name_prefix = "(double)" + name;
                Expression expr_x = debugger.GetExpression(name_prefix + "[0]");
                Expression expr_y = debugger.GetExpression(name_prefix + "[1]");

                double x = double.Parse(expr_x.Value, System.Globalization.CultureInfo.InvariantCulture);
                double y = double.Parse(expr_y.Value, System.Globalization.CultureInfo.InvariantCulture);
                
                Point result = new Point(x, y);
                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                this.Draw(box, graphics, new Settings(Color.Orange));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));

                float x = cs.ConvertX(this[0]);
                float y = cs.ConvertY(this[1]);
                graphics.FillEllipse(brush, x - 2.5f, y - 2.5f, 5, 5);
                graphics.DrawEllipse(pen, x - 2.5f, y - 2.5f, 5, 5);
            }

            public Geometry.Box Aabb { get { return new Geometry.Box(this, this); } }
        }

        public class Box : Geometry.Box, IDrawable
        {
            public Box(Point min, Point max)
                : base(min, max)
            {}

            public static Box Load(Debugger debugger, string name, string first = "m_min_corner", string second = "m_max_corner", bool intervals = false)
            {
                Box result;

                Point first_p = Point.Load(debugger, name + "." + first);
                Point second_p = Point.Load(debugger, name + "." + second);

                if (!intervals)
                {
                    result = new Box(first_p, second_p);
                }
                else
                {
                    result = new Box(new Point(first_p[0], second_p[0]),
                                     new Point(first_p[1], second_p[1]));
                }
                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                this.Draw(box, graphics, new Settings(Color.Red));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));

                float rx = cs.ConvertX(min[0]);
                float ry = cs.ConvertY(max[1]);
                float rw = cs.ConvertDimension(Width);
                float rh = cs.ConvertDimension(Height);

                if (rw == 0 && rh == 0)
                {
                    graphics.FillEllipse(brush, rx - 2.5f, ry - 2.5f, 5, 5);
                    graphics.DrawEllipse(pen, rx - 2.5f, ry - 2.5f, 5, 5);
                }
                else if (rw == 0 || rh == 0)
                {
                    graphics.DrawLine(pen, rx, ry, rx + rw, ry + rh);
                }
                else
                {
                    graphics.FillRectangle(brush, rx, ry, rw, rh);
                    graphics.DrawRectangle(pen, rx, ry, rw, rh);
                }
            }

            public Geometry.Box Aabb { get { return this; } }
        }

        public class Segment : Geometry.Segment, IDrawable
        {
            public static Segment Load(Debugger debugger, string name, string first = "first", string second = "second")
            {
                Segment result = new Segment();

                result.p0 = Point.Load(debugger, name + "." + first);
                result.p1 = Point.Load(debugger, name + "." + second);

                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                this.Draw(box, graphics, new Settings(Color.YellowGreen));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);

                PointF p0 = cs.Convert(this[0]);
                PointF p1 = cs.Convert(this[1]);
                graphics.DrawLine(pen, p0, p1);

                if (settings.showDir)
                    DrawDir(p0, p1, graphics, pen);
            }

            public Geometry.Box Aabb{ get { return Envelope(); } }
        }

        public class Linestring : Geometry.Linestring, IDrawable
        {
            public static Linestring Load(Debugger debugger, string name)
            {
                Linestring result = new Linestring();
                result.box = Geometry.Box.Inverted();

                int size = LoadSize(debugger, name);
                for (int i = 0; i < size; ++i)
                {
                    Point p = Point.Load(debugger, name + "[" + i + "]");
                    result.Add(p);
                    result.box.Expand(p);
                }

                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                this.Draw(box, graphics, new Settings(Color.Green));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);

                for (int i = 1; i < Count; ++i)
                {
                    PointF p0 = cs.Convert(this[i - 1]);
                    PointF p1 = cs.Convert(this[i]);
                    graphics.DrawLine(pen, p0, p1);

                    if (settings.showDir)
                        DrawDir(p0, p1, graphics, pen);
                }
            }

            public Geometry.Box Aabb { get { return box; } }
            
            private Geometry.Box box;
        }

        public class Ring : Geometry.Ring, IDrawable
        {
            public static Ring Load(Debugger debugger, string name, string member = "")
            {
                string name_suffix = member.Length > 0 ? "." + member : "";
                Linestring ls = Linestring.Load(debugger, name + name_suffix);

                Ring result = new Ring();
                result.linestring = ls;
                result.box = ls.Aabb;
                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                this.Draw(box, graphics, new Settings(Color.SlateBlue));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                LocalCS cs = new LocalCS(box, graphics);

                PointF[] dst_points = cs.Convert(this);

                if (dst_points != null)
                {
                    Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                    SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));
                    
                    graphics.FillPolygon(brush, dst_points);
                    graphics.DrawPolygon(pen, dst_points);

                    if (settings.showDir)
                        DrawDir(dst_points, true, graphics, pen);
                }
            }

            public Geometry.Box Aabb { get { return box; } }

            private Geometry.Box box;
        }

        public class Polygon : Geometry.Polygon, IDrawable
        {
            public static Polygon Load(Debugger debugger, string name, string outer, string inners, bool inners_in_list = false, string ring_member = "")
            {
                Polygon result = new Polygon();

                Ring r = Ring.Load(debugger, name + "." + outer, ring_member);

                result.box = Geometry.Box.Inverted();
                result.box.Expand(r.Aabb);

                result.outer = r;

                string name_inners = name + "." + inners;
                int inners_size = LoadSize(debugger, name_inners);
                // ugly temporary solution
                string next_node = name_inners + "._Mypair._Myval2._Myhead";
                for (int i = 0; i < inners_size; ++i)
                {
                    Expression e1 = debugger.GetExpression(next_node);
                    bool b1 = e1.IsValidValue;
                    string v1 = e1.Value;

                    // ugly temporary solution
                    string next_inner;
                    if (!inners_in_list)
                    {
                        next_inner = name_inners + "[" + i + "]";
                    }
                    else
                    {
                        next_node = next_node + "->_Next";
                        next_inner = next_node + "->_Myval";
                    }

                    Expression e2 = debugger.GetExpression(next_node);
                    bool b2 = e2.IsValidValue;
                    string v2 = e2.Value;

                    Expression e3 = debugger.GetExpression(next_inner);
                    bool b3 = e3.IsValidValue;
                    string v3 = e3.Value;

                    r = Ring.Load(debugger, next_inner, ring_member);

                    result.inners.Add(r);
                    result.box.Expand(r.Aabb);
                }

                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                this.Draw(box, graphics, new Settings(Color.RoyalBlue));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                LocalCS cs = new LocalCS(box, graphics);

                PointF[] dst_outer_points = cs.Convert(outer);
                if (dst_outer_points != null)
                {
                    Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                    SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));

                    GraphicsPath gp = new GraphicsPath();
                    gp.AddPolygon(dst_outer_points);

                    if (settings.showDir)
                        DrawDir(dst_outer_points, true, graphics, pen);

                    foreach (Ring inner in inners)
                    {
                        PointF[] dst_inner_points = cs.Convert(inner);
                        if (dst_inner_points != null)
                        {
                            gp.AddPolygon(dst_inner_points);

                            if (settings.showDir)
                                DrawDir(dst_inner_points, true, graphics, pen);
                        }
                    }

                    graphics.FillPath(brush, gp);
                    graphics.DrawPath(pen, gp);
                }
            }

            public Geometry.Box Aabb { get { return box; } }

            private Geometry.Box box;
        }

        public class Multi<S> : IDrawable
        {
            private Multi() { }

            public static Multi<S> Load(Debugger debugger, string name, string outer, string inners)
            {
                Multi<S> result = new Multi<S>();
                result.singles = new List<IDrawable>();
                result.box = Geometry.Box.Inverted();

                int size = LoadSize(debugger, name);

                Type singleType = typeof(S);
                if (singleType == typeof(Point))
                {
                    for (int i = 0; i < size; ++i)
                    {
                        Point s = Point.Load(debugger, name + "[" + i + "]");
                        result.singles.Add(s);
                        result.box.Expand(s);
                    }
                }
                else if (singleType == typeof(Linestring))
                {
                    for (int i = 0; i < size; ++i)
                    {
                        Linestring s = Linestring.Load(debugger, name + "[" + i + "]");
                        result.singles.Add(s);
                        result.box.Expand(s.Aabb);
                    }
                }
                else if (singleType == typeof(Polygon))
                {
                    for (int i = 0; i < size; ++i)
                    {
                        Polygon s = Polygon.Load(debugger, name + "[" + i + "]", outer, inners);
                        result.singles.Add(s);
                        result.box.Expand(s.Aabb);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.Assert(false);
                }
                
                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                foreach (IDrawable single in singles)
                {
                    single.Draw(box, graphics);
                }
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                foreach (IDrawable single in singles)
                {
                    single.Draw(box, graphics, settings);
                }
            }

            public Geometry.Box Aabb { get { return box; } }

            private Geometry.Box box;

            private List<IDrawable> singles;
        }

        public class ValuesContainer : IDrawable
        {
            private ValuesContainer() { }

            public static ValuesContainer Load(Debugger debugger, string name, int size)
            {
                ValuesContainer result = new ValuesContainer();
                result.values = new List<double>();
                result.box = Geometry.Box.Inverted();

                if (size > 0)
                    result.box.Expand(new Point(0.0, 0.0));

                for (int i = 0; i < size; ++i)
                {
                    Expression expr = debugger.GetExpression("(double)" + name + "[" + i + "]");
                    if (!expr.IsValidValue)
                        continue;
                    double v = double.Parse(expr.Value, System.Globalization.CultureInfo.InvariantCulture);
                    result.values.Add(v);
                    result.box.Expand(new Point(i, v));
                }

                // make square
                if (size > 0)
                    result.box.max[0] = result.box.min[0] + result.box.Height;

                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                this.Draw(box, graphics, new Settings(Color.Black));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen axis_pen = new Pen(settings.color, 2);
                Pen pen = new Pen(Color.FromArgb(112, settings.color), 1);

                float ax0 = cs.ConvertX(box.min[0]);
                float ax1 = cs.ConvertX(box.max[0]);
                float ay = cs.ConvertY(0);
                graphics.DrawLine(axis_pen, ax0, ay, ax1, ay);

                double i = 0.0;
                double step = box.Width / values.Count;
                foreach (double v in values)
                {
                    float x = cs.ConvertX(i);
                    float y = cs.ConvertY(v);
                    graphics.DrawLine(pen, x, ay, x, y);
                    i += step;
                }
            }

            public Geometry.Box Aabb { get { return box; } }

            private Geometry.Box box;

            private List<double> values;
        }

        public class TurnsContainer : IDrawable
        {
            private TurnsContainer() { }

            private class Turn
            {
                public Turn(Geometry.Point p, char m, char o0, char o1)
                {
                    point = p;
                    method = m;
                    operation0 = o0;
                    operation1 = o1;
                }

                public Geometry.Point point;
                public char method, operation0, operation1;
            }

            private static char MethodChar(string method)
            {
                switch (method)
                {
                    case "method_none": return '-';
                    case "method_disjoint": return 'd';
                    case "method_crosses": return 'i';
                    case "method_touch": return 't';
                    case "method_touch_interior": return 'm';
                    case "method_collinear": return 'c';
                    case "method_equal": return 'e';
                    case "method_error": return '!';
                    default: return '?';
                }
            }

            private static char OperationChar(string operation)
            {
                switch(operation)
                {
                    case "operation_none" : return '-';
                    case "operation_union" : return 'u';
                    case "operation_intersection" : return 'i';
                    case "operation_blocked" : return 'x';
                    case "operation_continue" : return 'c';
                    case "operation_opposite" : return 'o';
                    default : return '?';
                }
            }

            public static TurnsContainer Load(Debugger debugger, string name, int size)
            {
                TurnsContainer result = new TurnsContainer();
                result.turns = new List<Turn>();
                result.box = Geometry.Box.Inverted();

                for (int i = 0; i < size; ++i)
                {
                    string turn_str = name + "[" + i + "]";

                    Point p = Point.Load(debugger, turn_str + ".point");

                    char method = '?';
                    Expression expr_method = debugger.GetExpression(turn_str + ".method");
                    if (expr_method.IsValidValue)
                        method = MethodChar(expr_method.Value);

                    char op0 = '?';
                    Expression expr_op0 = debugger.GetExpression(turn_str + ".operations[0].operation");
                    if (expr_op0.IsValidValue)
                        op0 = OperationChar(expr_op0.Value);

                    char op1 = '?';
                    Expression expr_op1 = debugger.GetExpression(turn_str + ".operations[1].operation");
                    if (expr_op1.IsValidValue)
                        op1 = OperationChar(expr_op1.Value);

                    result.turns.Add(new Turn(p, method, op0, op1));
                    result.box.Expand(p);
                }

                return result;
            }

            public void Draw(Geometry.Box box, Graphics graphics)
            {
                this.Draw(box, graphics, new Settings(Color.DarkOrange));
            }

            public void Draw(Geometry.Box box, Graphics graphics, Settings settings)
            {
                LocalCS cs = new LocalCS(box, graphics);

                Pen pen = new Pen(Color.FromArgb(112, settings.color), 2);
                SolidBrush brush = new SolidBrush(Color.FromArgb(64, settings.color));
                SolidBrush text_brush = new SolidBrush(Color.Black);

                Font font = null;
                if (settings.showLabels)
                    font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 10);

                Dictionary<System.Drawing.Point, string> labelsMap = new Dictionary<System.Drawing.Point, string>();

                int index = 0;
                foreach (Turn turn in turns)
                {
                    PointF p = cs.Convert(turn.point);
                    graphics.FillEllipse(brush, p.X - 2.5f, p.Y - 2.5f, 5, 5);
                    graphics.DrawEllipse(pen, p.X - 2.5f, p.Y - 2.5f, 5, 5);

                    if (settings.showLabels)
                    {
                        System.Drawing.Point pi = new System.Drawing.Point((int)Math.Round(p.X), (int)Math.Round(p.Y));
                        string str = index.ToString() + ' ' + turn.method + ':' + turn.operation0 + '/' + turn.operation1;

                        if (!labelsMap.ContainsKey(pi))
                            labelsMap.Add(pi, str);
                        else
                            labelsMap[pi] = labelsMap[pi] + '\n' + str;
                    }
                    ++index;
                }

                foreach(var label in labelsMap)
                    graphics.DrawString(label.Value, font, text_brush, label.Key);
            }

            public Geometry.Box Aabb { get { return box; } }

            private Geometry.Box box;

            private List<Turn> turns;
        }

        private static bool DrawDir(PointF p0, PointF p1, Graphics graphics, Pen pen)
        {
            PointF v = SubF(p1, p0);
            float distSqr = DotF(v, v);

            if (distSqr < 49.0f) // (1+5+1)^2
                return false;

            PointF ph = AddF(p0, MulF(v, 0.5f));
            float a = AngleF(v);
            PointF ps = AddF(ph, RotF(new PointF(-1.25f, -2.5f), a));
            PointF pm = AddF(ph, RotF(new PointF(1.25f, 0.0f), a));
            PointF pe = AddF(ph, RotF(new PointF(-1.25f, 2.5f), a));
            graphics.DrawLine(pen, pm, ps);
            graphics.DrawLine(pen, pm, pe);

            return true;
        }

        private static void DrawDir(PointF[] points, bool closed, Graphics graphics, Pen pen)
        {
            bool drawn = false;
            for (int i = 1; i < points.Length; ++i)
            {
                bool ok = DrawDir(points[i - 1], points[i], graphics, pen);
                drawn = drawn || ok;
            }
            if (closed && points.Length > 1)
            {
                bool ok = DrawDir(points[points.Length - 1], points[0], graphics, pen);
                drawn = drawn || ok;
                if (drawn)
                    graphics.DrawEllipse(pen, points[0].X - 2.5f, points[0].Y - 2.5f, 5, 5);
            }
        }

        private static PointF MulF(PointF p, float v) { return new PointF(p.X * v, p.Y * v); }
        private static PointF DivF(PointF p, float v) { return new PointF(p.X / v, p.Y / v); }
        private static PointF MulF(PointF l, PointF r) { return new PointF(l.X * r.X, l.Y * r.Y); }
        private static PointF DivF(PointF l, PointF r) { return new PointF(l.X / r.X, l.Y / r.Y); }
        private static PointF AddF(PointF l, PointF r) { return new PointF(l.X + r.X, l.Y + r.Y); }
        private static PointF SubF(PointF l, PointF r) { return new PointF(l.X - r.X, l.Y - r.Y); }
        private static float DotF(PointF l, PointF r) { return l.X * r.X + l.Y * r.Y; }
        private static float AngleF(PointF v) { return (float)Math.Atan2(v.Y, v.X); }
        private static PointF RotF(PointF v, float a)
        {
            return new PointF((float)(v.X * Math.Cos(a) - v.Y * Math.Sin(a)),
                              (float)(v.Y * Math.Cos(a) + v.X * Math.Sin(a)));
        }

        private static List<string> Tparams(string type)
        {
            List<string> result = new List<string>();

            int param_list_index = 0;
            int index = 0;
            int param_first = -1;
            int param_last = -1;
            foreach (char c in type)
            {
                if (c == '<')
                {
                    ++param_list_index;
                }
                else if (c == '>')
                {
                    if (param_last == -1 && param_list_index == 1)
                        param_last = index;

                    --param_list_index;
                }
                else if (c == ',')
                {
                    if (param_last == -1 && param_list_index == 1)
                        param_last = index;
                }
                else
                {
                    if (param_first == -1 && param_list_index == 1)
                        param_first = index;
                }

                if (param_first != -1 && param_last != -1)
                {
                    result.Add(type.Substring(param_first, param_last - param_first));
                    param_first = -1;
                    param_last = -1;
                }

                ++index;
            }

            return result;
        }

        private static string BaseType(string type)
        {
            if (type.StartsWith("const "))
                type = type.Remove(0, 6);
            int i = type.IndexOf('<');
            if (i > 0)
                type = type.Remove(i);
            return type;
        }

        // For now the list of handled types is hardcoded
        private static bool IsGeometry(string type)
        {
            string base_type = BaseType(type);
            if (base_type == "boost::geometry::model::point")
            {
                List<string> tparams = Tparams(type);
                return tparams.Count == 3 && tparams[1] == "2"; // 2D only for now
            }
            else if (base_type == "boost::geometry::model::d2::point_xy")
            {
                return true;
            }

            return false;
        }

        private static bool IsGeometry(string type, string single_type)
        {
            if (BaseType(type) != single_type)
                return false;
            List<string> tparams = Tparams(type);
            return tparams.Count > 0 && IsGeometry(tparams[0]);
        }

        private static bool IsGeometry(string type, string multi_type, string single_type)
        {
            if (BaseType(type) != multi_type)
                return false;

            List<string> tparams = Tparams(type);
            if (!(tparams.Count > 0 && IsGeometry(tparams[0], single_type)))
                return false;

            tparams = Tparams(tparams[0]);
            return tparams.Count > 0 && IsGeometry(tparams[0]);
        }

        private static bool IsTurnsContainer(string type)
        {
            if (! (BaseType(type) == "std::vector"
                || BaseType(type) == "std::deque") )
                return false;

            List<string> tparams = Tparams(type);
            if (! (tparams.Count > 0
                && (BaseType(tparams[0]) == "boost::geometry::detail::overlay::turn_info"
                 || BaseType(tparams[0]) == "boost::geometry::detail::overlay::traversal_turn_info")) )
                return false;

            tparams = Tparams(tparams[0]);
            return tparams.Count > 0 && IsGeometry(tparams[0]);
        }

        private static IDrawable LoadGeometry(Debugger debugger, string name, string type)
        {
            IDrawable d = null;

            if (IsGeometry(type))
                d = Point.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::box"))
                d = Box.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::segment")
                  || IsGeometry(type, "boost::geometry::model::referring_segment"))
                d = Segment.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::linestring"))
                d = Linestring.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::ring"))
                d = Ring.Load(debugger, name);
            else if (IsGeometry(type, "boost::geometry::model::polygon"))
                d = Polygon.Load(debugger, name, "m_outer", "m_inners");
            else if (IsGeometry(type, "boost::geometry::model::multi_point"))
                d = Multi<Point>.Load(debugger, name, "m_outer", "m_inners");
            else if (IsGeometry(type, "boost::geometry::model::multi_linestring", "boost::geometry::model::linestring"))
                d = Multi<Linestring>.Load(debugger, name, "m_outer", "m_inners");
            else if (IsGeometry(type, "boost::geometry::model::multi_polygon", "boost::geometry::model::polygon"))
                d = Multi<Polygon>.Load(debugger, name, "m_outer", "m_inners");

            else if (BaseType(type) == "boost::polygon::point_data")
                d = Point.Load(debugger, name);
            else if (BaseType(type) == "boost::polygon::segment_data")
                d = Segment.Load(debugger, name, "points_[0]", "points_[1]");
            else if (BaseType(type) == "boost::polygon::rectangle_data")
                d = Box.Load(debugger, name, "ranges_[0]", "ranges_[1]", true);
            else if (BaseType(type) == "boost::polygon::polygon_data")
                d = Ring.Load(debugger, name, "coords_");
            else if (BaseType(type) == "boost::polygon::polygon_with_holes_data")
                d = Polygon.Load(debugger, name, "self_", "holes_", true, "coords_");

            return d;
        }

        private static IDrawable LoadGeometryOrVariant(Debugger debugger, string name, string type)
        {
            // Currently the supported types are hardcoded as follows:

            // Boost.Geometry models
            IDrawable d = LoadGeometry(debugger, name, type);

            if (d == null)
            {
                // Boost.Variant containing a Boost.Geometry model
                if (BaseType(type) == "boost::variant")
                {
                    Expression expr_which = debugger.GetExpression(name + ".which_");
                    if (!expr_which.IsValidValue)
                        return null;
                    int which = int.Parse(expr_which.Value);
                    List<string> tparams = Tparams(type);
                    if (which < 0 || which >= tparams.Count)
                        return null;
                    string value_str = "(*(" + tparams[which] + "*)" + name + ".storage_.data_.buf)";
                    Expression expr_value = debugger.GetExpression(value_str);
                    if (!expr_value.IsValidValue)
                        return null;
                    d = LoadGeometry(debugger, value_str, expr_value.Type);
                }
            }

            return d;
        }

        private static IDrawable LoadTurnsContainer(Debugger debugger, string name, string type, bool verbose)
        {
            // STL RandomAccess container of Turns
            IDrawable d = null;

            if (IsTurnsContainer(type))
            {
                int size = LoadSize(debugger, name);
                d = TurnsContainer.Load(debugger, name, size);
            }

            return d;
        }

        private static IDrawable LoadDrawable(Debugger debugger, string name, string type)
        {
            IDrawable d = LoadGeometryOrVariant(debugger, name, type);

            if (d == null)
                d = LoadTurnsContainer(debugger, name, type, false);

            if (d == null)
            {
                // STL RandomAccess container of 1D values convertible to double
                if (BaseType(type) == "std::vector"
                 || BaseType(type) == "std::deque")
                {
                    int size = LoadSize(debugger, name);
                    d = ValuesContainer.Load(debugger, name, size);
                }
                // Boost.Array of 1D values convertible to double
                else if (BaseType(type) == "boost::array")
                {
                    List<string> tparams = Tparams(type);
                    int size = int.Parse(tparams[1]);
                    d = ValuesContainer.Load(debugger, name, size);
                }
            }

            return d;
        }

        // For GeometryWatch
        public static IDrawable MakeGeometry(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return null;

            IDrawable d = LoadGeometryOrVariant(debugger, expr.Name, expr.Type);
            if (d != null)
                return d;

            return LoadTurnsContainer(debugger, expr.Name, expr.Type, true);
        }

        // For GraphicalWatch
        public static IDrawable MakeDrawable(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            if (!expr.IsValidValue)
                return null;

            return LoadDrawable(debugger, expr.Name, expr.Type);
        }

        // For GeometryWatch
        public static bool DrawGeometry(Graphics graphics, Debugger debugger, string name)
        {
            IDrawable d = MakeGeometry(debugger, name);
            if (d == null)
                return false;
            d.Draw(d.Aabb, graphics);
            return true;
        }

        // For GraphicalWatch
        public static bool Draw(Graphics graphics, Debugger debugger, string name)
        {
            IDrawable d = MakeDrawable(debugger, name);
            if (d == null)
                return false;
            d.Draw(d.Aabb, graphics);
            return true;
        }

        public static bool DrawAabb(Graphics graphics, Geometry.Box box)
        {
            if (!box.IsValid())
                return false;

            LocalCS cs = new LocalCS(box, graphics);

            Pen pen = new Pen(Color.Black, 1);
            SolidBrush brush = new SolidBrush(Color.Black);

            float min_x = cs.ConvertX(box.min[0]);
            float min_y = cs.ConvertY(box.min[1]);
            float max_x = cs.ConvertX(box.max[0]);
            float max_y = cs.ConvertY(box.max[1]);

            graphics.DrawLine(pen, min_x - 1, min_y, min_x + 1, min_y);
            graphics.DrawLine(pen, min_x, min_y - 1, min_x, min_y + 1);
            graphics.DrawLine(pen, max_x - 1, max_y, max_x + 1, max_y);
            graphics.DrawLine(pen, max_x, max_y - 1, max_x, max_y + 1);

            float maxHeight = 20.0f;// Math.Min(Math.Max(graphics.VisibleClipBounds.Height - min_y, 0.0f), 20.0f);
            if (maxHeight > 1)
            {
                string min_x_str = box.min[0].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string min_y_str = box.min[1].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string max_x_str = box.max[0].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string max_y_str = box.max[1].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                Font font = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), maxHeight / 2.0f);
                StringFormat drawFormat = new StringFormat();
                drawFormat.Alignment = StringAlignment.Center;
                string minStr = "(" + min_x_str + " " + min_y_str + ")";
                string maxStr = "(" + max_x_str + " " + max_y_str + ")";
                SizeF minSize = graphics.MeasureString(minStr, font);
                SizeF maxSize = graphics.MeasureString(maxStr, font);
                RectangleF drawRectMin = new RectangleF(Math.Max(min_x - minSize.Width, 0.0f),
                                                        Math.Min(min_y + 2, graphics.VisibleClipBounds.Height - maxSize.Height),
                                                        minSize.Width,
                                                        minSize.Height);
                RectangleF drawRectMax = new RectangleF(Math.Min(max_x, graphics.VisibleClipBounds.Width - maxSize.Width),
                                                        Math.Max(max_y - maxHeight, 0.0f),
                                                        maxSize.Width,
                                                        maxSize.Height);
                graphics.DrawString(minStr, font, brush, drawRectMin, drawFormat);
                graphics.DrawString(maxStr, font, brush, drawRectMax, drawFormat);
            }

            return true;
        }

        private static int LoadSize(Debugger debugger, string name)
        {
            // VS2015 vector
            Expression expr_inners_size0 = debugger.GetExpression(name + "._Mypair._Myval2._Mylast-" + name + "._Mypair._Myval2._Myfirst");
            if (expr_inners_size0.IsValidValue)
            {
                int result = int.Parse(expr_inners_size0.Value);
                return Math.Max(result, 0);
            }
            // VS2015 deque, list
            Expression expr_inners_size1 = debugger.GetExpression(name + "._Mypair._Myval2._Mysize");
            if (expr_inners_size1.IsValidValue)
            {
                int result = int.Parse(expr_inners_size1.Value);
                return Math.Max(result, 0);
            }
            /*
            // VS2013 vector
            Expression expr_inners_size2 = debugger.GetExpression(name + "._Mylast-" + name + "._Myfirst");
            if (expr_inners_size2.IsValidValue)
            {
                int result = int.Parse(expr_inners_size2.Value);
                return Math.Max(result, 0);
            }
            // VS2013 deque, list
            Expression expr_inners_size3 = debugger.GetExpression(name + "._Mysize");
            if (expr_inners_size3.IsValidValue)
            {
                int result = int.Parse(expr_inners_size3.Value);
                return Math.Max(result, 0);
            }*/

            return 0;
        }

        private class LocalCS
        {
            public LocalCS(Geometry.Box src_box, Graphics dst_graphics)
            {
                float w = dst_graphics.VisibleClipBounds.Width;
                float h = dst_graphics.VisibleClipBounds.Height;
                dst_x0 = w / 2;
                dst_y0 = h / 2;
                float dst_w = w * 0.9f;
                float dst_h = h * 0.9f;

                double src_w = src_box.Width;
                double src_h = src_box.Height;
                src_x0 = src_box.min[0] + src_w / 2;
                src_y0 = src_box.min[1] + src_h / 2;

                if (src_w == 0 && src_h == 0)
                    scale = 1; // point
                else if (src_w == 0)
                    scale = (h * 0.9f) / src_h;
                else if (src_h == 0)
                    scale = (w * 0.9f) / src_w;
                else
                {
                    double scale_w = (w * 0.9f) / src_w;
                    double scale_h = (h * 0.9f) / src_h;
                    scale = Math.Min(scale_w, scale_h);
                }
            }

            public float ConvertX(double src)
            {
                return dst_x0 + (float)((src - src_x0) * scale);
            }

            public float ConvertY(double src)
            {
                return dst_y0 - (float)((src - src_y0) * scale);
            }

            public float ConvertDimension(double src)
            {
                return (float)(src * scale);
            }

            public PointF Convert(Geometry.Point p)
            {
                return new PointF(ConvertX(p[0]), ConvertY(p[1]));
            }

            public PointF[] Convert(Geometry.Ring ring)
            {
                if (ring.Count <= 0)
                    return null;

                int dst_count = ring.Count + (ring[0] == ring[ring.Count - 1] ? 0 : 1);

                PointF[] dst_points = new PointF[dst_count];
                int i = 0;
                for (; i < ring.Count; ++i)
                {
                    dst_points[i] = Convert(ring[i]);
                }
                if (i < dst_count)
                {
                    dst_points[i] = Convert(ring[0]);
                }

                return dst_points;
            }

            float dst_x0, dst_y0;
            double src_x0, src_y0;
            double scale;
        }
    }
}