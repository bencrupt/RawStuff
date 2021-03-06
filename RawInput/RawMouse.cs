﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RawInput_dll
{
	public sealed class RawMouse
	{
		private readonly Dictionary<IntPtr, MouseEvent> _deviceList = new Dictionary<IntPtr, MouseEvent>();
		public delegate void DeviceEventHandler(object sender, RawInputMouseEventArg e);
		public event DeviceEventHandler MouseMoved;
		readonly object _padLock = new object();
		public int NumberOfMouses { get; private set; }
		static InputData _rawBuffer;

		public RawMouse(IntPtr hwnd, bool captureOnlyInForeground)
		{
			var rid = new RawInputDevice[1];

			rid[0].UsagePage = HidUsagePage.GENERIC;
			rid[0].Usage = HidUsage.Mouse;
			rid[0].Flags = (captureOnlyInForeground ? RawInputDeviceFlags.NONE : RawInputDeviceFlags.INPUTSINK) | RawInputDeviceFlags.DEVNOTIFY;
			rid[0].Target = hwnd;

			if (!Win32.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0])))
			{
				throw new ApplicationException("Failed to register raw input device(s).");
			}
		}

		public void EnumerateDevices()
		{
			lock (_padLock)
			{
				_deviceList.Clear();

				var mouseNumber = 0;

				var globalDevice = new MouseEvent
				{
					DeviceName = "Global Mouse",
					DeviceHandle = IntPtr.Zero,
					DeviceType = Win32.GetDeviceType(DeviceType.RimTypemouse),
					Name = "Fake Mouse. Some keys (ZOOM, MUTE, VOLUMEUP, VOLUMEDOWN) are sent to rawinput with a handle of zero.",
					Source = mouseNumber++.ToString(CultureInfo.InvariantCulture)
				};

				_deviceList.Add(globalDevice.DeviceHandle, globalDevice);

				var numberOfDevices = 0;
				uint deviceCount = 0;
				var dwSize = (Marshal.SizeOf(typeof(Rawinputdevicelist)));

				if (Win32.GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, (uint)dwSize) == 0)
				{
					var pRawInputDeviceList = Marshal.AllocHGlobal((int)(dwSize * deviceCount));
					Win32.GetRawInputDeviceList(pRawInputDeviceList, ref deviceCount, (uint)dwSize);

					for (var i = 0; i < deviceCount; i++)
					{
						uint pcbSize = 0;

						// On Window 8 64bit when compiling against .Net > 3.5 using .ToInt32 you will generate an arithmetic overflow. Leave as it is for 32bit/64bit applications
						var rid = (Rawinputdevicelist)Marshal.PtrToStructure(new IntPtr((pRawInputDeviceList.ToInt64() + (dwSize * i))), typeof(Rawinputdevicelist));

						Win32.GetRawInputDeviceInfo(rid.hDevice, RawInputDeviceInfo.RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);

						if (pcbSize <= 0) continue;

						var pData = Marshal.AllocHGlobal((int)pcbSize);
						Win32.GetRawInputDeviceInfo(rid.hDevice, RawInputDeviceInfo.RIDI_DEVICENAME, pData, ref pcbSize);
						var deviceName = Marshal.PtrToStringAnsi(pData);

						if (rid.dwType == DeviceType.RimTypemouse || rid.dwType == DeviceType.RimTypeHid)
						{
							var deviceDesc = Win32.GetDeviceDescription(deviceName);

							var dInfo = new MouseEvent
							{
								DeviceName = Marshal.PtrToStringAnsi(pData),
								DeviceHandle = rid.hDevice,
								DeviceType = Win32.GetDeviceType(rid.dwType),
								Name = deviceDesc,
								Source = mouseNumber++.ToString(CultureInfo.InvariantCulture)
							};

							if (!_deviceList.ContainsKey(rid.hDevice))
							{
								numberOfDevices++;
								_deviceList.Add(rid.hDevice, dInfo);
							}
						}

						Marshal.FreeHGlobal(pData);
					}

					Marshal.FreeHGlobal(pRawInputDeviceList);

					NumberOfMouses = numberOfDevices;
					Debug.WriteLine("EnumerateDevices() found {0} Mouse(s)", NumberOfMouses);
					return;
				}
			}

			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		public void ProcessRawInput(IntPtr hdevice)
		{
			//Debug.WriteLine(_rawBuffer.data.keyboard.ToString());
			//Debug.WriteLine(_rawBuffer.data.hid.ToString());
			//Debug.WriteLine(_rawBuffer.header.ToString());

			if (_deviceList.Count == 0) return;

			var dwSize = 0;
			Win32.GetRawInputData(hdevice, DataCommand.RID_INPUT, IntPtr.Zero, ref dwSize, Marshal.SizeOf(typeof(Rawinputheader)));

			if (dwSize != Win32.GetRawInputData(hdevice, DataCommand.RID_INPUT, out _rawBuffer, ref dwSize, Marshal.SizeOf(typeof(Rawinputheader))))
			{
				Debug.WriteLine("Error getting the rawinput buffer");
				return;
			}

			int virtualKey = _rawBuffer.data.mouse.usButtonData;
			int makeCode = _rawBuffer.data.keyboard.Makecode;
			int flags = _rawBuffer.data.keyboard.Flags;

			//if (virtualKey == Win32.KEYBOARD_OVERRUN_MAKE_CODE) return;

			//var isE0BitSet = ((flags & Win32.RI_KEY_E0) != 0);

			MouseEvent mouseEvent;

			if (_deviceList.ContainsKey(_rawBuffer.header.hDevice))
			{
				lock (_padLock)
				{
					mouseEvent = _deviceList[_rawBuffer.header.hDevice];
				}
			}
			else
			{
				Debug.WriteLine("Handle: {0} was not in the device list.", _rawBuffer.header.hDevice);
				return;
			}

			//var isBreakBitSet = ((flags & Win32.RI_KEY_BREAK) != 0);

			//mouseEvent.KeyPressState = isBreakBitSet ? "BREAK" : "MAKE";
			//mouseEvent.Message = _rawBuffer.data.mouse.messa.keyboard.Message;
			//mouseEvent.VKeyName = KeyMapper.GetKeyName(VirtualKeyCorrection(virtualKey, isE0BitSet, makeCode)).ToUpper();
			//mouseEvent.VKey = virtualKey;

			if (MouseMoved != null)
			{
				MouseMoved(this, new RawInputMouseEventArg(mouseEvent));
			}
		}

		//private static int VirtualKeyCorrection(int virtualKey, bool isE0BitSet, int makeCode)
		//{
		//	var correctedVKey = virtualKey;

		//	if (_rawBuffer.header.hDevice == IntPtr.Zero)
		//	{
		//		// When hDevice is 0 and the vkey is VK_CONTROL indicates the ZOOM key
		//		if (_rawBuffer.data.keyboard.VKey == Win32.VK_CONTROL)
		//		{
		//			correctedVKey = Win32.VK_ZOOM;
		//		}
		//	}
		//	else
		//	{
		//		switch (virtualKey)
		//		{
		//			// Right-hand CTRL and ALT have their e0 bit set 
		//			case Win32.VK_CONTROL:
		//				correctedVKey = isE0BitSet ? Win32.VK_RCONTROL : Win32.VK_LCONTROL;
		//				break;
		//			case Win32.VK_MENU:
		//				correctedVKey = isE0BitSet ? Win32.VK_RMENU : Win32.VK_LMENU;
		//				break;
		//			case Win32.VK_SHIFT:
		//				correctedVKey = makeCode == Win32.SC_SHIFT_R ? Win32.VK_RSHIFT : Win32.VK_LSHIFT;
		//				break;
		//			default:
		//				correctedVKey = virtualKey;
		//				break;
		//		}
		//	}

		//	return correctedVKey;
		//}
	}
}
