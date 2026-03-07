using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace vibrance.GUI.common
{
    public class GammaRampController
    {
        // ── GDI32 (user-mode fallback) ───────────────────────────────────────
        [DllImport("gdi32.dll")]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll")]
        private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        // ── D3DKMT (kernel-mode path — bypasses foreground/driver restrictions) ──
        // This is the same path NVIDIA Control Panel uses internally.
        [DllImport("gdi32.dll", SetLastError = false)]
        private static extern uint D3DKMTOpenAdapterFromHdc(ref D3DKMT_OPENADAPTERFROMHDC pData);

        [DllImport("gdi32.dll", SetLastError = false)]
        private static extern uint D3DKMTCreateDevice(ref D3DKMT_CREATEDEVICE pData);

        [DllImport("gdi32.dll", SetLastError = false)]
        private static extern uint D3DKMTSetGammaRamp(ref D3DKMT_SETGAMMARAMP pData);

        [DllImport("gdi32.dll", SetLastError = false)]
        private static extern uint D3DKMTDestroyDevice(ref D3DKMT_DESTROYDEVICE pData);

        [DllImport("gdi32.dll", SetLastError = false)]
        private static extern uint D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER pData);

        // ── D3DKMT structures ────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_OPENADAPTERFROMHDC
        {
            public IntPtr hDc;
            public uint hAdapter;
            public D3DKMT_LUID AdapterLuid;
            public uint VidPnSourceId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_CREATEDEVICE
        {
            public uint hAdapter;
            public uint Flags;           // D3DKMT_CREATEDEVICEFLAGS bit field — 0 = defaults
            public uint hDevice;         // [out]
            public IntPtr pCommandBuffer;        // [out] unused
            public uint CommandBufferSize;       // [out] unused
            public IntPtr pAllocationList;       // [out] unused
            public uint AllocationListSize;      // [out] unused
            public IntPtr pPatchLocationList;    // [out] unused
            public uint PatchLocationListSize;   // [out] unused
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_SETGAMMARAMP
        {
            public uint hDevice;
            public uint FirstEntry;       // 0
            public uint NumberOfEntries;  // 256
            public uint Type;             // D3DDDI_GAMMARAMP_RGB256x3x16 = 2
            public IntPtr pGammaRamp;     // pointer to ushort[768] (R[256], G[256], B[256])
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_DESTROYDEVICE { public uint hDevice; }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3DKMT_CLOSEADAPTER { public uint hAdapter; }

        // ── Gamma ramp data ──────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        private RAMP _originalRamp;
        private bool _hasOriginalRamp = false;
        private RAMP _lastRamp;
        private bool _hasLastRamp = false;

        // ── Core ramp setter ─────────────────────────────────────────────────
        private bool SetRamp(ref RAMP ramp)
        {
            // Build a flat ushort[768] array (R[256],G[256],B[256]) and pin it
            // so D3DKMT can pointer to it without GC interference.
            ushort[] flat = new ushort[768];
            Array.Copy(ramp.Red,   0, flat,   0, 256);
            Array.Copy(ramp.Green, 0, flat, 256, 256);
            Array.Copy(ramp.Blue,  0, flat, 512, 256);

            GCHandle pin = GCHandle.Alloc(flat, GCHandleType.Pinned);
            try
            {
                bool kmtOk = SetRampViaD3DKMT(pin.AddrOfPinnedObject());
                if (kmtOk) return true;
            }
            finally
            {
                pin.Free();
            }

            // D3DKMT unavailable or failed — fall through to GDI paths.
            bool gdiOk = false;
            string deviceName = Screen.PrimaryScreen.DeviceName;

            IntPtr hMonDC = CreateDC(null, deviceName, null, IntPtr.Zero);
            if (hMonDC != IntPtr.Zero)
            {
                gdiOk |= SetDeviceGammaRamp(hMonDC, ref ramp);
                DeleteDC(hMonDC);
            }

            IntPtr hDesktopDC = GetDC(IntPtr.Zero);
            gdiOk |= SetDeviceGammaRamp(hDesktopDC, ref ramp);
            ReleaseDC(IntPtr.Zero, hDesktopDC);

            bool result = gdiOk;
            VibranceGUI.Log($"GammaRampController.SetRamp: D3DKMT=fail GDI={gdiOk}");
            return result;
        }

        /// <summary>
        /// Sets the gamma ramp via D3DKMT — kernel-mode path that NVIDIA Control Panel also uses.
        /// Not restricted to foreground applications, unlike the GDI SetDeviceGammaRamp path.
        /// Returns true if the call succeeded, false if D3DKMT is unavailable on this system.
        /// </summary>
        private bool SetRampViaD3DKMT(IntPtr pFlatRamp)
        {
            // Get desktop DC to find the adapter
            IntPtr hDC = GetDC(IntPtr.Zero);
            if (hDC == IntPtr.Zero) return false;

            try
            {
                var openData = new D3DKMT_OPENADAPTERFROMHDC { hDc = hDC };
                if (D3DKMTOpenAdapterFromHdc(ref openData) != 0) return false;

                uint hAdapter = openData.hAdapter;
                try
                {
                    var createData = new D3DKMT_CREATEDEVICE { hAdapter = hAdapter };
                    if (D3DKMTCreateDevice(ref createData) != 0) return false;

                    var destroyData = new D3DKMT_DESTROYDEVICE { hDevice = createData.hDevice };
                    try
                    {
                        var setData = new D3DKMT_SETGAMMARAMP
                        {
                            hDevice       = createData.hDevice,
                            FirstEntry    = 0,
                            NumberOfEntries = 256,
                            Type          = 2,      // D3DDDI_GAMMARAMP_RGB256x3x16
                            pGammaRamp    = pFlatRamp
                        };
                        return D3DKMTSetGammaRamp(ref setData) == 0;
                    }
                    finally
                    {
                        D3DKMTDestroyDevice(ref destroyData);
                    }
                }
                finally
                {
                    var closeData = new D3DKMT_CLOSEADAPTER { hAdapter = hAdapter };
                    D3DKMTCloseAdapter(ref closeData);
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hDC);
            }
        }

        // ── Public API ───────────────────────────────────────────────────────
        public bool SaveOriginalRamp()
        {
            _originalRamp = new RAMP
            {
                Red   = new ushort[256],
                Green = new ushort[256],
                Blue  = new ushort[256]
            };

            string deviceName = Screen.PrimaryScreen.DeviceName;
            IntPtr hMonDC = CreateDC(null, deviceName, null, IntPtr.Zero);
            bool result = false;
            if (hMonDC != IntPtr.Zero)
            {
                result = GetDeviceGammaRamp(hMonDC, ref _originalRamp);
                DeleteDC(hMonDC);
            }
            if (!result)
            {
                IntPtr hDesktopDC = GetDC(IntPtr.Zero);
                result = GetDeviceGammaRamp(hDesktopDC, ref _originalRamp);
                ReleaseDC(IntPtr.Zero, hDesktopDC);
            }

            // If reading failed, build a default linear ramp so restore still works.
            if (!result)
            {
                for (int i = 0; i < 256; i++)
                {
                    ushort v = (ushort)(i * 257); // maps 0-255 → 0-65535
                    _originalRamp.Red[i]   = v;
                    _originalRamp.Green[i] = v;
                    _originalRamp.Blue[i]  = v;
                }
                result = true; // we have a usable fallback ramp
                VibranceGUI.Log("GammaRampController: GetDeviceGammaRamp failed — using linear fallback.");
            }

            _hasOriginalRamp = result;
            return result;
        }

        public bool RestoreOriginalRamp()
        {
            // NVAPI path — try first since GDI is silently ignored on NVIDIA App systems
            if (vibrance.GUI.NVIDIA.NvidiaColorController.IsAvailable)
                return vibrance.GUI.NVIDIA.NvidiaColorController.RestoreOriginal();

            if (!_hasOriginalRamp) return false;
            _hasLastRamp = false;
            return SetRamp(ref _originalRamp);
        }

        /// <summary>Called by the 16ms refresh timer — re-applies the last ramp cheaply.</summary>
        public void Reapply()
        {
            // If NVAPI is active the driver keeps it; no re-apply needed.
            // If GDI path, re-apply every tick to fight driver resets.
            if (!vibrance.GUI.NVIDIA.NvidiaColorController.IsAvailable && _hasLastRamp)
                SetRamp(ref _lastRamp);
        }

        public bool ApplyShadowBoostAndGamma(int shadowBoostStrength, float gammaScalar = 1.0f)
        {
            if (shadowBoostStrength < 0)  shadowBoostStrength = 0;
            if (shadowBoostStrength > 100) shadowBoostStrength = 100;
            if (gammaScalar <= 0f) gammaScalar = 1.0f;

            RAMP ramp = new RAMP
            {
                Red   = new ushort[256],
                Green = new ushort[256],
                Blue  = new ushort[256]
            };

            double intensity = shadowBoostStrength / 100.0 * 2.5;

            for (int i = 0; i < 256; i++)
            {
                double n = i / 255.0;
                double boosted = Math.Sqrt(n);
                double taper   = 1.0 - (n * n);

                double final = n + (boosted - n) * intensity * taper;

                if (Math.Abs(gammaScalar - 1.0f) > 0.001f)
                    final = Math.Pow(final, 1.0 / gammaScalar);

                if (final > 1.0) final = 1.0;
                if (final < 0.0) final = 0.0;

                ushort v = (ushort)(final * 65535.0);
                ramp.Red[i]   = v;
                ramp.Green[i] = v;
                ramp.Blue[i]  = v;
            }

            _lastRamp    = ramp;
            _hasLastRamp = true;

            // NVAPI path: same driver-level access the NVIDIA App uses for gamma/brightness.
            // GDI SetDeviceGammaRamp is silently ignored on systems where NVIDIA App is active.
            if (vibrance.GUI.NVIDIA.NvidiaColorController.TryInit())
            {
                bool nvapiOk = vibrance.GUI.NVIDIA.NvidiaColorController.SetGammaAndBrightness(gammaScalar, shadowBoostStrength);
                if (nvapiOk)
                {
                    VibranceGUI.Log($"NvidiaColorController.SetGammaAndBrightness OK: shadow={shadowBoostStrength} gamma={gammaScalar}");
                    return true;
                }
                VibranceGUI.Log("NvidiaColorController.SetGammaAndBrightness FAILED — falling back to GDI/D3DKMT");
            }

            // Fallback: GDI / D3DKMT
            bool ok = SetRamp(ref ramp);
            if (!ok)
                VibranceGUI.Log($"GammaRampController: all paths failed. shadow={shadowBoostStrength} gamma={gammaScalar}");
            return ok;
        }
    }
}
