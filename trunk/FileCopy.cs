using System;
using System.Collections.Generic;
using System.Text;

namespace PortPod
{
    using System;
    using System.Runtime.InteropServices;
    public class FileCopy
    {
        #region "API Declaration"
        private enum FO_Func : uint
        {
            FO_MOVE = 0x0001,
            FO_COPY = 0x0002,
            FO_DELETE = 0x0003,
            FO_RENAME = 0x0004,
            FOF_ALLOWUNDO = 0x0040
        }

        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public FO_Func wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszProgressTitle;

        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHFileOperation([In] ref SHFILEOPSTRUCT lpFileOp);
        #endregion

        private static SHFILEOPSTRUCT _ShFile;
        /// <summary>
        /// Copies the files from source to target, showing the Progress Dialog
        /// </summary>
        /// <param name="sSource">Source from where the File(s) will be copied</param>
        /// <param name="sTarget">Target or Detination</param>
        public static void CopyFiles(string sSource, string sTarget)
        {
                _ShFile.wFunc = FO_Func.FO_COPY;
                _ShFile.fFlags = (ushort)FO_Func.FOF_ALLOWUNDO;
                _ShFile.pFrom = sSource;                
                _ShFile.pTo = sTarget;
                SHFileOperation(ref _ShFile);
        }
    }
}
