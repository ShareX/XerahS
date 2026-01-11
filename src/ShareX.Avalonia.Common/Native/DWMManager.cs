namespace XerahS.Common.Native
{
    public class DWMManager : IDisposable
    {
        private bool isDWMEnabled;
        private bool autoEnable;

        public DWMManager()
        {
            isDWMEnabled = NativeMethods.IsDWMEnabled();
        }

        public bool AutoDisable()
        {
            if (isDWMEnabled)
            {
                ChangeComposition(false);
                autoEnable = true;
                return true;
            }

            return false;
        }

        public void ChangeComposition(bool enable)
        {
            try
            {
                // DWM_EC moved to NativeEnums.cs
                NativeMethods.DwmEnableComposition(enable ? DWM_EC.DWM_EC_ENABLECOMPOSITION : DWM_EC.DWM_EC_DISABLECOMPOSITION);
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }
        }

        public void Dispose()
        {
            if (isDWMEnabled && autoEnable)
            {
                ChangeComposition(true);
                autoEnable = false;
            }
        }
    }
}
