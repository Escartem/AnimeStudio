using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
            var arch = Environment.Is64BitProcess ? "x64" : "x32";
            var appAssembly = typeof(Program).Assembly.GetName();
            var productName = appAssembly.Name;
            var productVer = appAssembly.Version.ToString();
            Text += " " + productName;
            productTitleLabel.Text = productName;
            productVersionLabel.Text = $"v{productVer} [{arch}]";
            productNamelabel.Text = productName;
            modVersionLabel.Text = productVer;

            licenseRichTextBox.Text = GetLicenseText();
        }

        private string GetLicenseText()
        {
            string license = "MIT License";

            if (File.Exists("LICENSE"))
            {
                string text = File.ReadAllText("LICENSE");
                license = text;
            }

            return license;
        }

        private void checkUpdatesLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://nightly.link/Escartem/AnimeStudio/workflows/build/master")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void gitYarikLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://github.com/yarik0chka/YarikStudio")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void gitHashblenLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://github.com/hashblen/ZZZ_Studio")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void gitRazmothLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://github.com/RazTools/Studio")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void gitPerfareLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://github.com/Perfare/AssetStudio")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void gitEscartemLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://github.com/Escartem")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void gitAelurumLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://github.com/aelurum/AssetStudio")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://github.com/Escartem/AnimeStudio")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://github.com/Escartem/AnimeStudio/issues/new?template=bug_report.md")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }

        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new ProcessStartInfo("https://discord.gg/hoyotoon")
            {
                UseShellExecute = true
            };
            Process.Start(ps);
        }
    }
}
