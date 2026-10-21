namespace OpenMirror {
    internal static class Edition {
        internal static bool AirPlayOnly {
            get {
#if EXPERIMENTAL_PROTOCOLS
                return false;
#else
                return true;
#endif
            }
        }
    }
}
