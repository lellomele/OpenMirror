using System;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenMirror;

// Standalone deterministic tests. These tests never capture the desktop or start FFmpeg.
internal static class CaptureTests
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);

    private static string[] Split(string commandLine)
    {
        int count;
        IntPtr pointer = CommandLineToArgvW(commandLine, out count);
        if (pointer == IntPtr.Zero) throw new Exception("CommandLineToArgvW failed.");
        try
        {
            string[] arguments = new string[count];
            for (int index = 0; index < count; index++)
                arguments[index] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, index * IntPtr.Size));
            return arguments;
        }
        finally { LocalFree(pointer); }
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }

    public static int Main()
    {
        try
        {
            string[] samples = { "", "ordinary", "a b", "a\"b", "C:\\folder with spaces\\", "\\\"", "é日本語", "x & echo nope", "line\nnext" };
            foreach (string sample in samples)
            {
                string[] parsed = Split("ffmpeg.exe " + Recorder.QuoteArgument(sample));
                Assert(parsed.Length == 2 && parsed[1] == sample, "Argument round-trip failed: " + sample);
            }
            string output = "C:\\a b\\recording.mp4";
            string microphone = "Microphone (A \"B\")";
            string[] audio = Split("ffmpeg.exe " + Recorder.BuildArguments(new Rectangle(-1920, -20, 641, 481), output, 30, microphone));
            Assert(Array.IndexOf(audio, "640x480") >= 0, "yuv420p dimensions must be even.");
            Assert(Array.IndexOf(audio, "-1920") >= 0 && Array.IndexOf(audio, "-20") >= 0, "Negative monitor offsets must be preserved.");
            Assert(Array.IndexOf(audio, "audio=" + microphone) >= 0, "Microphone names must survive Windows quoting.");
            Assert(audio[audio.Length - 1] == output, "Output filename must remain a single argument.");
            string silent = Recorder.BuildArguments(new Rectangle(0, 0, 1280, 720), output, 60, null);
            Assert(silent.IndexOf(" -an ", StringComparison.Ordinal) >= 0 && silent.IndexOf("dshow", StringComparison.Ordinal) < 0, "Silent capture must have no audio device input.");
            bool rejected = false;
            try { Recorder.BuildArguments(new Rectangle(0, 0, 1280, 720), output, 0, null); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Assert(rejected, "Invalid FPS must be rejected.");
            Assert(!Native.IsWindow(IntPtr.Zero), "Null handles must not be valid windows.");
            Console.WriteLine("Capture tests passed: quoting round-trips, multi-monitor bounds, dimensions, audio and FPS validation.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
