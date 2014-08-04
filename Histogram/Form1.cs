using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web;
using System.Windows.Forms;

namespace Histogram
{
  public partial class Form1 : Form
  {
    private int[] distribution;
    private int prev_x, prev_y;
    private Dictionary<string, decimal> stats;
    private Bitmap backup;

    public Form1()
    {
      InitializeComponent();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
      comboBox1.Items.Clear();
      string[] files = Directory.GetFiles(".");
      foreach (string file in files)
        comboBox1.Items.Add(file.Substring(2));
      comboBox1.SelectedIndex = 0;
    }

    private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (sender != checkBox1 && File.Exists(comboBox1.Text))
      {
        distribution = GetHistogramData(File.ReadAllBytes(comboBox1.Text));
        stats = GetStats(distribution, pictureBox1.Size.Height);
      }
      backup = DrawHistogram(distribution, stats, pictureBox1.Size.Width, pictureBox1.Size.Height, checkBox1.Checked);
      pictureBox1.Image = backup;
      string l = "";
      if (stats["used"] < distribution.Length / 2)
        l += "Is probably a textfile. ";
      if (stats["maxid"] == 0)
        l += "Is probably an executable, or data file. ";
      if (stats["used"] == distribution.Length && stats["min"] > stats["mean"] * (decimal).9) // This bit needs a lot of work.
        l += "Is probably compressed or encrypted. ";
      label1.Text = l;
      //Console.WriteLine("Deviation: " + stats["deviation"] + " Entropy: " + stats["entropy"]);
    }

    private void checkBox1_CheckedChanged(object sender, EventArgs e)
    {
      comboBox1_SelectedIndexChanged(sender, e);
    }

    private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
    {
      if (prev_x != e.X && pictureBox1.Image != backup)
        pictureBox1.Image = backup;
      if (prev_x == e.X && prev_y == e.Y || e.X >= pictureBox1.Size.Width)
        return;
      prev_x = e.X;
      prev_y = e.Y;
      int i = (int)(prev_x / Math.Ceiling((double)pictureBox1.Size.Width / (double)distribution.Length));
      byte[] c = new byte[] { (byte)i };
      string str = "Dec: " + i.ToString();
      str += " Hex: 0x" + BitConverter.ToString(c);
      str += "\nCharacter: ( \"" + HttpUtility.JavaScriptStringEncode(Encoding.UTF8.GetString(c)) + "\" )";
      str += "\nAmount: " + distribution[i];
      toolTip1.SetToolTip(pictureBox1, str);
      int h;
      if (checkBox1.Checked && distribution[i] > 0)
        h = (int)(Math.Log((double)distribution[i], (double)stats["max"]) * (double)backup.Height);
      else
        h = (int)Math.Ceiling(distribution[i] * stats["hscale"]);
      pictureBox1.Image = DrawOverlay(backup, i, h, backup.Width / distribution.Length);
    }

    private void pictureBox1_MouseLeave(object sender, EventArgs e)
    {
      if (pictureBox1.Image != backup)
        pictureBox1.Image = backup;
      toolTip1.SetToolTip(pictureBox1, "");
    }

    private static int[] GetHistogramData(byte[] bytes)
    {
      int[] dist = new int[256];
      for (int i = 0; i < bytes.Length; i++)
        dist[bytes[i]]++;
      return dist;
    }

    private static Dictionary<string, decimal> GetStats(int[] dist, int height)
    {
      Dictionary<string, decimal> ret = new Dictionary<string, decimal>();
      int max = 0;
      int maxid = 0;
      int min = int.MaxValue;
      int minid = 0;
      int used = 0;
      ulong total = 0;
      for (int i = 0; i < dist.Length; i++)
      {
        total += (ulong)dist[i];
        if (dist[i] > 0)
          used++;
        if (dist[i] > max)
        {
          max = dist[i];
          maxid = i;
        }
        else if (dist[i] < min && dist[i] > 0)
        {
          min = dist[i];
          minid = i;
        }
      }
      double scale = (double)height / (double)max;
      int mean = (int)Math.Ceiling((total / (ulong)used) * scale);

      int devtotal = 0;
      double entropy = 0.0;
      double frequency;
      for (int i = 0; i < dist.Length; i++)
      {
        if (dist[i] == 0)
          continue;
        devtotal += Math.Abs(dist[i] - mean);
        frequency = (double)dist[i] / used;
        entropy -= frequency * (Math.Log(frequency, 2));
      }
      decimal deviation = (decimal)devtotal / (decimal)used;

      ret.Add("max", max);
      ret.Add("maxid", maxid);
      ret.Add("min", min);
      ret.Add("minid", minid);
      ret.Add("used", used);
      ret.Add("total", (decimal)total);
      ret.Add("mean", mean);
      ret.Add("hscale", (decimal)scale);
      ret.Add("deviation", deviation);
      ret.Add("entropy", (decimal)entropy);
      return ret;
    }

