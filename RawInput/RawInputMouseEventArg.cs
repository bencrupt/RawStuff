using System;

namespace RawInput_dll
{
    public class RawInputMouseEventArg : EventArgs
    {
        public RawInputMouseEventArg(MouseEvent arg)
        {
            MouseEvent = arg;
        }

        public MouseEvent MouseEvent { get; private set; }
    }
}
