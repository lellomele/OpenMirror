using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OpenMirror
{
    public sealed class MiracastAvailability
    {
        public bool ApiPresent { get; internal set; }
        public bool HardwareSupported { get; internal set; }
        public bool CompanionInstalled { get; internal set; }
        public string WiFiStatus { get; internal set; }
        public string ListeningStatus { get; internal set; }
        public int MaxConnections { get; internal set; }
        public string ApplicationId { get; internal set; }
        public string Message { get; internal set; }
        public bool CanLaunch { get { return ApiPresent && HardwareSupported && CompanionInstalled; } }
    }

    /// <summary>Read-only Windows capability discovery and explicit activation of our UWP receiver.</summary>
    public static class MiracastSupport
    {
        public const string PackageName = "OpenMirror.Miracast";
        public const string PackagePublisher = "CN=OpenMirror";

        public static Task<MiracastAvailability> InspectAsync()
        {
            return Task.Run(new Func<MiracastAvailability>(Inspect));
        }

        public static MiracastAvailability Inspect()
        {
            var result = new MiracastAvailability();
            try
            {
                Type type = Type.GetType("Windows.Media.Miracast.MiracastReceiver, Windows.Media, ContentType=WindowsRuntime", false);
                if (type == null)
                {
                    result.Message = "Miracast richiede Windows 10 versione 1903 o successiva e Wi-Fi Direct.";
                    return result;
                }
                result.ApiPresent = true;
                object receiver = Activator.CreateInstance(type);
                object status = type.GetMethod("GetStatus").Invoke(receiver, null);
                result.WiFiStatus = Convert.ToString(Property(status, "WiFiStatus"));
                result.ListeningStatus = Convert.ToString(Property(status, "ListeningStatus"));
                result.MaxConnections = Convert.ToInt32(Property(status, "MaxSimultaneousConnections"));
                result.HardwareSupported = result.WiFiStatus == "MiracastSupported" || result.WiFiStatus == "MiracastSupportNotOptimized";
                if (!result.HardwareSupported)
                {
                    result.Message = result.WiFiStatus == "MiracastNotSupported"
                        ? "L'adattatore Wi-Fi o il driver non supporta la ricezione Miracast."
                        : "Windows non ha ancora determinato il supporto Miracast dell'adattatore Wi-Fi.";
                    return result;
                }
                result.ApplicationId = FindCompanionApplicationId();
                result.CompanionInstalled = !String.IsNullOrEmpty(result.ApplicationId);
                result.Message = result.CompanionInstalled
                    ? "Ricevitore Miracast disponibile. Si apre nella finestra del componente Windows dedicato."
                    : "Wi-Fi Direct supportato. Installa il componente OpenMirror Miracast UWP incluso in bin/miracast prima di ricevere uno schermo; vedi bin/miracast/README.md.";
                if (result.WiFiStatus == "MiracastSupportNotOptimized")
                    result.Message += " Il driver segnala possibili problemi di compatibilità Miracast.";
            }
            catch (Exception exception)
            {
                if (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
                result.Message = "Impossibile verificare Miracast: " + exception.Message;
            }
            return result;
        }

        private static object Property(object target, string name)
        {
            return target.GetType().GetProperty(name).GetValue(target, null);
        }

        private static string FindCompanionApplicationId()
        {
            Type type = Type.GetType("Windows.Management.Deployment.PackageManager, Windows.Management, ContentType=WindowsRuntime", false);
            if (type == null) return null;
            object manager = Activator.CreateInstance(type);
            MethodInfo find = type.GetMethod("FindPackagesForUser", new Type[] { typeof(string), typeof(string), typeof(string) });
            IEnumerable packages = (IEnumerable)find.Invoke(manager, new object[] { "", PackageName, PackagePublisher });
            foreach (object package in packages)
            {
                object id = Property(package, "Id");
                string family = Convert.ToString(Property(id, "FamilyName"));
                if (!String.IsNullOrEmpty(family)) return family + "!App";
            }
            return null;
        }

        public static uint LaunchCompanion()
        {
            return LaunchCompanion("");
        }

        public static uint LaunchCompanion(string arguments)
        {
            MiracastAvailability availability = Inspect();
            if (!availability.CanLaunch) throw new InvalidOperationException(availability.Message);
            return Activate(availability.ApplicationId, arguments);
        }

        internal static uint Activate(string applicationId, string arguments)
        {
            object instance = new ApplicationActivationManager();
            try
            {
                uint processId;
                int error = ((IApplicationActivationManager)instance).ActivateApplication(applicationId, arguments, 2, out processId);
                Marshal.ThrowExceptionForHR(error);
                return processId;
            }
            finally { Marshal.FinalReleaseComObject(instance); }
        }

        [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
        private class ApplicationActivationManager { }
        [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationActivationManager
        {
            [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string applicationId, [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
        }
    }
}
