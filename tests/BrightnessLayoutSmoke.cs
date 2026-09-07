using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class BrightnessLayoutSmoke
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { return MainCore(args); }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
            if (ex.InnerException != null)
                Console.Error.WriteLine(ex.InnerException.GetType().FullName + ": " + ex.InnerException.Message);
            return 9;
        }
    }

    private static int MainCore(string[] args)
    {
        if (args.Length != 2) return 2;
        Assembly assembly = Assembly.LoadFrom(args[0]);
        Type formType = assembly.GetType("AndroidADBTools.MainForm", true);
        using (Form form = (Form)Activator.CreateInstance(formType))
        {
            form.WindowState = FormWindowState.Normal;
            form.Size = new Size(1500, 980);
            TabControl tabs = (TabControl)GetField(formType, "mainTabs").GetValue(form);
            foreach (TabPage page in tabs.TabPages)
                if (String.Equals(page.Name, "brightness", StringComparison.Ordinal)) tabs.SelectedTab = page;
            TabPage brightnessPage = tabs.SelectedTab;
            Control brightnessContent = brightnessPage.Controls[0];
            brightnessPage.Controls.Remove(brightnessContent);
            using (Form host = new Form())
            {
                host.FormBorderStyle = FormBorderStyle.None;
                host.ShowInTaskbar = false;
                host.StartPosition = FormStartPosition.Manual;
                host.Location = new Point(-30000, -30000);
                host.ClientSize = new Size(1300, 850);
                brightnessContent.Dock = DockStyle.Fill;
                host.Controls.Add(brightnessContent);
                host.Show();
                Application.DoEvents();
                PerformLayoutRecursive(host);

                Button saved = (Button)GetField(formType, "applySavedBrightnessCalibrationButton").GetValue(form);
                Size required = TextRenderer.MeasureText(saved.Text, saved.Font,
                    new Size(Int32.MaxValue, saved.ClientSize.Height), TextFormatFlags.SingleLine);
                if (saved.ClientSize.Width < required.Width + 24)
                {
                    Console.Error.WriteLine("Saved-result button is too narrow: " + saved.ClientSize.Width + " < " + (required.Width + 24));
                    return 3;
                }
                if (saved.BackColor.GetBrightness() < 0.65F)
                {
                    Console.Error.WriteLine("Disabled saved-result button background is too dark.");
                    return 4;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(args[1]));
                using (Bitmap image = new Bitmap(brightnessContent.ClientSize.Width, brightnessContent.ClientSize.Height))
                {
                    brightnessContent.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    image.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
        Console.WriteLine("BRIGHTNESS_LAYOUT_SMOKE_OK");
        return 0;
    }

    private static FieldInfo GetField(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(type.FullName, name);
        return field;
    }

    private static void PerformLayoutRecursive(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls) PerformLayoutRecursive(child);
    }
}
