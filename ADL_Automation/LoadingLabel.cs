using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ADL_Automation
{
    public class LoadingLabel
    {
        public LoadingLabel() 
        {
 
        }

        
        public void InitialiseLoadingLabel(string showThis, MainForm form1, Panel RootPanel)
        {
            try
            {
                Globals.loadingLabel = new Label()
                {
                    Text = "Status: " + showThis + " ...",
                    AutoSize = false,
                    Font = new Font("Arial", 11),
                    Location = new Point(5, 5),
                    MaximumSize = new Size(450, 0),   // max width, unlimited height
                    Size = new Size(400, 100)         // initial height
                };
                form1.Controls.Add(Globals.loadingLabel);
                Globals.loadingLabel.BringToFront();

                // Hide the main tab (you can add more components here)
                RootPanel.Hide();

            }
            catch(Exception exception)
            {
                HelperFunctions.ShutEverythingDown($"Error occured while creating label. \nMessage: {exception.Message}");
            }
        }
        public void ChangeLoadingLabelText(string showThis)
        {
            try
            {
                Globals.loadingLabel.Text = "Status: " + showThis + " ...";

            }
            catch(Exception exception)
            {
                HelperFunctions.ShutEverythingDown($"Error occured while creating label. \nMessage: {exception.Message}");
            }
        }
        public static void ShowErrorLabel(string showThis)
        {
            try
            {
                if(Globals.loadingLabel!=null)
                {
                    Globals.loadingLabel.Text = "Status: " + showThis ;
                    Globals.loadingLabel.ForeColor = Color.Red;
                    Globals.loadingLabel.Show();

                }

            }
            catch (Exception exception)
            {
                HelperFunctions.ShutEverythingDown($"Error occured while creating label. \nMessage: {exception.Message}");
            }
        }
        
    }
}
