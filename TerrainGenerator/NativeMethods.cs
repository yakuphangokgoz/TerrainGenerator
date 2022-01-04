using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TerrainGenerator
{
    
    public class NativeMethods : Form

    {

        // ...

        private static readonly Int32 WM_SETREDRAW = 0x000b;


        public void SuspendDrawing(Control c)

        {

            if (c == null)

                throw new ArgumentNullException("c");

            NativeMethods.SendMessage(c.Handle, WM_SETREDRAW, (Int32)0, (Int32)0);

        }



        public void ResumeDrawing(Control c)

        {

            if (c == null)

                throw new ArgumentNullException("c");

            NativeMethods.SendMessage(c.Handle, WM_SETREDRAW, (Int32)1, (Int32)0);

            c.Refresh();

        }
        /*
        [System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        
        */
        [DllImport("User32")]
        private static extern IntPtr SendMessage(IntPtr hWnd, Int32 msg, Int32 wParam, Int32 lParam);

        

    }
}
