using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace OpenMirror
{
    public sealed class MiracastStartResult
    {
        public bool Success { get; internal set; }
        public string Message { get; internal set; }
        public bool HasExternalSession { get; internal set; }
        public uint ProcessId { get; internal set; }
    }

    public static class MiracastIntegration
    {
        private sealed class Lease { internal string ApplicationId; internal long Started; }
        private static readonly Dictionary<uint, Lease> leases = new Dictionary<uint, Lease>();
        public static Task<MiracastStartResult> StartAsync(System.Windows.Window owner)
        {
            return StartAsync(owner, Loc.Language, "OpenMirror", 1920, 1080, 30, true);
        }

        public static async Task<MiracastStartResult> StartAsync(System.Windows.Window owner, string language, string receiverName, int width, int height, int fps, bool startFullscreen)
        {
            var support = await MiracastSupport.InspectAsync();
            if (!support.CanLaunch) return new MiracastStartResult { Message = Describe(support, language) };
            try
            {
                // Activation is explicit user work. This method never registers packages or changes Windows settings.
                string arguments = "--language=" + (language == "en" ? "en" : "it") + " --name64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(receiverName ?? "OpenMirror"))
                    + " --width=" + Math.Max(320, Math.Min(7680, width)) + " --height=" + Math.Max(240, Math.Min(4320, height))
                    + " --fps=" + Math.Max(1, Math.Min(60, fps)) + " --autostart=1 --fullscreen=" + (startFullscreen ? "1" : "0");
                uint pid = MiracastSupport.Activate(support.ApplicationId, arguments);
                using (var process = Process.GetProcessById((int)pid))
                    lock (leases) leases[pid] = new Lease { ApplicationId = support.ApplicationId, Started = process.StartTime.ToUniversalTime().Ticks };
                return new MiracastStartResult { Success = true, HasExternalSession = true, ProcessId = pid,
                    Message = language == "en" ? "OpenMirror Miracast opened in its own window. Select this PC in Smart View or Win+K and accept the request there. One device at a time."
                        : "OpenMirror Miracast aperto nella finestra dedicata. Scegli questo PC da Smart View o Win+K e accetta la richiesta. Un dispositivo alla volta." };
            }
            catch (Exception exception) { return new MiracastStartResult { Message = (language == "en" ? "Unable to open the Miracast receiver: " : "Impossibile aprire il ricevitore Miracast: ") + exception.Message }; }
        }

        public static async Task<string> StatusTextAsync()
        {
            return Describe(await MiracastSupport.InspectAsync(), Loc.Language);
        }

        public static bool IsRunning(uint pid)
        {
            Lease lease; lock (leases) if (!leases.TryGetValue(pid, out lease)) return false;
            try
            {
                using (var process = Process.GetProcessById((int)pid))
                    if (!process.HasExited && process.StartTime.ToUniversalTime().Ticks == lease.Started) return true;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { return true; } // Preserve the lock when Windows denies inspection.
            lock (leases) leases.Remove(pid);
            return false;
        }

        public static async Task StopAsync(uint pid)
        {
            if (!IsRunning(pid)) return;
            Lease lease; lock (leases) if (!leases.TryGetValue(pid, out lease)) return;
            // Ask our package to stop and exit through its normal lifecycle. Never terminate an arbitrary PID.
            MiracastSupport.Activate(lease.ApplicationId, "--stop");
            for (int retry = 0; retry < 50 && IsRunning(pid); retry++) await Task.Delay(100);
            if (IsRunning(pid)) throw new InvalidOperationException(Loc.T("Chiudi la finestra OpenMirror Miracast per terminare la sessione.", "Close the OpenMirror Miracast window to end the session."));
        }

        private static string Describe(MiracastAvailability support, string language)
        {
            if (language != "en") return support.Message;
            if (!support.ApiPresent) return "Miracast requires Windows 10 version 1903 or later and Wi-Fi Direct.";
            if (!support.HardwareSupported) return "Windows cannot confirm Miracast receiver support for this Wi-Fi adapter and driver (" + (support.WiFiStatus ?? "unknown") + ").";
            if (!support.CompanionInstalled) return "Wi-Fi Direct is supported. Install the bundled OpenMirror Miracast UWP component before receiving a screen; see bin/miracast/README.md.";
            return "Miracast receiver available in the dedicated OpenMirror window. One device at a time.";
        }
    }
}
