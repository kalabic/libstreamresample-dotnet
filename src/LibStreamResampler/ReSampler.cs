/******************************************************************************
 *
 * libresamplesharp
 * Copyright (c) 2018 Xianyi Cui
 *
 * libresample4j
 * Copyright (c) 2009 Laszlo Systems, Inc
 *
 * libresample 0.1.3
 * Copyright (c) 2003 Dominic Mazzoni
 *
 * Resample 1.7
 * Copyright (c) 1994-2002 Julius O. Smith III
 * 
 * 'libresamplesharp' is a C# port of David Nault's 'libresample4j',
 * which is a Java port of Dominic Mazzoni's 'libresample 0.1.3',
 * which is in turn based on Julius Smith's 'Resample 1.7' library.
 *
 * This product includes software derived from the work of
 * Julius Smith, Dominic Mazzoni, David Nault, and others.
 * 
 * - https://ccrma.stanford.edu/~jos/resample/
 * - https://ccrma.stanford.edu/~jos/resample/Free_Resampling_Software.html
 * - https://github.com/minorninth/libresample
 * - https://github.com/dnault/libresample4j
 * - https://github.com/xycui/libresamplesharp
 *
 *
 * This file is part of AudioFormatLib, which is free software:
 * you can redistribute it and/or modify it under the terms of the
 * GNU Lesser General Public License (LGPL), version 2.1 only,
 * as published by the Free Software Foundation.
 *
 * AudioFormatLib is intended to be used as a dynamically linked library.
 * Applications using this library are not subject to the LGPL license,
 * provided they comply with its terms (e.g., allowing replacement of the library).
 *
 * AudioFormatLib is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * version 2.1 along with this file. If not, see:
 * https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html 
 *
 *****************************************************************************/

using System;
using System.Linq;

namespace LibStreamResampler
{
    internal class ReSampler
    {
        /// <summary>
        /// Callback for producing samples. Enalbes on-the-fly conversion between sample types
        /// (signed 16-bit integers to floats, for example).
        /// </summary>
        internal interface IInputProducer
        {
            /// <summary>
            /// Get the number of input samples available
            /// </summary>
            /// <returns>number of input samples available</returns>
            int GetInputBufferLenght();

            /// <summary>
            /// Copy lenght samples from the input buffer to the given array, starting at the 
            /// given offset. Samples should be in the range -1.0f to 1.0f
            /// </summary>
            /// <param name="array">array to hold samples from the input buffer</param>
            /// <param name="offset">start writing samples here</param>
            /// <param name="length">write this many samples</param>
            void ProduceInput(float[] array, int offset, int length);
        }

        /// <summary>
        /// Callback for consuming samples. Enalbes on-the-fly conversion between sample types
        /// (signed 16-bit integers to floats, for example) and/or writing directly to an output stream.
        /// </summary>
        internal interface IOutputConsumer
        {
            /// <summary>
            /// Get the number of samples the output buffer has room for
            /// </summary>
            /// <returns>number of samples the output buffer has room for</returns>
            int GetOutputBufferLength();

            /// <summary>
            /// Copy length samples from the given array to the output buffer, starting at the given offset.
            /// </summary>
            /// <param name="array">array to read from</param>
            /// <param name="offset">start reading samples here</param>
            /// <param name="length">read this many samples</param>
            void ConsumeOutput(float[] array, int offset, int length);
        }


        /// <summary>
        ///     Wrapper for cached arrays of coefficients.
        /// </summary>
        internal class Coefficients
        {
            private static readonly Object s_lock = new();

            private static Coefficients? s_nmult_HQ = null;

            private static Coefficients? s_nmult_LQ = null;

            internal static int calc_nwing(int nmult)
            {
                return Npc * (nmult - 1) / 2;
            }

            private static Coefficients GetCoefficients(int nmult)
            {
                if (nmult == NMULT_HQ)
                {
                    if (s_nmult_HQ is null)
                    {
                        int nwing = Coefficients.calc_nwing(nmult);
                        s_nmult_HQ = new Coefficients(nwing, 0.5 * ROLL_OFF, BETA, Npc);
                    }
                    return s_nmult_HQ;
                }
                else if (nmult == NMULT_LQ)
                {
                    if (s_nmult_LQ is null)
                    {
                        int nwing = Coefficients.calc_nwing(nmult);
                        s_nmult_LQ = new Coefficients(nwing, 0.5 * ROLL_OFF, BETA, Npc);
                    }
                    return s_nmult_LQ;
                }
                else
                {
                    throw new NotImplementedException($"Don't have coefficients for nmult={nmult}");
                }
            }

            internal static float[] GetCoArray_imp64(int nmult)
            {
                lock (s_lock)
                {
                    return GetCoefficients(nmult).imp64;
                }
            }

