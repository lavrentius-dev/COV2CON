using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;


namespace COV2CON
{
    public partial class MainWindow : Window
    {
        public void DebugParseSVGDataStr(int i, MatchCollection Match)
        {
            MatchCollection Match1 = Regex.Matches(Match[i].Groups[3].Value,
/* closepath */                         @"z|" +
/* moveto */                            @"m\s?(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)+|" +
/* lineto */                            @"l\s?(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)+|" +
/* horizontal-lineto */                 @"h\s?(?:-?\d+(?:\.\d+)?\s+)+|" +
/* vertical-lineto */                   @"v\s?(?:-?\d+(?:\.\d+)?\s+)+|" +
/* curveto */                           @"c\s?(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)(?:-?\d+(\.\d+)?,-?\d+(?:\.\d+)?\s+)+|" +
/* smooth-curveto */                    @"s\s?(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)+|" +
/* quadratic-bezier-curveto */          @"q\s?(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)+|" +
/* smooth-quadratic-bezier-curveto */   @"t\s?(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)+|" +
/* catmull-rom */                       @"r\s?(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)+|" +
/* elliptical-arc */                    @"a\s?(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)(?:-?\d+(?:\.\d+)?\s+)(?:-?\d+(\.\d+)?,-?\d+(?:\.\d+)?\s+)(?:-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\s+)" +
/* bearing */                           @"b\s?(?:-?\d+(?:\.\d+)?\s+)+",
                RegexOptions.IgnoreCase);

            // Parse shapes (total Match1.Count shapes => one shape per Match1[j].Groups[0] item)
            //Point[] pla = null;
            if (Match1.Count > 0)
            {
                double Xstart = Double.NaN, Ystart = Double.NaN;
                double Xnext = 0, Ynext = 0;  // initialized below

                for (int j = 0; j < Match1.Count; j++)
                {
                    char c = Match1[j].Groups[0].Value[0];
                    string dataStr = Match1[j].Groups[0].Value.Substring(1).TrimStart(' ').Trim();
                    dataStr = Regex.Replace(dataStr, @"\s+", " ");
                    Debug.WriteLine($" [{i}] {Match[i].Groups[2].Value}: [{j}] -> {c} '{dataStr}'");

                    string[] lstNumPair = dataStr.Split(' ');

                    switch (c)
                    {
                        case 'm':
                        case 'M':
                            if (lstNumPair.Length > 0)
                            {
                                //pla = new Point[lstNumPair.Length + 1];

                                for (int k = 0; k < lstNumPair.Length; k++)
                                {
                                    string numPair = lstNumPair[k];
                                    string[] coord = numPair.Split(',');
                                    if (Double.TryParse(coord[0], out double X) && Double.TryParse(coord[1], out double Y))
                                    {
                                        if (k == 0)
                                        {
                                            if ((Xstart == Double.NaN) || (Ystart == Double.NaN))
                                            {
                                                Xstart = X;
                                                Ystart = Y;
                                            }
                                            else
                                            {
                                                Xstart = Xnext + X;
                                                Ystart = Ynext + Y;
                                            }
                                            Xnext = Xstart; Ynext = Ystart;
                                            //pla[k] = new Point(Xstart, Ystart);
                                            Debug.WriteLine($"{Xstart}\t{Ystart}");
                                        }
                                        else
                                        {
                                            Xnext += X;
                                            Ynext += Y;
                                            //pla[k] = new Point(Xnext, Ynext);
                                            Debug.WriteLine($"{Xnext}\t{Ynext}");
                                        }
                                    }
                                    else
                                        Debug.WriteLine($"** Unable to parse coords '{numPair}' of point {k} for '{Match[i].Groups[1].Value}'");
                                }

                                Debug.WriteLine($"* Parsed {lstNumPair.Length} points for '{Match[i].Groups[1].Value}'");
                            }
                            else
                            {
                                Debug.WriteLine($"** Data string split count for '{Match1[j].Groups[0].Value}' < 0");
                            }
                            break;

                        case 'z':
                        case 'Z':
                            //if (pla != null)
                            //{
                            //    pla[lstNumPair.Length] = new Point(Xstart, Ystart);  // FIXME: shape is not closed

                            //    PolyLineSegment pls = new PolyLineSegment
                            //    {
                            //        Points = new PointCollection(pla)
                            //    };

                            //    PathFigure pfg = new PathFigure
                            //    {
                            //        IsClosed = true
                            //    };
                            //    pfg.Segments.Add(pls);

                            //    PathGeometry pgm = new PathGeometry();
                            //    pgm.Figures.Add(pfg);

                            //    //var brush = (Brush)converter.ConvertFromString(Match[i].Groups[4].Value);
                            //    Path pth = new Path
                            //    {
                            //        Stroke = Brushes.Black,
                            //        Fill = Brushes.DarkKhaki,
                            //        StrokeThickness = 1
                            //    };
                            //    pth.Data = pgm;

                            //    Grid1.Children.Add(pth);
                            //}
                            //else
                            //{
                            //    Debug.WriteLine($"** pla is null");
                            //}
                            break;

                        default:
                            Debug.WriteLine($"** Item ({i}, {j}) = unknown case switch '{c}'");
                            break;
                    }
                }
            }
        }

    }
}
