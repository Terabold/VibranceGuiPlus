using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace vibrance.GUI.common
{
    public partial class VibranceSettings : Form
    {
        private IVibranceProxy _v;
        private ListViewItem _sender;
        private readonly Func<int, string> _resolveLabelLevel;

        public VibranceSettings(IVibranceProxy v, int minValue, int maxValue, int defaultValue, ListViewItem sender, ApplicationSetting setting, List<ResolutionModeWrapper> supportedResolutionList, Func<int, string> resolveLabelLevel)
        {
            InitializeComponent();
            this.trackBarIngameLevel.Minimum = minValue;
            this.trackBarIngameLevel.Maximum = maxValue;
            this.trackBarIngameLevel.Value = defaultValue;
            this._sender = sender;
            _resolveLabelLevel = resolveLabelLevel;
            this._v = v;
            labelIngameLevel.Text = _resolveLabelLevel(trackBarIngameLevel.Value);
            this.labelTitle.Text += $@"""{sender.Text}""";
            this.pictureBox.Image = this._sender.ListView.LargeImageList.Images[this._sender.ImageIndex];
            this.cBoxResolution.DataSource = supportedResolutionList;
            
            this.trackBarShadowLevel.Minimum = 0;
            this.trackBarShadowLevel.Maximum = 100;
            this.trackBarShadowLevel.Value = 0;
            this.labelShadowLevel.Text = "0%";
            
            this.trackBarGammaLevel.Minimum = 50;
            this.trackBarGammaLevel.Maximum = 200;
            this.trackBarGammaLevel.Value = 100;
            this.labelGammaLevel.Text = "1.00";
            
            // If the setting is new, we don't need to set the progress bar value
            if (setting != null)
            {
                // Sets the progress bar value to the Ingame Vibrance setting
                this.trackBarIngameLevel.Value = setting.IngameLevel;
                this.cBoxResolution.SelectedItem = setting.ResolutionSettings;
                this.checkBoxResolution.Checked = setting.IsResolutionChangeNeeded;
                this.trackBarShadowLevel.Value = setting.ShadowBoostLevel;
                this.labelShadowLevel.Text = $"{this.trackBarShadowLevel.Value}%";
                this.trackBarGammaLevel.Value = (int)(setting.GammaLevel * 100f);
                this.labelGammaLevel.Text = $"{setting.GammaLevel:F2}";
                // Necessary to reload the label which tells the percentage
                trackBarIngameLevel_Scroll(null, null); 
            }
        }

        private void trackBarIngameLevel_Scroll(object sender, EventArgs e)
        {
            _v.SetVibranceIngameLevel(trackBarIngameLevel.Value);
            labelIngameLevel.Text = _resolveLabelLevel(trackBarIngameLevel.Value);
        }

        private void trackBarShadowLevel_Scroll(object sender, EventArgs e)
        {
            labelShadowLevel.Text = $"{trackBarShadowLevel.Value}%";
        }

        private void trackBarGammaLevel_Scroll(object sender, EventArgs e)
        {
            float gamma = trackBarGammaLevel.Value / 100f;
            labelGammaLevel.Text = $"{gamma:F2}";
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public ApplicationSetting GetApplicationSetting()
        {
            return new ApplicationSetting(_sender.Text, _sender.Tag.ToString(), this.trackBarIngameLevel.Value, 
                (ResolutionModeWrapper)this.cBoxResolution.SelectedItem, this.checkBoxResolution.Checked, this.trackBarShadowLevel.Value, this.trackBarGammaLevel.Value / 100f);
        }

        private void checkBoxResolution_CheckedChanged(object sender, EventArgs e)
        {
            this.cBoxResolution.Enabled = this.checkBoxResolution.Checked;
        }
    }
}