            internal static float[] GetCoArray_impD64(int nmult)
            {
                lock (s_lock)
                {
                    return GetCoefficients(nmult).impD64;
                }
            }

            public readonly float[] imp64;

            public readonly float[] impD64;

            internal Coefficients(int nwing, double frq, double beta, int num)
            {
                double[] dbl_imp64 = new double[nwing];
                FilterKit.LrsLpFilter(dbl_imp64, nwing, frq, beta, num);

                imp64 = new float[nwing];
                for (var i = 0; i < nwing; i++)
                {
                    imp64[i] = (float)dbl_imp64[i];
                }

                impD64 = new float[nwing];
                for (var i = 0; i < nwing - 1; i++)
                {
                    impD64[i] = imp64[i + 1] - imp64[i];
                }
                impD64[nwing - 1] = -imp64[nwing - 1];
            }
        }


        /// <summary>
        ///     Number of values per 1/delta in impulse response
        /// </summary>
        internal const int Npc = 4096;

        /// <summary>
        ///     Roll-off frequency of filter
        /// </summary>
        internal const double ROLL_OFF = 0.90;

        /// <summary>
        ///     Kaiser window parameter beta.
        /// </summary>
        internal const double BETA = 6;

        /// <summary>
        ///     Multiplier that determines the length of the HIGH quality filter.
        /// </summary>
        internal const int NMULT_HQ = 35;

        /// <summary>
        ///     Multiplier that determines the length of the LOW quality filter.
        /// </summary>
        internal const int NMULT_LQ = 11;


        private readonly float[] _imp;
        private readonly float[] _impD;
        private readonly float _lpScl;
        private readonly double _maxFactor;
        private readonly double _minFactor;
        private readonly int _nmult;
        private readonly int _nwing;
        private readonly float[] _x;
        private readonly int _xoff;
        private readonly int _xSize;
        private readonly float[] _y;
        private double _time;
        private int _xp; // current "now"-sample pointer for input
        private int _xread; // position to put new samples
        private int _yp;

        /// <summary>
        ///     Clone an existing resampling session. Faster than creating one from scratch.
        /// </summary>
        /// <param name="other">another instance of resampler</param>
        internal ReSampler(ReSampler other)
        {
            _imp = other._imp.ToArray();
            _impD = other._impD.ToArray();
            _lpScl = other._lpScl;
            _nmult = other._nmult;
            _nwing = other._nwing;
            _minFactor = other._minFactor;
            _maxFactor = other._maxFactor;
            _xSize = other._xSize;
            _x = other._x.ToArray();
            _xp = other._xp;
            _xread = other._xread;
            _xoff = other._xoff;
            _y = other._y;
            _yp = other._yp;
            _time = other._time;
        }

        internal ReSampler(bool highQuality, double minFactor, double maxFactor)
        {
            if (minFactor <= 0.0 || maxFactor <= 0.0)
            {
                throw new ArgumentException("minFactor and maxFactor must be positive");
            }
            if (maxFactor < minFactor)
            {
                throw new ArgumentException("minFactor must be less or equal to maxFactor");
            }

            _minFactor = minFactor;
            _maxFactor = maxFactor;
            _nmult = highQuality ? NMULT_HQ : NMULT_LQ;
            _lpScl = 1.0f;
            _nwing = Coefficients.calc_nwing(_nmult); // # of filter coeffs in right wing

            // Use cached arrays of coefficients, allocated and calculated only on first use.
            _imp = Coefficients.GetCoArray_imp64(_nmult);
            _impD = Coefficients.GetCoArray_impD64(_nmult);

            var xoffMin = (int)(((_nmult + 1) / 2.0) * Math.Max(1.0, 1.0 / minFactor) + 10);
            var xoffMax = (int)(((_nmult + 1) / 2.0) * Math.Max(1.0, 1.0 / maxFactor) + 10);
            _xoff = Math.Max(xoffMin, xoffMax);

            _xSize = Math.Max(2 * _xoff + 10, 4096);
            _x = new float[_xSize + _xoff];
            _xp = _xoff;
            _xread = _xoff;

            var ySize = (int)(_xSize * maxFactor + 2.0);
            _y = new float[ySize];
            _yp = 0;

            _time = _xoff;
        }

        public int GetfilterWidth()
        {
            return _xoff;
        }

