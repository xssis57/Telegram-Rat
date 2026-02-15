using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Principal;

namespace Builder
{
    public partial class Builder : Form
    {
        public Builder()
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                try { Process.Start(new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = true, Verb = "runas" }); } catch { }
                Environment.Exit(0);
            }
            InitializeComponent();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {

            try
            {
                string filePath = Environment.CurrentDirectory + "\\net10.0-windows" + "\\settings.json";

                if (!File.Exists(filePath))
                {
                    MessageBox.Show("settings.json not found:\n" + filePath);
                    return;
                }

                string jsonText = File.ReadAllText(filePath);
                JObject jsonObj = JObject.Parse(jsonText);

                // Access the "General" object
                JObject general = (JObject)jsonObj["General"];

                if (general == null)
                {
                    MessageBox.Show("General section not found!");
                    return;
                }

                // Modify values
                general["Bot"] = textBox1.Text;
                general["Id"] = textBox2.Text;

                File.WriteAllText(filePath, jsonObj.ToString());

                MessageBox.Show("Settings updated successfully!");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.ToString());
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/xssis57/")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
