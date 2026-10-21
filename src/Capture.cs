using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace OpenMirror
{
    public static class Native
    {
        private delegate bool EnumWindowProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll", EntryPoint = "IsWindow")] private static extern bool IsWindowNative(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out RECT rectangle);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] private static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

        public static bool IsWindow(IntPtr window) { return window != IntPtr.Zero && IsWindowNative(window); }

        public static IntPtr FindVideoWindow(int processId)
        {
            IntPtr best = IntPtr.Zero;
            long bestArea = -1;
            EnumWindows(delegate(IntPtr window, IntPtr ignored)
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner != (uint)processId || !IsWindowVisible(window)) return true;
                StringBuilder className = new StringBuilder(256);
                StringBuilder caption = new StringBuilder(512);
                GetClassName(window, className, className.Capacity);
                GetWindowText(window, caption, caption.Capacity);
                string kind = className.ToString();
                if (kind.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("PseudoConsoleWindow", StringComparison.OrdinalIgnoreCase) ||
                    (kind.Length == 0 && caption.Length == 0)) return true;
                RECT rect;
                if (!GetClientRect(window, out rect)) return true;
                long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                if (area > 0 && area > bestArea) { best = window; bestArea = area; }
                return true;
            }, IntPtr.Zero);
            return best;
        }

        public static void Attach(IntPtr child, IntPtr parent)
        {
            if (!IsWindow(child) || !IsWindow(parent)) throw new ArgumentException(Loc.T("La finestra video non è disponibile.", "The video window is unavailable."));
            const int GWL_STYLE = -16;
            int originalStyle = GetWindowLong(child, GWL_STYLE);
            int style = originalStyle;
            style &= unchecked((int)~(0x80000000u | 0x00C00000u | 0x00040000u | 0x00800000u));
            style |= 0x40000000; // WS_CHILD
            SetWindowLong(child, GWL_STYLE, style);
            // A null return may mean that the previous parent was the desktop,
            // so verify the resulting relationship rather than the return value.
            SetParent(child, parent);
            int error = Marshal.GetLastWin32Error();
            if (GetParent(child) != parent)
            {
                if (IsWindow(child))
                {
                    SetWindowLong(child, GWL_STYLE, originalStyle);
                    SetWindowPos(child, IntPtr.Zero, 0, 0, 0, 0, 0x0027);
                }
                if (error != 0) throw new System.ComponentModel.Win32Exception(error, Loc.T("Impossibile integrare la finestra video.", "Could not embed the video window."));
                throw new InvalidOperationException(Loc.T("Impossibile integrare la finestra video.", "Could not embed the video window."));
            }
            SetWindowPos(child, IntPtr.Zero, 0, 0, 0, 0, 0x0027); // frame changed, no move/size/z-order
            ShowWindow(child, 5);
        }

        public static void Resize(IntPtr child, int width, int height)
        {
            if (!IsWindow(child)) return;
            SetWindowPos(child, IntPtr.Zero, 0, 0, Math.Max(1, width), Math.Max(1, height), 0x0014);
        }

        public static void Screenshot(Control target, string path)
        {
            Rectangle bounds = GetCaptureBounds(target);
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        internal static Rectangle GetCaptureBounds(Control target)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (target.IsDisposed || !target.IsHandleCreated) throw new InvalidOperationException(Loc.T("La superficie video non è disponibile.", "The video surface is unavailable."));
            if (target.InvokeRequired)
                return (Rectangle)target.Invoke(new Func<Rectangle>(delegate { return GetCaptureBounds(target); }));
            if (!target.Visible || target.ClientSize.Width < 2 || target.ClientSize.Height < 2)
                throw new InvalidOperationException(Loc.T("La superficie video deve essere visibile.", "The video surface must be visible."));
            return target.RectangleToScreen(target.ClientRectangle);
        }
    }

    /// <summary>Records the visible desktop rectangle of a video control. Occluding windows are recorded too.</summary>
    public sealed class Recorder : IDisposable
    {
        private sealed class Session
        {
            internal Process Process;
            internal SafeFileHandle Job;
            internal string Output;
            internal readonly StringBuilder Errors = new StringBuilder();
            internal readonly TaskCompletionSource<int> Exit = new TaskCompletionSource<int>();
            internal readonly TaskCompletionSource<bool> OutputClosed = new TaskCompletionSource<bool>();
            internal readonly TaskCompletionSource<bool> ErrorClosed = new TaskCompletionSource<bool>();
            internal readonly TaskCompletionSource<bool> Released = new TaskCompletionSource<bool>();
            internal int Frames;
            internal bool Stopping;
            internal bool Forced;
        }

        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private Session current;
        private volatile bool recording;
        private volatile bool disposed;
        public bool IsRecording { get { return recording; } }
        public event Action<string> Error;
        public event Action<string> Completed;

        public async Task StartAsync(Control target, string ffmpegPath, string output, int fps, string microphone)
        {
            Rectangle bounds = Native.GetCaptureBounds(target);
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (disposed) throw new ObjectDisposedException("Recorder");
                if (current != null) throw new InvalidOperationException(Loc.T("È già in corso una registrazione.", "A recording is already in progress."));
                if (!File.Exists(ffmpegPath)) throw new FileNotFoundException(Loc.T("FFmpeg non è disponibile.", "FFmpeg is unavailable."), ffmpegPath);
                string fullOutput = Path.GetFullPath(output);
                if (String.Equals(Path.GetFullPath(ffmpegPath), fullOutput, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(Loc.T("Il file di registrazione non può sostituire FFmpeg.", "The recording file cannot overwrite FFmpeg."), "output");
                Directory.CreateDirectory(Path.GetDirectoryName(fullOutput));
                Session session = new Session();
                session.Output = fullOutput;
                Process process = new Process();
                session.Process = process;
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = BuildArguments(bounds, fullOutput, fps, microphone),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                process.EnableRaisingEvents = true;
                process.Exited += delegate { try { session.Exit.TrySetResult(process.ExitCode); } catch (InvalidOperationException) { } };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (args.Data == null) { session.ErrorClosed.TrySetResult(true); return; }
                    lock (session.Errors)
                    {
                        session.Errors.AppendLine(args.Data);
                        if (session.Errors.Length > 16000) session.Errors.Remove(0, session.Errors.Length - 12000);
                    }
                };
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (args.Data == null) { session.OutputClosed.TrySetResult(true); return; }
                    int frames;
                    if (args.Data != null && args.Data.StartsWith("frame=", StringComparison.Ordinal) &&
                        Int32.TryParse(args.Data.Substring(6).Trim(), out frames) && frames > 0)
                        Interlocked.Exchange(ref session.Frames, frames);
                };
                try
                {
                    if (!process.Start()) throw new InvalidOperationException(Loc.T("Impossibile avviare FFmpeg.", "Could not start FFmpeg."));
                    session.Job = OpenMirror.Session.CreateOwnedJob(process);
                    current = session;
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    if (await Task.WhenAny(session.Exit.Task, Task.Delay(700)).ConfigureAwait(false) == session.Exit.Task)
                    {
                        bool drained = await DrainAsync(session).ConfigureAwait(false);
                        if (!drained) throw new InvalidOperationException(FailureMessage(session, Loc.T("FFmpeg non ha avviato la registrazione. Chiusura dei canali di diagnostica scaduta.", "FFmpeg did not start recording. Diagnostic stream shutdown timed out.")));
                        throw new InvalidOperationException(FailureMessage(session, Loc.T("FFmpeg non ha avviato la registrazione.", "FFmpeg did not start recording.")));
                    }
                    if (disposed) throw new ObjectDisposedException("Recorder");
                    recording = true;
                    ObserveExitAsync(session);
                }
                catch
                {
                    recording = false;
                    if (current == session) current = null;
                    try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                    Release(session);
                    throw;
                }
            }
            finally { gate.Release(); }
        }

        public async Task StopAsync()
        {
            string notice = null;
            bool success = false;
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                Session session = current;
                if (session == null) return;
                session.Stopping = true;
                if (!session.Exit.Task.IsCompleted)
                {
                    // A blocked stdin must not postpone the ten-second shutdown deadline.
                    SendQuitAsync(session);
                    if (await Task.WhenAny(session.Exit.Task, Task.Delay(10000)).ConfigureAwait(false) != session.Exit.Task)
                    {
                        session.Forced = true;
                        try { session.Process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                        // Closing our job also terminates children that may retain pipe handles.
                        if (session.Job != null) { session.Job.Dispose(); session.Job = null; }
                    }
                }
                notice = await FinishAsync(session).ConfigureAwait(false);
                success = notice == null;
                if (success) notice = session.Output;
            }
            finally { gate.Release(); }
            Notify(success ? Completed : Error, notice);
        }

        private async void ObserveExitAsync(Session session)
        {
            try
            {
                await Task.WhenAny(session.Exit.Task, session.Released.Task).ConfigureAwait(false);
                if (session.Released.Task.IsCompleted) return;
                string notice = null;
                bool success = false;
                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (current != session) return;
                    notice = await FinishAsync(session).ConfigureAwait(false);
                    success = notice == null;
                    if (success) notice = session.Output;
                }
                finally { gate.Release(); }
                Notify(success ? Completed : Error, notice);
            }
            catch (Exception exception)
            {
                recording = false;
                Notify(Error, Loc.T("Errore durante la chiusura della registrazione: ", "Could not finalize the recording: ") + exception.Message);
            }
        }

        private async Task<string> FinishAsync(Session session)
        {
            try
            {
                if (await Task.WhenAny(session.Exit.Task, Task.Delay(2000)).ConfigureAwait(false) != session.Exit.Task)
                    return FailureMessage(session, Loc.T("FFmpeg non ha confermato la chiusura dopo l'arresto forzato. Il file potrebbe essere incompleto.", "FFmpeg did not confirm exit after forced shutdown. The file may be incomplete."));
                int exitCode = await session.Exit.Task.ConfigureAwait(false);
                if (!await DrainAsync(session).ConfigureAwait(false))
                    return FailureMessage(session, Loc.T("Chiusura dei canali di diagnostica di FFmpeg scaduta. Il file potrebbe essere incompleto.", "FFmpeg diagnostic stream shutdown timed out. The file may be incomplete."));
                if (session.Forced) return FailureMessage(session, Loc.T("FFmpeg non si è arrestato in tempo. Il file potrebbe essere incompleto.", "FFmpeg did not stop in time. The file may be incomplete."));
                if (exitCode != 0) return FailureMessage(session, Loc.T("Registrazione interrotta da FFmpeg (codice ", "Recording interrupted by FFmpeg (code ") + exitCode + ").");
                if (!session.Stopping) return FailureMessage(session, Loc.T("FFmpeg si è chiuso prima dell'arresto richiesto.", "FFmpeg exited before recording was stopped."));
                if (Interlocked.CompareExchange(ref session.Frames, 0, 0) <= 0 ||
                    !File.Exists(session.Output) || new FileInfo(session.Output).Length == 0)
                    return FailureMessage(session, Loc.T("La registrazione non contiene fotogrammi validi.", "The recording contains no valid frames."));
                return null;
            }
            finally
            {
                recording = false;
                if (current == session) current = null;
                Release(session);
            }
        }

        private static async Task<bool> DrainAsync(Session session)
        {
            // EOF callbacks drain both redirected streams without an unbounded WaitForExit.
            Task closed = Task.WhenAll(session.OutputClosed.Task, session.ErrorClosed.Task);
            return await Task.WhenAny(closed, Task.Delay(2000)).ConfigureAwait(false) == closed;
        }

        private static async void SendQuitAsync(Session session)
        {
            try
            {
                await session.Process.StandardInput.WriteLineAsync("q").ConfigureAwait(false);
                await session.Process.StandardInput.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception) { /* Exit, disposal or a broken pipe may race the quit request. */ }
        }

        private static void Release(Session session)
        {
            session.Released.TrySetResult(true);
            if (session.Job != null) { session.Job.Dispose(); session.Job = null; }
            try { session.Process.CancelOutputRead(); } catch (InvalidOperationException) { }
            try { session.Process.CancelErrorRead(); } catch (InvalidOperationException) { }
            session.Process.Dispose();
        }

        private static string FailureMessage(Session session, string message)
        {
            lock (session.Errors)
            {
                string details = session.Errors.ToString().Trim();
                return details.Length == 0 ? message : message + Environment.NewLine + details;
            }
        }

        private static void Notify(Action<string> handler, string value)
        {
            // Events run on a worker thread; subscribers marshal updates to their UI.
            if (handler == null || value == null) return;
            foreach (Action<string> callback in handler.GetInvocationList())
                try { callback(value); } catch (Exception) { /* A subscriber must not break recorder cleanup. */ }
        }

        public static string BuildArguments(Rectangle bounds, string output, int fps, string microphone)
        {
            if (bounds.Width < 2 || bounds.Height < 2) throw new ArgumentException(Loc.T("La superficie video è troppo piccola.", "The video surface is too small."), "bounds");
            if (fps < 1 || fps > 60) throw new ArgumentOutOfRangeException("fps", Loc.T("Il frame rate deve essere compreso fra 1 e 60.", "The frame rate must be between 1 and 60."));
            if (String.IsNullOrWhiteSpace(output)) throw new ArgumentException(Loc.T("Specificare il file di destinazione.", "Specify the output file."), "output");
            int width = bounds.Width & ~1;
            int height = bounds.Height & ~1;
            StringBuilder arguments = new StringBuilder("-hide_banner -y -nostats -progress pipe:1 -thread_queue_size 512 -f gdigrab -framerate ");
            arguments.Append(fps).Append(" -draw_mouse 0 -offset_x ").Append(bounds.X)
                .Append(" -offset_y ").Append(bounds.Y).Append(" -video_size ").Append(width).Append('x').Append(height).Append(" -i desktop ");
            if (!String.IsNullOrWhiteSpace(microphone))
                arguments.Append("-thread_queue_size 512 -f dshow -i ").Append(QuoteArgument("audio=" + microphone)).Append(" -map 0:v:0 -map 1:a:0 -c:a aac -b:a 128k ");
            else arguments.Append("-map 0:v:0 -an ");
            arguments.Append("-c:v libx264 -preset ultrafast -crf 23 -pix_fmt yuv420p -movflags +faststart -f mp4 ").Append(QuoteArgument(output));
            return arguments.ToString();
        }

        public static string QuoteArgument(string argument)
        {
            if (argument == null) throw new ArgumentNullException("argument");
            // Windows CRT quoting: backslashes are doubled before quotes and before the final quote.
            StringBuilder quoted = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') quoted.Append('\\', slashes * 2 + 1).Append('"');
                else quoted.Append('\\', slashes).Append(character);
                slashes = 0;
            }
            quoted.Append('\\', slashes * 2).Append('"');
            return quoted.ToString();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Do not synchronously wait on the UI thread. The normal close flow should await StopAsync.
            StopAsync().ContinueWith(delegate(Task task) { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