        internal bool Process(double factor, IInputProducer input, IOutputConsumer output, bool lastBatch)
        {
            if (factor < _minFactor || factor > _maxFactor)
            {
                throw new ArgumentException("factor" + factor + "is not between minFactor=" + _minFactor + " and maxFactor=" + _maxFactor);
            }

            int outBufferLen = output.GetOutputBufferLength();
            int inBufferLen = input.GetInputBufferLenght();

            float[] imp = _imp;
            float[] impD = _impD;
            float lpScl = _lpScl;
            int nwing = _nwing;
            bool interpFilt = false;

            int inBufferUsed = 0;
            int outSampleCount = 0;

            if ((_yp != 0) && (outBufferLen - outSampleCount) > 0)
            {
                int len = Math.Min(outBufferLen - outSampleCount, _yp);

                output.ConsumeOutput(_y, 0, len);

                outSampleCount += len;
                for (int i = 0; i < _yp - len; i++)
                {
                    _y[i] = _y[i + len];
                }
                _yp -= len;
            }

            if (_yp != 0)
            {
                return inBufferUsed == 0 && outSampleCount == 0;
            }

            if (factor < 1)
            {
                lpScl = (float)(lpScl * factor);
            }

            while (true)
            {
                int len = _xSize - _xread;

                if (len >= inBufferLen - inBufferUsed)
                {
                    len = inBufferLen - inBufferUsed;
                }

                input.ProduceInput(_x, _xread, len);

                inBufferUsed += len;
                _xread += len;

                int nx;
                if (lastBatch && (inBufferUsed == inBufferLen))
                {
                    nx = _xread - _xoff;
                    for (int i = 0; i < _xoff; i++)
                    {
                        _x[_xread + i] = 0;
                    }
                }
                else
                {
                    nx = _xread - 2 * _xoff;
                }

                if (nx <= 0)
                {
                    break;
                }

                int nout;
                if (factor >= 1)
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    nout = LrsSrcUp(_x, _y, factor, nx, nwing, lpScl, imp, impD, interpFilt);
                }
                else
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    nout = LrsSrcUd(_x, _y, factor, nx, nwing, lpScl, imp, impD, interpFilt);
                }

                _time -= nx;
                _xp += nx;

                int ncreep = (int)(_time) - _xoff;
                if (ncreep != 0)
                {
                    _time -= ncreep;
                    _xp += ncreep;
                }

                int nreuse = _xread - (_xp - _xoff);

                for (int i = 0; i < nreuse; i++)
                {
                    _x[i] = _x[i + (_xp - _xoff)];
                }

                _xread = nreuse;
                _xp = _xoff;

                _yp = nout;

                if (_yp != 0 && (outBufferLen - outSampleCount) > 0)
                {
                    len = Math.Min(outBufferLen - outSampleCount, _yp);

                    output.ConsumeOutput(_y, 0, len);
                    outSampleCount += len;
                    for (int i = 0; i < _yp - len; i++)
                    {
                        _y[i] = _y[i + len];
                    }
                    _yp -= len;
                }

                if (_yp != 0)
                {
                    break;
                }
            }

            return inBufferUsed == 0 && outSampleCount == 0;
        }

        private int LrsSrcUp(float[] x, float[] y, double factor, int nx, int nwing, float lpScl, float[] imp, float[] impD, bool interp)
        {
            float[] xpArray = x;
            int xpIndex;

            float[] ypArray = y;
            int ypIndex = 0;

            float v;

            double currentTime = _time;
            double dt;
            double endTime;

            dt = 1.0 / factor;

            endTime = currentTime + nx;
            while (currentTime < endTime)
            {
                double leftPhase = currentTime - Math.Floor(currentTime);
                double rightPhase = 1.0 - leftPhase;

                xpIndex = (int)currentTime;
                v = FilterKit.LrsFilterUp(imp, impD, nwing, interp, xpArray, xpIndex++, leftPhase, -1);
                v += FilterKit.LrsFilterUp(imp, impD, nwing, interp, xpArray, xpIndex, rightPhase, 1);
                v *= lpScl;

                ypArray[ypIndex++] = v;
                currentTime += dt;
            }

            _time = currentTime;

            return ypIndex;
        }

        private int LrsSrcUd(float[] x, float[] y, double factor, int nx, int nwing, float lpScl, float[] imp, float[] impD, bool interp)
        {
            float[] xpArray = x;
            int xpIndex;

            float[] ypArray = y;
            int ypIndex = 0;

            float v;

            double currentTime = _time;
            double dh;
            double dt;
            double endTime;

            dt = 1.0 / factor;

            dh = Math.Min(Npc, factor * Npc);

            endTime = currentTime + nx;
            while (currentTime < endTime)
            {
                double leftPhase = currentTime - Math.Floor(currentTime);
                double rightPhase = 1.0 - leftPhase;

                xpIndex = (int)currentTime;
                v = FilterKit.LrsFilterUd(imp, impD, nwing, interp, xpArray, xpIndex++, leftPhase, -1, dh);
                v += FilterKit.LrsFilterUd(imp, impD, nwing, interp, xpArray, xpIndex, rightPhase, 1, dh);
                v *= lpScl;

                ypArray[ypIndex++] = v;
                currentTime += dt;
            }

            _time = currentTime;
            return ypIndex;
        }
    }
}
