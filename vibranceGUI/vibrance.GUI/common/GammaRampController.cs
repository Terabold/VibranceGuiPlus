using System;
using System.Runtime.InteropServices;
using System.Drawing;

namespace vibrance.GUI.common
{
    public class GammaRampController
    {
        [DllImport("gdi32.dll")]
        public static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll")]
        public static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

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

        public bool SaveOriginalRamp()
        {
            IntPtr hDC = GetDC(IntPtr.Zero);
            _originalRamp = new RAMP();
            _originalRamp.Red = new ushort[256];
            _originalRamp.Green = new ushort[256];
            _originalRamp.Blue = new ushort[256];

            bool result = GetDeviceGammaRamp(hDC, ref _originalRamp);
            ReleaseDC(IntPtr.Zero, hDC);
            
            _hasOriginalRamp = result;
            return result;
        }

        public bool RestoreOriginalRamp()
        {
            if (!_hasOriginalRamp) return false;
            
            IntPtr hDC = GetDC(IntPtr.Zero);
            bool result = SetDeviceGammaRamp(hDC, ref _originalRamp);
            ReleaseDC(IntPtr.Zero, hDC);
            return result;
        }

        public bool ApplyShadowBoostAndGamma(int shadowBoostStrength, float gammaScalar = 1.0f)
        {
            // strength should be between 0 (no boost) and 100 (max boost)
            if (shadowBoostStrength < 0) shadowBoostStrength = 0;
            if (shadowBoostStrength > 100) shadowBoostStrength = 100;

            // gammaScalar should normally be 1.0 for default.
            if (gammaScalar <= 0f) gammaScalar = 1.0f;

            IntPtr hDC = GetDC(IntPtr.Zero);
            RAMP ramp = new RAMP();
            ramp.Red = new ushort[256];
            ramp.Green = new ushort[256];
            ramp.Blue = new ushort[256];

            // Normalize strength to a factor between 0.0 and 1.5 roughly
            // Increase this to have a stronger effect on shadows
            double intensity = shadowBoostStrength / 100.0 * 2.5; 

            for (int i = 0; i < 256; i++)
            {
                // Non-linear gamma curve to boost shadows while preserving highlights
                // We use a curve that rises quickly initially, then tapers off.
                // Output = i + intensity * (i * (255 - i) / 128) -> simplified parabola
                // A better approach is blending linear (x) with sqrt(x)
                
                double normalizedI = i / 255.0; // 0.0 to 1.0
                
                // Base linear curve
                double linear = normalizedI;
                
                // Boost curve (sqrt creates a quick rise from 0, lifting shadows)
                double boosted = Math.Sqrt(normalizedI);
                
                // Blend based on strength. 
                // We taper off the boost as we get closer to 1.0 (highlights)
                double taper = 1.0 - (normalizedI * normalizedI); // Stays near 1.0 longer, then drops
                
                // Calculate final normalized value and scale back to 0-65535
                double finalNormalized = linear + (boosted - linear) * intensity * taper;

                // Apply flat gamma scalar mathematically (curve exponentiation: Output = Input ^ (1 / Gamma))
                if (Math.Abs(gammaScalar - 1.0f) > 0.001f)
                {
                    finalNormalized = Math.Pow(finalNormalized, 1.0 / gammaScalar);
                }
                
                // Clamp to max
                if (finalNormalized > 1.0) finalNormalized = 1.0;
                
                ushort value = (ushort)(finalNormalized * 65535.0);

                ramp.Red[i] = value;
                ramp.Green[i] = value;
                ramp.Blue[i] = value;
            }

            bool result = SetDeviceGammaRamp(hDC, ref ramp);
            ReleaseDC(IntPtr.Zero, hDC);
            return result;
        }
    }
}
