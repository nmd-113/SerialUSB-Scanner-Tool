using System;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;

namespace SerialUSB_Scanner_Tool
{
    public partial class SerialUSBScanner : Form
    {
        private bool _isListening;
        private readonly int[] _bauds = { 4800, 9600, 19200, 38400, 57600, 115200 };
        private ComKey _transfer;
        private bool _isLoadingSettings = false;

        private bool _dragging;
        private Point _dragCursorPoint;
        private Point _dragFormPoint;

        public SerialUSBScanner()
        {
            InitializeComponent();
            KeyPreview = true;
            KeyDown += OnKeyDown;

            MouseDown += Form_MouseDown;
            MouseMove += Form_MouseMove;
            MouseUp += Form_MouseUp;

            comport.SelectedIndexChanged += Comport_SelectedIndexChanged;
            comspeed.SelectedIndexChanged += Comspeed_SelectedIndexChanged;
        }

        private void Comport_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoadingSettings) return;

            if (comport.SelectedItem is string selectedPort)
            {
                Debug.WriteLine($"Saving COMPORT: {selectedPort}");
                SaveUserSetting("COMPORT", selectedPort);
            }
        }

        private void Comspeed_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoadingSettings) return;

            if (comspeed.SelectedItem is int selectedBaud)
            {
                Debug.WriteLine($"Saving BAUDS: {selectedBaud}");
                SaveUserSetting("BAUDS", selectedBaud);
            }
            else if (int.TryParse(comspeed.SelectedItem?.ToString(), out int parsedBaud))
            {
                Debug.WriteLine($"Saving BAUDS: {parsedBaud}");
                SaveUserSetting("BAUDS", parsedBaud);
            }
        }

        private void SerialUSBScanner_Load(object sender, EventArgs e)
        {
            _isLoadingSettings = true;

            PopulatePortList();
            PopulateBaudList();

            Debug.WriteLine("Loading saved settings...");
            Debug.WriteLine("Saved COMPORT: " + Properties.Settings.Default.COMPORT);
            Debug.WriteLine("Saved BAUDS: " + Properties.Settings.Default.BAUDS);

            string savedPort = Properties.Settings.Default.COMPORT;
            if (!string.IsNullOrWhiteSpace(savedPort))
            {
                int index = comport.Items.IndexOf(savedPort);
                Debug.WriteLine($"COMPORT index found: {index}");
                comport.SelectedIndex = index >= 0 ? index : 0;
            }
            else
            {
                comport.SelectedIndex = 0;
            }

            int savedBaud = Properties.Settings.Default.BAUDS;
            if (_bauds.Contains(savedBaud))
            {
                Debug.WriteLine($"BAUDS value found: {savedBaud}");
                comspeed.SelectedItem = savedBaud;
            }
            else
            {
                Debug.WriteLine("BAUDS not found in list, selecting default 9600");
                comspeed.SelectedItem = 9600;
            }

            _isLoadingSettings = false;

            if (Properties.Settings.Default.AUTO == "AUTO")
            {
                ToggleConnection();
                if (_isListening)
                    BeginInvoke(new Action(MinimizeToTray));
            }
        }

        private void SaveUserSetting(string key, object value)
        {
            try
            {
                if (key == "COMPORT" && value is string portValue)
                    Properties.Settings.Default.COMPORT = portValue;
                else if (key == "BAUDS" && value is int baudValue)
                    Properties.Settings.Default.BAUDS = baudValue;

                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving setting {key}: {ex.Message}");
            }
        }

        private void PopulateBaudList()
        {
            comspeed.Items.Clear();
            foreach (int baud in _bauds)
            {
                comspeed.Items.Add(baud);
            }
            comspeed.SelectedItem = 9600;
        }

        private void PopulatePortList()
        {
            comport.Items.Clear();
            comport.Sorted = true;
            string[] ports = SerialPort.GetPortNames();
            comport.Items.AddRange(ports);

            if (comport.Items.Count > 0)
                comport.SelectedIndex = 0;
        }

        private void ToggleConnection()
        {
            _isListening = !_isListening;
            connectBtn.Text = _isListening ? "STOP" : "CONNECT";

            if (_isListening)
            {
                StartListening();
            }
            else
            {
                StopListening();
            }
        }

        private void StartListening()
        {
            StopListening();

            string portName = comport.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(portName))
            {
                MessageBox.Show("Please select a valid COM port.", "Invalid Port", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int baudRate = comspeed.SelectedItem is int b ? b : 9600;

            try
            {
                var serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                _transfer = new ComKey(serialPort);
                _transfer.Start();
                SetInterfaceState(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to start serial communication: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _isListening = false;
                connectBtn.Text = "CONNECT";
            }
        }

        private void StopListening()
        {
            if (_transfer != null)
            {
                _transfer.Stop();
                _transfer.Dispose();
                _transfer = null;
            }

            SetInterfaceState(true);
        }

        private void SetInterfaceState(bool enabled)
        {
            comspeed.Enabled = enabled;
            comport.Enabled = enabled;
        }

        private void MinimizeToTray()
        {
            if (notifyIcon1 != null)
            {
                Hide();
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(1000);
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }

        private void Form_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragCursorPoint = Cursor.Position;
                _dragFormPoint = Location;
            }
        }

        private void Form_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(_dragCursorPoint));
                Location = Point.Add(_dragFormPoint, new Size(diff));
            }
        }

        private void Form_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                _dragging = false;
        }

        private void exitBtn_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Are you sure you want to exit?",
                "Exit Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                StopListening();
                if (notifyIcon1 != null)
                    notifyIcon1.Visible = false;
                Application.Exit();
            }
        }

        private void hideBtn_Click(object sender, EventArgs e)
        {
            MinimizeToTray();
        }

        private void connectBtn_Click(object sender, EventArgs e)
        {
            ToggleConnection();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                Debug.Print("Enter key pressed.");
            }
        }
    }
}