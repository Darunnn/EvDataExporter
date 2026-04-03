namespace EvDataExporter
{
    internal static class Program
    {
        private const string MutexName = "Global\\EvDataExporter_SingleInstance";

        [STAThread]
        static void Main()
        {
            using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // โปรแกรมเปิดอยู่แล้ว — bring to front แล้วออก
                NativeMethods.BringExistingWindowToFront();
                return;
            }

            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new EvDataExporter());
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public static void BringExistingWindowToFront()
        {
            var current = System.Diagnostics.Process.GetCurrentProcess();
            var existing = System.Diagnostics.Process
                .GetProcessesByName(current.ProcessName)
                .FirstOrDefault(p => p.Id != current.Id);

            if (existing?.MainWindowHandle is IntPtr hwnd && hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
        }
    }
}