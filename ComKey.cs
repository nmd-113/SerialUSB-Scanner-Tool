using System;
using System.IO.Ports;
using System.Windows.Forms;

namespace SerialUSB_Scanner_Tool
{
    class ComKey : System.IDisposable
    {
        private readonly SerialPort _port;

        public ComKey(SerialPort port)
        {
            _port = port;
            _port.DataReceived += PortOnDataReceived;
        }

        private void PortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            while (_port.BytesToRead > 0)
            {
                var original = _port.ReadExisting();
                var reformattedString = DefaultFormatter.Reformat(original);
                try
                {
                    SendKeys.SendWait(reformattedString);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        public void Start()
        {
            if (!_port.IsOpen)
                _port.Open();
        }
        public void Stop()
        {
            if (_port.IsOpen)
                _port.Close();
        }
        public void Dispose()
        {
            if (_port.IsOpen)
                _port.Close();
            _port.DataReceived -= PortOnDataReceived;
            _port.Dispose();

        }
    }
}
