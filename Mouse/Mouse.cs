using System;
using RawInput_dll;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Mouse.Properties;
using System.Drawing;

namespace Mouse
{
    public partial class Mouse : Form
    {
        [DllImport("user32.dll")]
        public static extern Int32 SwapMouseButton(Int32 bSwap);

        private readonly RawMouseInput _rawMouseInput;
        private IntPtr _rightMouse = (IntPtr)0;
        private bool _isRight = false;
        private NotifyIcon _ni;
        const bool CaptureOnlyInForeground = false;

        public Mouse()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            _rawMouseInput = new RawMouseInput(Handle, CaptureOnlyInForeground);

            _rawMouseInput.AddMessageFilter();   // Adding a message filter will cause keypresses to be handled
            //Win32.DeviceAudit();            // Writes a file DeviceAudit.txt to the current directory
            if (_rawMouseInput.NumberOfMouses > 1)
                _rawMouseInput.MouseMoved += OnMouseMoved;
        }

        private void OnMouseMoved(object sender, RawInputMouseEventArg e)
        {
            lbHandle.Text = e.MouseEvent.DeviceHandle.ToString();
            lbType.Text = e.MouseEvent.DeviceType;
            lbName.Text = e.MouseEvent.DeviceName;
            lbDescription.Text = e.MouseEvent.Name;
            lbNumKeyboards.Text = _rawMouseInput.NumberOfMouses.ToString(CultureInfo.InvariantCulture);
            lbSource.Text = e.MouseEvent.Source;

            if (_rightMouse == (IntPtr)0)
                _rightMouse = e.MouseEvent.DeviceHandle;

            if (e.MouseEvent.DeviceHandle == _rightMouse)
            {
                if (!_isRight)
                {
                    SwapMouseButton(0);
                    _isRight = true;
                    _ni.Icon = (Icon)Resources.ResourceManager.GetObject("right");
                }
            }
            else
            {
                if (_isRight)
                {
                    SwapMouseButton(1);
                    _isRight = false;
                    _ni.Icon = (Icon)Resources.ResourceManager.GetObject("left");
                }
            }
            //lbMessage.Text = string.Format("0x{0:X4} ({0})", e.MouseEvent.Message);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;

            if (null == ex) return;

            // Log this error. Logging the exception doesn't correct the problem but at least now
            // you may have more insight as to why the exception is being thrown.
            Debug.WriteLine("Unhandled Exception: " + ex.Message);
            Debug.WriteLine("Unhandled Exception: " + ex);
            MessageBox.Show(ex.Message);
        }

        private void Mouse_FormClosing(object sender, FormClosingEventArgs e)
        {
            _rawMouseInput.MouseMoved -= OnMouseMoved;
            _ni.Visible = false;
            _ni.Dispose();
        }

        private void Mouse_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            //this.ShowInTaskbar = false;
            _ni = new NotifyIcon();
            _ni.Text = "DualMouse";
            _ni.Icon = (Icon)Resources.ResourceManager.GetObject("question");
            _ni.Visible = true;
        }
    }
}