    /*public static double StandardDeviation(int[] valueList)
    {
      double M = 0.0;
      double S = 0.0;
      int k = 1;
      double tmpM;
      foreach (int value in valueList)
      {
        tmpM = M;
        M += (value - tmpM) / k;
        S += ((value - tmpM) * (value - M)) / M;
        k++;
      }
      return Math.Sqrt(S / valueList.Length);
    }*/

    private static Bitmap DrawHistogram(int[] dist, Dictionary<string, decimal> stats, int width, int height, bool log = false)
    {
      int mean;
      if (log)
        mean = (int)(Math.Log(((double)stats["total"] / (double)stats["used"]), (double)stats["max"]) * height);
      else
        mean = (int)stats["mean"];
      int h;
      int spacing = width / dist.Length;
      Color c;
      Bitmap DrawArea = new Bitmap(width, height);
      Graphics g = Graphics.FromImage(DrawArea);
      g.Clear(Color.Transparent);
      Pen eraser = new Pen(Color.FromArgb(64, Form1.DefaultBackColor.R, Form1.DefaultBackColor.G, Form1.DefaultBackColor.B), 1.0f);
      for (int i = 0; i < dist.Length; i++)
      {
        if (i == stats["maxid"] || i == stats["minid"])
          c = HSL2RGB(i * (.9 / (double)dist.Length), .9, .5);
        else
          c = HSL2RGB(i * (.9 / (double)dist.Length), .6, .5);
        Pen mypen = new Pen(c, (float)spacing);
        //mypen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        if (log && dist[i] > 0)
          h = (int)(Math.Log((double)dist[i], (double)stats["max"]) * (double)height);
        else
          h = (int)Math.Ceiling(dist[i] * stats["hscale"]);
        g.DrawLine(mypen, i * spacing, height, i * spacing, height - h);
        g.Flush();
        g.DrawLine(eraser, i * spacing, height, i * spacing, height - (float)mean);
      }
      g.Dispose();
      return DrawArea;
    }

    private static Bitmap DrawOverlay(Bitmap original, int id, int h, int spacing)
    {
      Bitmap DrawArea = new Bitmap(original);
      Graphics g = Graphics.FromImage(DrawArea);
      Pen mypen = new Pen(Color.FromArgb(64, 255, 255, 255), (float)spacing);
      g.DrawLine(mypen, id * spacing, DrawArea.Height, id * spacing, DrawArea.Height - h);
      g.Dispose();
      return DrawArea;
    }

    // Given H,S,L in range of 0-1
    // Returns a Color (RGB struct) in range of 0-255
    private static Color HSL2RGB(double h, double sl, double l, int alpha = 255)
    {
      double v;
      double r, g, b;

      r = l; // default to gray
      g = l;
      b = l;
      v = (l <= 0.5) ? (l * (1.0 + sl)) : (l + sl - l * sl);

      if (v > 0)
      {
        double m;
        double sv;
        int sextant;
        double fract, vsf, mid1, mid2;

        m = l + l - v;
        sv = (v - m) / v;
        h *= 6.0;
        sextant = (int)h;
        fract = h - sextant;
        vsf = v * sv * fract;
        mid1 = m + vsf;
        mid2 = v - vsf;
        switch (sextant)
        {
          case 0:
            r = v;
            g = mid1;
            b = m;
            break;

          case 1:
            r = mid2;
            g = v;
            b = m;
            break;

          case 2:
            r = m;
            g = v;
            b = mid1;
            break;

          case 3:
            r = m;
            g = mid2;
            b = v;
            break;

          case 4:
            r = mid1;
            g = m;
            b = v;
            break;

          case 5:
            r = v;
            g = m;
            b = mid2;
            break;
        }
      }
      return Color.FromArgb(alpha, Convert.ToByte(r * 255.0f), Convert.ToByte(g * 255.0f), Convert.ToByte(b * 255.0f));
    }
  }
}