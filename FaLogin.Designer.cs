namespace FurryDownloader
{
    partial class FaLogin
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.WebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)(this.WebView)).BeginInit();
            this.SuspendLayout();
            // 
            // WebView
            // 
            this.WebView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.WebView.Location = new System.Drawing.Point(0, 0);
            this.WebView.MinimumSize = new System.Drawing.Size(20, 20);
            this.WebView.Name = "WebView";
            this.WebView.Size = new System.Drawing.Size(782, 753);
            this.WebView.TabIndex = 0;
            // 
            // FaLogin
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(782, 753);
            this.Controls.Add(this.WebView);
            this.Name = "FaLogin";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "登录";
            ((System.ComponentModel.ISupportInitialize)(this.WebView)).EndInit();
            this.ResumeLayout(false);
        }

        private Microsoft.Web.WebView2.WinForms.WebView2 WebView;
    }
}
