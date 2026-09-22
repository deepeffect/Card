using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DortGok.Playtest
{
    /// <summary>
    /// dotnet run --project Tools/Playtest                   → oyunu açar
    /// dotnet run --project Tools/Playtest -- --snapshot x.png [tur]  → botlarla oynatıp ekran görüntüsü kaydeder
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 1 && args[0] == "--snapshot")
                return Snapshot(args[1], args.Length > 2 ? int.Parse(args[2]) : 12);

            Application.Run(new MainForm());
            return 0;
        }

        private static int Snapshot(string path, int turns)
        {
            using var form = new MainForm(tile: 52);
            form.StopTimer();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-5000, -5000);
            form.ShowInTaskbar = false;
            form.Show();
            for (int seat = 0; seat < 4; seat++) form.SetHuman(seat, false);
            form.AutoPlay(turns);
            var current = FormCurrentSeat(form);
            form.SetHuman(current, true);
            form.SelectFirstUnitOfCurrent();
            Application.DoEvents();
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
            form.Close();
            bmp.Save(path, ImageFormat.Png);
            Console.WriteLine("Kaydedildi: " + System.IO.Path.GetFullPath(path));
            return 0;
        }

        private static int FormCurrentSeat(MainForm form) => form.CurrentSeat;
    }
}
