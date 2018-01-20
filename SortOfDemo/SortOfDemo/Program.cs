﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SortOfDemo
{
    class Program
    {
        struct BasicTimer : IDisposable
        {
            Stopwatch _timer;
            string _message { get; }
            public BasicTimer(string message)
            {
                _message = message;
                Console.WriteLine($"> {message}...");
                _timer = Stopwatch.StartNew();
            }
            void IDisposable.Dispose()
            {
                _timer.Stop();
                Console.WriteLine($"< {_message}, {_timer.ElapsedMilliseconds}ms");
            }
        }
        static void Main()
        {
            try
            {
                Execute();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }
        static void Execute()
        {
            SomeType[] data;
            DateTime[] releaseDates;
            ulong[] sortKeys;
            int[] index;
            using (new BasicTimer("allocating"))
            {
                data = new SomeType[16 * 1024 * 1024];
                releaseDates = new DateTime[data.Length];
                sortKeys = new ulong[data.Length];
                index = new int[data.Length];
            }

            Populate(data);
            try
            {
                CheckData(data);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("data is unsorted, as intended");
            }


            LINQ(data);
            ArraySortComparable(data);
            ArraySortComparer(data);
            ArraySortComparison(data);
            DualArrayDates(data, releaseDates);
            DualArrayComposite(data, sortKeys);
            DualArrayIndexed(data, index, sortKeys);
            DualArrayIndexedOptimized(data, index, sortKeys);
        }



        static void CheckData(SomeType[] data)
        {
            using (new BasicTimer("checking order"))
            {
                for (int i = 1; i < data.Length; i++)
                {
                    AssertOrdered(in data[i - 1], in data[i]);
                }
            }
        }

        static void CheckData(SomeType[] data, int[] index)
        {
            using (new BasicTimer("checking order"))
            {
                for (int i = 1; i < data.Length; i++)
                {
                    AssertOrdered(in data[index[i - 1]], in data[index[i]]);
                }
            }
        }
        static void AssertOrdered(in SomeType x, in SomeType y)
        {
            if (x.ReleaseDate > y.ReleaseDate) return;
            if (x.ReleaseDate == y.ReleaseDate && x.Price <= y.Price) return;

            throw new InvalidOperationException($"incorrect sort; {x.ReleaseDate}/{x.Price} [{Sortable(x)}] vs {y.ReleaseDate}/{y.Price} [{Sortable(y)}]");
        }
        static string Me([CallerMemberName] string caller = null) => caller;

        static void LINQ(SomeType[] data)
        {
            SomeType[] sorted;
            using (new BasicTimer(Me()))
            {
                sorted = (from item in data
                          orderby item.ReleaseDate descending,
                                  item.Price
                          select item).ToArray();
            }
            CheckData(sorted);
        }

        static void ArraySortComparable(SomeType[] data)
        {
            using (new BasicTimer(Me()))
            {
                Array.Sort<SomeType>(data);
            }
            CheckData(data);
            Populate(data); // need to re-invent
        }

        static void ArraySortComparer(SomeType[] data)
        {
            using (new BasicTimer(Me()))
            {
                Array.Sort<SomeType>(data, SomeTypeComparer.Default);
            }
            CheckData(data);
            Populate(data); // need to re-invent
        }

        static void ArraySortComparison(SomeType[] data)
        {
            using (new BasicTimer(Me()))
            {
                Array.Sort<SomeType>(data, (x, y) =>
                {
                    var delta = y.ReleaseDate
                            .CompareTo(x.ReleaseDate);
                    if (delta == 0) // second property
                        delta = x.Price.CompareTo(y.Price);
                    return delta;
                });
            }
            CheckData(data);
            Populate(data); // need to re-invent
        }

        private static void DualArrayDates(SomeType[] data, DateTime[] releaseDates)
        {
            using (new BasicTimer(Me() + " prepare"))
            {
                for (int i = 0; i < data.Length; i++)
                    releaseDates[i] = data[i].ReleaseDate;
            }
            using (new BasicTimer(Me() + " sort"))
            {
                Array.Sort(releaseDates, data);
            }
            // no point checking the data; we know it is wrong
            Populate(data); // need to re-invent
        }

        static ulong Sortable(in SomeType item)
        {
            return (~(ulong)item.ReleaseDate.ToMillenialTime()) << 32
                        | Sortable((float)item.Price);
        }
        private static void DualArrayComposite(SomeType[] data, ulong[] sortKeys)
        {
            using (new BasicTimer(Me() + " prepare"))
            {
                for (int i = 0; i < data.Length; i++)
                    sortKeys[i] = Sortable(in data[i]);
            }
            using (new BasicTimer(Me() + " sort"))
            {
                Array.Sort(sortKeys, data);
            }
            CheckData(data);
            Populate(data); // need to re-invent
        }

        private static void DualArrayIndexed(SomeType[] data, int[] index, ulong[] sortKeys)
        {
            using (new BasicTimer(Me() + " prepare"))
            {
                for (int i = 0; i < data.Length; i++)
                {
                    index[i] = i;
                    sortKeys[i] = Sortable(in data[i]);
                }
            }
            using (new BasicTimer(Me() + " sort"))
            {
                Array.Sort(sortKeys, index);
            }
            CheckData(data, index);
            // no need to re-invent
        }

        private static void DualArrayIndexedOptimized(SomeType[] data, int[] index, ulong[] sortKeys)
        {
            using (new BasicTimer(Me() + " prepare"))
            {
                for (int i = 0; i < data.Length; i++)
                {
                    index[i] = i;
                    sortKeys[i] = Sortable(in data[i]);
                }
            }
            using (new BasicTimer(Me() + " sort"))
            {
                Helpers.Sort(sortKeys, index);
            }
            CheckData(data, index);
            // no need to re-invent
        }

        static readonly DateTime Epoch = new DateTime(
            2005, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        private static void Populate(SomeType[] data)
        {
            using (new BasicTimer("inventing data"))
            {
                var rand = new Random(data.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    int id = rand.Next();
                    var releaseDate = Epoch
                        .AddYears(rand.Next(50))
                        .AddDays(rand.Next(365))
                        .AddSeconds(rand.Next(24 * 60 * 60));
                    var price = rand.NextDouble() * 50000;
                    data[i] = new SomeType(
                        id, releaseDate, price);
                }
            }
        }

        protected static ulong Sortable(int value)
        {
            // re-base eveything upwards, so anything
            // that was the min-value is now 0, etc
            var val = unchecked((uint)(value - int.MinValue));
            return val;
        }

        protected static unsafe ulong Sortable(float value)
        {
            const int MSB = 1 << 31;
            int raw = *(int*)(&value);
            if ((raw & MSB) != 0) // IEEE first bit is the sign bit
            {
                // is negative; shoult interpret as -(the value without the MSB) - not the same as just
                // dropping the bit, since integer math is twos-complement
                raw = -(raw & ~MSB);
            }
            return Sortable(raw);
        }
    }



    readonly partial struct SomeType
    {
        public int Id { get; }
        public DateTime ReleaseDate { get; }
        public double Price { get; }

        public SomeType(int id, DateTime releaseDate, double price)
        {
            Id = id;
            ReleaseDate = releaseDate;
            Price = price;
            _some = _other = _stuff = _not = _shown = 0;
        }

#pragma warning disable CS0414 // suppress "assigned, never used"
        private readonly long _some, _other, _stuff, _not, _shown;
#pragma warning restore CS0414
    }

    partial struct SomeType : IComparable<SomeType>
    {
        int IComparable<SomeType>.CompareTo(SomeType other)
        {
            var delta = other.ReleaseDate
                .CompareTo(this.ReleaseDate);
            if (delta == 0) // second property
                delta = this.Price.CompareTo(other.Price);
            return delta;
        }
    }

    sealed class SomeTypeComparer : IComparer<SomeType>
    {
        private SomeTypeComparer() { }
        public static SomeTypeComparer Default { get; } = new SomeTypeComparer();
        int IComparer<SomeType>.Compare(SomeType x, SomeType y)
        {
            var delta = y.ReleaseDate
                    .CompareTo(x.ReleaseDate);
            if (delta == 0) // second property
                delta = x.Price.CompareTo(y.Price);
            return delta;
        }
    }

    static unsafe class Helpers
    {

        private static DateTime
                Millenium = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                MillenialMinValue = Millenium.AddSeconds(2),
                MillenialMaxValue = Millenium.AddSeconds(uint.MaxValue - 1);

        public static uint ToMillenialTime(this DateTime value)
        {
            if (value == default(DateTime)) return 0;

            if (value < MillenialMinValue || value > MillenialMaxValue) throw new ArgumentOutOfRangeException(
                nameof(value), $"{value} outside the range {MillenialMinValue} to {MillenialMaxValue}");

            return (uint)(value - Millenium).TotalSeconds;
        }
        // borrowed from core-clr, with massive special-casing
        // https://github.com/dotnet/coreclr/blob/775003a4c72f0acc37eab84628fcef541533ba4e/src/mscorlib/src/System/Array.cs
        internal static void Sort(ulong* keys, int* values, int count)
        {
            if (count < 2) return;
            // note: remove SZSort call - doesn't apply: https://github.com/dotnet/coreclr/blob/775003a4c72f0acc37eab84628fcef541533ba4e/src/classlibnative/bcltype/arrayhelpers.cpp#L290
            IntrospectiveSort(keys, values, 0, count);
        }

        public static void Sort(ulong[] keys, int[] values)
        {
            fixed (ulong* k = keys)
            fixed (int * v = values)
            {
                Sort(k, v, Math.Min(keys.Length, values.Length));
            }
        }
        
        // borrowed from core-clr, with massive special-casing
        // https://github.com/dotnet/coreclr/blob/775003a4c72f0acc37eab84628fcef541533ba4e/src/mscorlib/src/System/Collections/Generic/ArraySortHelper.cs
        internal static void IntrospectiveSort(ulong* keys, int* values, int left, int length)
        {
            if (length < 2)
                return;

            IntroSort(keys, values, left, length + left - 1, 2 * IntrospectiveSortUtilities.FloorLog2(length));
        }

        private static void IntroSort(ulong* keys, int* values, int lo, int hi, int depthLimit)
        {
            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= IntrospectiveSortUtilities.IntrosortSizeThreshold)
                {
                    if (partitionSize == 1)
                    {
                        return;
                    }
                    if (partitionSize == 2)
                    {
                        SwapIfGreaterWithItems(keys, values, lo, hi);
                        return;
                    }
                    if (partitionSize == 3)
                    {
                        SwapIfGreaterWithItems(keys, values, lo, hi - 1);
                        SwapIfGreaterWithItems(keys, values, lo, hi);
                        SwapIfGreaterWithItems(keys, values, hi - 1, hi);
                        return;
                    }

                    InsertionSort(keys, values, lo, hi);
                    return;
                }

                if (depthLimit == 0)
                {
                    Heapsort(keys, values, lo, hi);
                    return;
                }
                depthLimit--;

                int p = PickPivotAndPartition(keys, values, lo, hi);
                // Note we've already partitioned around the pivot and do not have to move the pivot again.
                IntroSort(keys, values, p + 1, hi, depthLimit);
                hi = p - 1;
            }
        }
        private static void DownHeap(ulong* keys, int* values, int i, int n, int lo)
        {
            ulong d = keys[lo + i - 1];
            int dValue = values[lo + i - 1];
            int child;
            while (i <= n / 2)
            {
                child = 2 * i;
                if (child < n && keys[lo + child - 1] < keys[lo + child])
                {
                    child++;
                }
                if (keys[lo + child - 1] < d)
                    break;
                keys[lo + i - 1] = keys[lo + child - 1];
                values[lo + i - 1] = values[lo + child - 1];
                i = child;
            }
            keys[lo + i - 1] = d;
            values[lo + i - 1] = dValue;
        }

        private static void Swap(ulong* keys, int* values, int i, int j)
        {
            if (i != j)
            {
                ulong k = keys[i];
                keys[i] = keys[j];
                keys[j] = k;

                int v = values[i];
                values[i] = values[j];
                values[j] = v;
            }
        }
        private static void Heapsort(ulong* keys, int* values, int lo, int hi)
        {
            int n = hi - lo + 1;
            for (int i = n / 2; i >= 1; i = i - 1)
            {
                DownHeap(keys, values, i, n, lo);
            }
            for (int i = n; i > 1; i = i - 1)
            {
                Swap(keys, values, lo, lo + i - 1);
                DownHeap(keys, values, 1, i - 1, lo);
            }
        }

        private static int PickPivotAndPartition(ulong* keys, int* values, int lo, int hi)
        {
            // Compute median-of-three.  But also partition them, since we've done the comparison.
            int middle = lo + ((hi - lo) / 2);

            // Sort lo, mid and hi appropriately, then pick mid as the pivot.
            SwapIfGreaterWithItems(keys, values, lo, middle);  // swap the low with the mid point
            SwapIfGreaterWithItems(keys, values, lo, hi);   // swap the low with the high
            SwapIfGreaterWithItems(keys, values, middle, hi); // swap the middle with the high

            ulong pivot = keys[middle];
            Swap(keys, values, middle, hi - 1);
            int left = lo, right = hi - 1;  // We already partitioned lo and hi and put the pivot in hi - 1.  And we pre-increment & decrement below.

            while (left < right)
            {
                while (pivot > keys[++left]) ;
                while (pivot < keys[--right]) ;

                if (left >= right)
                    break;

                Swap(keys, values, left, right);
            }

            // Put pivot in the right location.
            Swap(keys, values, left, (hi - 1));
            return left;
        }

        private static void InsertionSort(ulong* keys, int* values, int lo, int hi)
        {
            int i, j;
            ulong t;
            int tValue;
            for (i = lo; i < hi; i++)
            {
                j = i;
                t = keys[i + 1];
                tValue = values[i + 1];
                while (j >= lo && t < keys[j])
                {
                    keys[j + 1] = keys[j];
                    values[j + 1] = values[j];
                    j--;
                }
                keys[j + 1] = t;
                values[j + 1] = tValue;
            }
        }
        private static void SwapIfGreaterWithItems(ulong* keys, int* values, int a, int b)
        {
            if (a != b && keys[a] > keys[b])
            {
                ulong key = keys[a];
                keys[a] = keys[b];
                keys[b] = key;

                int value = values[a];
                values[a] = values[b];
                values[b] = value;
            }
        }
        internal static class IntrospectiveSortUtilities
        {
            // This is the threshold where Introspective sort switches to Insertion sort.
            // Imperically, 16 seems to speed up most cases without slowing down others, at least for integers.
            // Large value types may benefit from a smaller number.
            internal const int IntrosortSizeThreshold = 16;

            internal const int QuickSortDepthThreshold = 32;

            internal static int FloorLog2(int n)
            {
                int result = 0;
                while (n >= 1)
                {
                    result++;
                    n = n / 2;
                }
                return result;
            }
        }

    }
}
