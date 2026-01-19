using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace ADL_Automation
{
    public partial class ApiKeyForm : Form
    {

        public string SecretKey { get; private set; }
        public string SelectedEnvironment
        {
            get { return EnvComboBox.SelectedItem?.ToString(); }
        }
        public string SelectedUser
        {
            get { return UserComboBox.SelectedItem?.ToString(); }
        }
        private readonly string keyFilePath = "key.txt";
        private FileHandlers _fileHandlers = null;

        public ApiKeyForm()
        {
            try
            {
                InitializeComponent();
                Load += ApiKeyForm_Load;
                _fileHandlers = new FileHandlers();

            }
            catch(Exception ex) 
            {
                HelperFunctions.ShutEverythingDown($"Error occured while initialising key entering form. \nMessage: {ex.Message}");
            }
        }

        private void ApiKeyForm_Load(object sender, EventArgs e)
        {
            try
            {
                this.Icon = new Icon("Logo/FinalLogo.ico");
                EnvComboBox.SelectedItem = "UatCert"; // TODO: Change to live
                UserComboBox.SelectedItem = "Client"; // TODO: Change to server
                string existingKey = _fileHandlers.FetchApiKey(keyFilePath);
                if (existingKey != null)
                    txtKey.Text = existingKey;

            }
            catch(Exception ex)
            {
                HelperFunctions.ShutEverythingDown($"Error occured while API key form load. \nMessage: {ex.Message}");
            }
        }
        private void btnSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                string enteredKey = txtKey.Text.Trim();
                if (string.IsNullOrEmpty(enteredKey))
                {
                    MessageBox.Show("Please enter a valid key.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SecretKey = enteredKey;

                DialogResult = DialogResult.OK;
                Close();

            }
            catch(Exception ex)
            {
                HelperFunctions.ShutEverythingDown($"Error occured while fetching key from form. \nMessage: {ex.Message}"); 
            }
        }
    }
}
