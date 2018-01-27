﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Sorted
{
    struct BasicTimer : IDisposable
    {
        Stopwatch _timer;
        string _message { get; }
        public BasicTimer(string message)
        {
            _message = message;
            Console.Write($"> {message}...");
            _timer = Stopwatch.StartNew();
        }
        void IDisposable.Dispose()
        {
            _timer.Stop();
            Console.WriteLine($" {_timer.ElapsedMilliseconds}ms");
        }
    }
    static class Program
    {
        static void Main()
        {
            //var z = int.MinValue;
            //Console.WriteLine((uint)z);
            //var c = RadixConverter.Get<int, uint>();
            //var arr = new int[] { int.MinValue, -1, 0, 1, int.MaxValue};
            //var arr2 = new uint[arr.Length];
            //var s = new Span<int>(arr).NonPortableCast<int, uint>();
            //Console.WriteLine(string.Join(", ", arr));
            //c.ToRadix(s, arr2);
            //Console.WriteLine(string.Join(", ", arr2));
            //c.FromRadix(arr2, s);
            //Console.WriteLine(string.Join(", ", arr));

            var rand = new Random(12345);
            const int LOOP = 2;
            float[] origFloat = new float[8 * 1024 * 1024], valsFloat = new float[origFloat.Length];
            uint[] origUInt32 = new uint[origFloat.Length], valsUInt32 = new uint[origFloat.Length];
            int[] origInt32 = new int[origFloat.Length], valsInt32 = new int[origFloat.Length];

            for (int i = 0; i < origFloat.Length; i++)
            {
                origFloat[i] = (float)(rand.NextDouble() * 50000);
                int ival = rand.Next(int.MinValue, int.MaxValue);
                origInt32[i] = ival;
                origUInt32[i] = unchecked((uint)ival);
            }
            var wFloat = new float[RadixSort.WorkspaceSize<float>(origFloat.Length)];
            Console.WriteLine($"Workspace length: {wFloat.Length}");

            Console.WriteLine();
            Console.WriteLine(">> float <<");
            Console.WriteLine();
            for (int i = 0; i < LOOP; i++)
            {
                origFloat.CopyTo(valsFloat, 0);
                using (new BasicTimer("RadixSort.Sort"))
                {
                    RadixSort.Sort<float>(valsFloat, wFloat);
                }
                CheckSort<float>(valsFloat);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origFloat.CopyTo(valsFloat, 0);
                using (new BasicTimer("RadixSort.Sort/descending"))
                {
                    RadixSort.Sort<float>(valsFloat, wFloat, descending: true);
                }
                CheckSortDescending<float>(valsFloat);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origFloat.CopyTo(valsFloat, 0);
                using (new BasicTimer("RadixSortUnsafe.Sort"))
                {
                    RadixSortUnsafe.Sort<float>(valsFloat, wFloat);
                }
                CheckSort<float>(valsFloat);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origFloat.CopyTo(valsFloat, 0);
                using (new BasicTimer("RadixSortUnsafe.Sort/descending"))
                {
                    RadixSortUnsafe.Sort<float>(valsFloat, wFloat, descending: true);
                }
                CheckSortDescending<float>(valsFloat);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origFloat.CopyTo(valsFloat, 0);
                using (new BasicTimer("Array.Sort"))
                {
                    Array.Sort<float>(valsFloat);
                }
                CheckSort<float>(valsFloat);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origFloat.CopyTo(valsFloat, 0);
                using (new BasicTimer("Array.Sort+Array.Reverse"))
                {
                    Array.Sort<float>(valsFloat);
                    Array.Reverse<float>(valsFloat);
                }
                CheckSortDescending<float>(valsFloat);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origFloat.CopyTo(valsFloat, 0);
                using (new BasicTimer("Array.Sort/neg CompareTo"))
                {
                    Array.Sort<float>(valsFloat, (x,y)=>y.CompareTo(x));
                }
                CheckSortDescending<float>(valsFloat);
            }



            Console.WriteLine();
            Console.WriteLine($">> int << (vec: {RadixConverter.AllowVectorization})");
            Console.WriteLine();
            var wInt = new Span<float>(wFloat).NonPortableCast<float, int>();
            var oldVec = RadixConverter.AllowVectorization;
            RadixConverter.AllowVectorization = false;
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSort.Sort + vec"))
                {
                    RadixSort.Sort<int>(valsInt32, wInt);
                }
                CheckSort<int>(valsInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSort.Sort/descending + vec"))
                {
                    RadixSort.Sort<int>(valsInt32, wInt, descending: true);
                }
                CheckSortDescending<int>(valsInt32);
            }
            RadixConverter.AllowVectorization = false;
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSort.Sort - vec"))
                {
                    RadixSort.Sort<int>(valsInt32, wInt);
                }
                CheckSort<int>(valsInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSort.Sort/descending - vec"))
                {
                    RadixSort.Sort<int>(valsInt32, wInt, descending: true);
                }
                CheckSortDescending<int>(valsInt32);
            }
            RadixConverter.AllowVectorization = oldVec;
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSortUnsafe.Sort"))
                {
                    RadixSortUnsafe.Sort<int>(valsInt32, wInt);
                }
                CheckSort<int>(valsInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSortUnsafe.Sort/descending"))
                {
                    RadixSortUnsafe.Sort<int>(valsInt32, wInt, descending: true);
                }
                CheckSortDescending<int>(valsInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("Array.Sort"))
                {
                    Array.Sort<int>(valsInt32);
                }
                CheckSort<int>(valsInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("Array.Sort+Array.Reverse"))
                {
                    Array.Sort<int>(valsInt32);
                    Array.Reverse<int>(valsInt32);
                }
                CheckSortDescending<int>(valsInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("Array.Sort/neg CompareTo"))
                {
                    Array.Sort<int>(valsInt32, (x, y) => y.CompareTo(x));
                }
                CheckSortDescending<int>(valsInt32);
            }

            Console.WriteLine();
            Console.WriteLine(">> uint <<");
            Console.WriteLine();
            var wUint = new Span<float>(wFloat).NonPortableCast<float, uint>();
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSort.Sort"))
                {
                    RadixSort.Sort<uint>(valsUInt32, wUint);
                }
                CheckSort<uint>(valsUInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSort.Sort/descending"))
                {
                    RadixSort.Sort<uint>(valsUInt32, wUint, descending: true);
                }
                CheckSortDescending<uint>(valsUInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSortUnsafe.Sort"))
                {
                    RadixSortUnsafe.Sort<uint>(valsUInt32, wUint);
                }
                CheckSort<uint>(valsUInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("RadixSortUnsafe.Sort/descending"))
                {
                    RadixSortUnsafe.Sort<uint>(valsUInt32, wUint, descending: true);
                }
                CheckSortDescending<uint>(valsUInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("Array.Sort"))
                {
                    Array.Sort<uint>(valsUInt32);
                }
                CheckSort<uint>(valsUInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("Array.Sort+Array.Reverse"))
                {
                    Array.Sort<uint>(valsUInt32);
                    Array.Reverse<uint>(valsUInt32);
                }
                CheckSortDescending<uint>(valsUInt32);
            }
            for (int i = 0; i < LOOP; i++)
            {
                origInt32.CopyTo(valsInt32, 0);
                using (new BasicTimer("Array.Sort/neg CompareTo"))
                {
                    Array.Sort<uint>(valsUInt32, (x, y) => y.CompareTo(x));
                }
                CheckSortDescending<uint>(valsUInt32);
            }
        }

        private static void CheckSort<T>(Span<T> vals) where T : struct, IComparable<T>
        {
            if (vals.Length <= 1) return;
            var prev = vals[0];
            for(int i = 1; i < vals.Length; i++)
            {
                var val = vals[i];
                if (val.CompareTo(prev) < 0) throw new InvalidOperationException($"not sorted: [{i - 1}] ({prev}) vs [{i}] ({val})");
                prev = val;
            }
        }
        private static void CheckSortDescending<T>(Span<T> vals) where T : struct, IComparable<T>
        {
            if (vals.Length <= 1) return;
            var prev = vals[0];
            for (int i = 1; i < vals.Length; i++)
            {
                var val = vals[i];
                if (val.CompareTo(prev) > 0) throw new InvalidOperationException($"not sorted: [{i - 1}] ({prev}) vs [{i}] ({val})");
                prev = val;
            }
        }
    }
}
