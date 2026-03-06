import os
import re

def modify_file(filepath, callback):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    new_content = callback(content)
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(new_content)

def theme_vibrancegui(content):
    # Add UI explanatory label about full desktop vibrance
    if 'this.labelGlobalInfo = new System.Windows.Forms.Label();' not in content:
        content = content.replace('this.components = new System.ComponentModel.Container();', 
                                  'this.components = new System.ComponentModel.Container();\n            this.labelGlobalInfo = new System.Windows.Forms.Label();')
        
        info_label_init = """            // 
            // labelGlobalInfo
            // 
            this.labelGlobalInfo.AutoSize = true;
            this.labelGlobalInfo.Location = new System.Drawing.Point(13, 400);
            this.labelGlobalInfo.Name = "labelGlobalInfo";
            this.labelGlobalInfo.Size = new System.Drawing.Size(350, 13);
            this.labelGlobalInfo.TabIndex = 20;
            this.labelGlobalInfo.Text = "Note: Vibrance is applied to the entire monitor while app is in focus.";
            this.labelGlobalInfo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
"""
        content = content.replace('// VibranceGUI', info_label_init + '            // VibranceGUI')
        content = content.replace('this.Controls.Add(this.observerStatusLabel);', 'this.Controls.Add(this.observerStatusLabel);\n            this.Controls.Add(this.labelGlobalInfo);')
        content = content.replace('private System.Windows.Forms.CheckBox checkBoxNeverChangeResolutions;', 'private System.Windows.Forms.CheckBox checkBoxNeverChangeResolutions;\n        private System.Windows.Forms.Label labelGlobalInfo;')

    # Fix gaps by moving controls up
    content = re.sub(r'this\.groupBox1\.Location = new System\.Drawing\.Point\(13, 82\);', 'this.groupBox1.Location = new System.Drawing.Point(13, 12);', content)
    content = re.sub(r'this\.groupBox5\.Location = new System\.Drawing\.Point\(13, 231\);', 'this.groupBox5.Location = new System.Drawing.Point(13, 161);', content)
    content = re.sub(r'this\.observerStatusLabel\.Location = new System\.Drawing\.Point\(12, 502\);', 'this.observerStatusLabel.Location = new System.Drawing.Point(12, 420);', content)
    content = re.sub(r'this\.statusLabel\.Location = new System\.Drawing\.Point\(106, 502\);', 'this.statusLabel.Location = new System.Drawing.Point(106, 420);', content)
    content = re.sub(r'this\.ClientSize = new System\.Drawing\.Size\((\d+), \d+\);', r'this.ClientSize = new System.Drawing.Size(\1, 450);', content)

    # Dark Mode Form
    content = re.sub(r'this\.AutoScaleDimensions = new System\.Drawing\.SizeF\(6F, 13F\);', 
                     r'this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));\n            this.ForeColor = System.Drawing.Color.White;\n            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);', content)
    
    # FlatStyle for buttons and checkboxes
    content = re.sub(r'(this\.(checkBox\w+|button\w+)\.UseVisualStyleBackColor = true;)', r'\1\n            this.\2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;\n            this.\2.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));', content)
    # Also for listApplications
    content = re.sub(r'this\.listApplications\.Location = ', r'this.listApplications.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(32)))));\n            this.listApplications.ForeColor = System.Drawing.Color.White;\n            this.listApplications.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;\n            this.listApplications.Location = ', content)
    
    return content

def theme_vibrancesettings(content):
    content = re.sub(r'this\.AutoScaleDimensions = new System\.Drawing\.SizeF\(6F, 13F\);', 
                     r'this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));\n            this.ForeColor = System.Drawing.Color.White;\n            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);', content)
    content = re.sub(r'(this\.(checkBox\w+|button\w+)\.UseVisualStyleBackColor = true;)', r'\1\n            this.\2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;\n            this.\2.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));', content)
    # Combobox
    content = re.sub(r'this\.cBoxResolution\.FormattingEnabled = true;', r'this.cBoxResolution.FormattingEnabled = true;\n            this.cBoxResolution.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(32)))));\n            this.cBoxResolution.ForeColor = System.Drawing.Color.White;\n            this.cBoxResolution.FlatStyle = System.Windows.Forms.FlatStyle.Flat;', content)
    return content

def theme_processexplorer(content):
    content = re.sub(r'this\.AutoScaleDimensions = new System\.Drawing\.SizeF\(6F, 13F\);', 
                     r'this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));\n            this.ForeColor = System.Drawing.Color.White;\n            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);', content)
    content = re.sub(r'(this\.(checkBox\w+|button\w+)\.UseVisualStyleBackColor = true;)', r'\1\n            this.\2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;\n            this.\2.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));', content)
    content = re.sub(r'this\.listView\.Location = ', r'this.listView.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(32)))));\n            this.listView.ForeColor = System.Drawing.Color.White;\n            this.listView.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;\n            this.listView.Location = ', content)
    return content


base_dir = r"c:\Users\ariel\Documents\VibranceGUI+\vibranceGUI\vibrance.GUI\common"
modify_file(os.path.join(base_dir, "VibranceGUI.Designer.cs"), theme_vibrancegui)
modify_file(os.path.join(base_dir, "VibranceSettings.Designer.cs"), theme_vibrancesettings)
modify_file(os.path.join(base_dir, "ProcessExplorer.Designer.cs"), theme_processexplorer)
print("Applied dark themes to Designer files.")
