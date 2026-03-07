using System;
using System.Runtime.InteropServices;
using vibrance.GUI.common;

namespace vibrance.GUI.NVIDIA
{
    /// <summary>
    /// Calls nvapi.dll directly via QueryInterface to set display gamma/brightness.
    /// Probes multiple candidate function IDs so we can find the right one
    /// regardless of driver version.
    /// </summary>
    internal static class NvidiaColorController
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QueryInterface_t(uint id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EnumDisplay_t(uint index, ref IntPtr hDisp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetDVCInfo_t(IntPtr hDisp, ref NvDisplayDvcInfo info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SetDVCLevel_t(IntPtr hDisp, int level);

        // Candidate color-info structs and delegates
        [StructLayout(LayoutKind.Sequential)]
        private struct NV_COLOR_INFO_V1
        {
            public uint version;      // struct size | (1u << 16)
            public uint brightness;   // 0-200, 50=default neutral
            public uint hue;          // 0-359
            public uint saturation;   // 0-200, 50=default neutral
            public uint colorimetry;  // 0=default
            public uint gamma;        // 100=1.0, range 100-480 in NVCPL
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ColorInfo_t(IntPtr hDisp, ref NV_COLOR_INFO_V1 info);

        // Known NVAPI function IDs
        private const uint ID_EnumDisplay      = 0x9ABDD40D;
        private const uint ID_GetDVCInfo       = 0x4085DE45; // confirmed in vibranceDLL
        private const uint ID_SetDVCLevel      = 0x172409B4; // confirmed in vibranceDLL

        // Candidate IDs for GetColorInfo / SetColorInfo — tried in order
        private static readonly uint[] CandidateGetColor = {
            0x61B9235C, 0x9A1B9365, 0xA48D1485, 0x09246688
        };
        private static readonly uint[] CandidateSetColor = {
            0x6BEF0C63, 0xE4FBFB9C, 0x4A82D9A8, 0x53B0A33D
        };

        private static bool _initDone  = false;
        private static bool _available = false;
        private static IntPtr _hDisp   = IntPtr.Zero;
        private static ColorInfo_t _getColor;
        private static ColorInfo_t _setColor;
        private static NV_COLOR_INFO_V1 _original;
        private static bool _hasOriginal;

        public static bool IsAvailable => _available;

        /// <summary>
        /// Probes nvapi.dll for gamma/color functions. Logs all results.
        /// Call once after vibranceDLL's initializeLibrary() so nvapi.dll is in-process.
        /// </summary>
        public static bool TryInit()
        {
            if (_initDone) return _available;
            _initDone = true;

            try
            {
                IntPtr hNvapi = GetModuleHandle("nvapi.dll");
                VibranceGUI.Log($"[NVAPI] GetModuleHandle(nvapi.dll) = 0x{hNvapi.ToInt64():X}");
                if (hNvapi == IntPtr.Zero) return false;

                IntPtr qiPtr = GetProcAddress(hNvapi, "nvapi_QueryInterface");
                VibranceGUI.Log($"[NVAPI] QueryInterface ptr = 0x{qiPtr.ToInt64():X}");
                if (qiPtr == IntPtr.Zero) return false;

                var qi = Marshal.GetDelegateForFunctionPointer<QueryInterface_t>(qiPtr);

                // --- Confirm known working IDs resolve (sanity check) ---
                IntPtr pGetDVC = qi(ID_GetDVCInfo);
                IntPtr pSetDVC = qi(ID_SetDVCLevel);
                VibranceGUI.Log($"[NVAPI] Known: GetDVCInfo=0x{pGetDVC.ToInt64():X}  SetDVCLevel=0x{pSetDVC.ToInt64():X}");

                // --- Get display handle ---
                IntPtr pEnum = qi(ID_EnumDisplay);
                VibranceGUI.Log($"[NVAPI] EnumNvidiaDisplayHandle ptr=0x{pEnum.ToInt64():X}");
                if (pEnum == IntPtr.Zero) return false;

                var enumFn = Marshal.GetDelegateForFunctionPointer<EnumDisplay_t>(pEnum);
                IntPtr h = IntPtr.Zero;
                int r = enumFn(0, ref h);
                VibranceGUI.Log($"[NVAPI] EnumDisplay(0) result={r}  hDisp=0x{h.ToInt64():X}");
                if (r != 0 || h == IntPtr.Zero) return false;
                _hDisp = h;

                // --- Probe candidate GetColorInfo IDs ---
                IntPtr pGet = IntPtr.Zero;
                uint foundGetId = 0;
                foreach (uint id in CandidateGetColor)
                {
                    IntPtr p = qi(id);
                    VibranceGUI.Log($"[NVAPI] Probe GetColor 0x{id:X8} => 0x{p.ToInt64():X}");
                    if (p != IntPtr.Zero && pGet == IntPtr.Zero) { pGet = p; foundGetId = id; }
                }

                // --- Probe candidate SetColorInfo IDs ---
                IntPtr pSet = IntPtr.Zero;
                uint foundSetId = 0;
                foreach (uint id in CandidateSetColor)
                {
                    IntPtr p = qi(id);
                    VibranceGUI.Log($"[NVAPI] Probe SetColor 0x{id:X8} => 0x{p.ToInt64():X}");
                    if (p != IntPtr.Zero && pSet == IntPtr.Zero) { pSet = p; foundSetId = id; }
                }

                if (pGet == IntPtr.Zero || pSet == IntPtr.Zero)
                {
                    VibranceGUI.Log($"[NVAPI] Color functions not found. GetColorId=0x{foundGetId:X8} SetColorId=0x{foundSetId:X8}");
                    return false;
                }

                _getColor = Marshal.GetDelegateForFunctionPointer<ColorInfo_t>(pGet);
                _setColor = Marshal.GetDelegateForFunctionPointer<ColorInfo_t>(pSet);
                VibranceGUI.Log($"[NVAPI] Using GetColor=0x{foundGetId:X8}  SetColor=0x{foundSetId:X8}");

                // Read original values
                var orig = MakeInfo();
                r = _getColor(_hDisp, ref orig);
                VibranceGUI.Log($"[NVAPI] GetColorInfo result={r}  brightness={orig.brightness} gamma={orig.gamma} sat={orig.saturation}");
                if (r == 0) { _original = orig; _hasOriginal = true; }

                _available = true;
                return true;
            }
            catch (Exception ex)
            {
                VibranceGUI.Log($"[NVAPI] TryInit exception: {ex.Message}");
                return false;
            }
        }

        public static bool SetGammaAndBrightness(float gammaScalar, int shadowBoost)
        {
            if (!_available) return false;
            try
            {
                var info = MakeInfo();
                _getColor(_hDisp, ref info);

                // gammaScalar 1.0 = neutral. Map to NVAPI (100 = 1.0)
                info.gamma = (uint)Math.Max(50, Math.Min(480, (int)Math.Round(gammaScalar * 100)));

                // brightness: 50 = neutral; shadowBoost 0-100 maps to +0..+50
                info.brightness = (uint)Math.Max(0, Math.Min(200, 50 + shadowBoost / 2));

                int r = _setColor(_hDisp, ref info);
                VibranceGUI.Log($"[NVAPI] SetColorInfo result={r} gamma={info.gamma} brightness={info.brightness}");
                return r == 0;
            }
            catch (Exception ex)
            {
                VibranceGUI.Log($"[NVAPI] SetGammaAndBrightness ex: {ex.Message}");
                return false;
            }
        }

        public static bool RestoreOriginal()
        {
            if (!_available || !_hasOriginal) return false;
            try
            {
                var info = _original;
                int r = _setColor(_hDisp, ref info);
                VibranceGUI.Log($"[NVAPI] RestoreOriginal result={r}");
                return r == 0;
            }
            catch { return false; }
        }

        private static NV_COLOR_INFO_V1 MakeInfo()
        {
            var i = new NV_COLOR_INFO_V1();
            i.version = (uint)Marshal.SizeOf(i) | (1u << 16);
            return i;
        }
    }
}
