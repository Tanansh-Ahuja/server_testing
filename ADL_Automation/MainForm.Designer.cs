namespace ADL_Automation
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.RootPanel = new System.Windows.Forms.Panel();
            StopButton = new System.Windows.Forms.Button();
            StartButton = new System.Windows.Forms.Button();
            FeedStatusLabel = new System.Windows.Forms.Label();
            this.NoOfTicksTextBox = new System.Windows.Forms.TextBox();
            this.ConversionFactorTextBox = new System.Windows.Forms.TextBox();
            this.NoOfTicksLabel = new System.Windows.Forms.Label();
            this.ConversionFactorLabel = new System.Windows.Forms.Label();
            this.RootPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // RootPanel
            // 
            this.RootPanel.Controls.Add(StopButton);
            this.RootPanel.Controls.Add(StartButton);
            this.RootPanel.Controls.Add(FeedStatusLabel);
            this.RootPanel.Controls.Add(this.NoOfTicksTextBox);
            this.RootPanel.Controls.Add(this.ConversionFactorTextBox);
            this.RootPanel.Controls.Add(this.NoOfTicksLabel);
            this.RootPanel.Controls.Add(this.ConversionFactorLabel);
            this.RootPanel.Location = new System.Drawing.Point(9, 15);
            this.RootPanel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.RootPanel.Name = "RootPanel";
            this.RootPanel.Size = new System.Drawing.Size(374, 219);
            this.RootPanel.TabIndex = 0;
            // 
            // StopButton
            // 
            StopButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            StopButton.Location = new System.Drawing.Point(153, 176);
            StopButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            StopButton.Name = "StopButton";
            StopButton.Size = new System.Drawing.Size(110, 34);
            StopButton.TabIndex = 6;
            StopButton.Text = "Stop";
            StopButton.UseVisualStyleBackColor = true;
            StopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // StartButton
            // 
            StartButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            StartButton.Location = new System.Drawing.Point(25, 176);
            StartButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            StartButton.Name = "StartButton";
            StartButton.Size = new System.Drawing.Size(110, 34);
            StartButton.TabIndex = 5;
            StartButton.Text = "Start";
            StartButton.UseVisualStyleBackColor = true;
            StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // FeedStatusLabel
            // 
            FeedStatusLabel.AutoSize = true;
            FeedStatusLabel.BackColor = System.Drawing.Color.Red;
            FeedStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            FeedStatusLabel.Location = new System.Drawing.Point(21, 11);
            FeedStatusLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            FeedStatusLabel.Name = "FeedStatusLabel";
            FeedStatusLabel.Size = new System.Drawing.Size(88, 20);
            FeedStatusLabel.TabIndex = 4;
            FeedStatusLabel.Text = "Neon Feed";
            // 
            // NoOfTicksTextBox
            // 
            this.NoOfTicksTextBox.Location = new System.Drawing.Point(162, 92);
            this.NoOfTicksTextBox.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.NoOfTicksTextBox.Name = "NoOfTicksTextBox";
            this.NoOfTicksTextBox.Size = new System.Drawing.Size(183, 20);
            this.NoOfTicksTextBox.TabIndex = 3;
            // 
            // ConversionFactorTextBox
            // 
            this.ConversionFactorTextBox.Location = new System.Drawing.Point(162, 52);
            this.ConversionFactorTextBox.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.ConversionFactorTextBox.Name = "ConversionFactorTextBox";
            this.ConversionFactorTextBox.Size = new System.Drawing.Size(183, 20);
            this.ConversionFactorTextBox.TabIndex = 2;
            // 
            // NoOfTicksLabel
            // 
            this.NoOfTicksLabel.AutoSize = true;
            this.NoOfTicksLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NoOfTicksLabel.Location = new System.Drawing.Point(21, 92);
            this.NoOfTicksLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.NoOfTicksLabel.Name = "NoOfTicksLabel";
            this.NoOfTicksLabel.Size = new System.Drawing.Size(127, 20);
            this.NoOfTicksLabel.TabIndex = 1;
            this.NoOfTicksLabel.Text = "Number of Ticks:";
            // 
            // ConversionFactorLabel
            // 
            this.ConversionFactorLabel.AutoSize = true;
            this.ConversionFactorLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ConversionFactorLabel.Location = new System.Drawing.Point(21, 50);
            this.ConversionFactorLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.ConversionFactorLabel.Name = "ConversionFactorLabel";
            this.ConversionFactorLabel.Size = new System.Drawing.Size(137, 20);
            this.ConversionFactorLabel.TabIndex = 0;
            this.ConversionFactorLabel.Text = "Conversion factor:";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(394, 245);
            this.Controls.Add(this.RootPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "MainForm";
            this.Text = "Server Algo";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.RootPanel.ResumeLayout(false);
            this.RootPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel RootPanel;
        private System.Windows.Forms.Label NoOfTicksLabel;
        private System.Windows.Forms.Label ConversionFactorLabel;
        private System.Windows.Forms.TextBox NoOfTicksTextBox;
        private System.Windows.Forms.TextBox ConversionFactorTextBox;
        private static System.Windows.Forms.Label FeedStatusLabel;
        private static System.Windows.Forms.Button StopButton;
        private static System.Windows.Forms.Button StartButton;
    }
}

