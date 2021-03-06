﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Sorted
{
    public static partial class RadixSort
    {
        public static int ParallelSort<T>(this Memory<T> keys, Memory<T> workspace, int r = DEFAULT_R, bool descending = false) where T : struct
        {
            if (Unsafe.SizeOf<T>() == 4)
            {
                return ParallelSort32<T>(
                    keys, workspace,
                    r, descending, uint.MaxValue, NumberSystem<T>.Value);
            }
            else
            {
                throw new NotSupportedException($"Sort type '{typeof(T).Name}' is {Unsafe.SizeOf<T>()} bytes, which is not supported");
            }
        }

        public static int ParallelSort(this Memory<uint> keys, Memory<uint> workspace, int r = DEFAULT_R, bool descending = false, uint mask = uint.MaxValue)
            => ParallelSort32<uint>(keys, workspace, r, descending, mask, NumberSystem.Unsigned);


        enum WorkerStep
        {
            Copy,
            BucketCountAscending,
            BucketCountDescending,
            ApplyAscending,
            ApplyDescending,
        }
        private sealed class Worker<T> where T : struct
        {
            private void ExecuteImpl()
            {
                var step = _step;

                while (true)
                {
                    int batchIndex = Interlocked.Increment(ref _batchIndex);
                    var start = _batchSize * batchIndex;
                    if (start >= _length) break;

                    var end = Math.Min(_batchSize + start, _length);
                    Execute(step, batchIndex, start, end);
                }

                lock(this)
                {
                    if (--_outstandingWorkersNeedsSync == 0)
                        Monitor.Pulse(this); // wake up the main thread
                }
            }

            private void Execute(WorkerStep step, int batchIndex, int start, int end)
            {
                int count = end - start;
                // Console.WriteLine($"{step}/{_shift}/{batchIndex}: [{start},{end}] ({count}) on thread {Environment.CurrentManagedThreadId}");

                switch (step)
                {
                    case WorkerStep.Copy:
                        _keys.Span.Slice(start, count).CopyTo(_workspace.Span.Slice(start, count));
                        break;
                    case WorkerStep.BucketCountAscending:
                    case WorkerStep.BucketCountDescending:
                        {
                            var keys = _keys.Span.NonPortableCast<T, uint>();
                            var buckets = _countsOffsets.Span.NonPortableCast<T, uint>().Slice(
                                                            batchIndex * _bucketCount, _bucketCount);
                            if (step == WorkerStep.BucketCountAscending)
                                BucketCountAscending(buckets, keys, start, end, _shift, _groupMask);
                            else
                                BucketCountDescending(buckets, keys, start, end, _shift, _groupMask);
                        }
                        break;
                    case WorkerStep.ApplyAscending:
                    case WorkerStep.ApplyDescending:
                        {
                            var offsets = _countsOffsets.Span.NonPortableCast<T, uint>().Slice(
                                                batchIndex * _bucketCount, _bucketCount);
                            if (NeedsApply(offsets))
                            {
                                var keys = _keys.Span.NonPortableCast<T, uint>();
                                var workspace = _workspace.Span.NonPortableCast<T, uint>();
                                if (step == WorkerStep.ApplyAscending)
                                    ApplyAscending(offsets, keys, workspace, start, end, _shift, _groupMask);
                                else
                                    ApplyDescending(offsets, keys, workspace, start, end, _shift, _groupMask);
                            }
                            else
                            {
                                _keys.Span.Slice(start, count).CopyTo(_workspace.Span.Slice((int)offsets[0], count));
                            }
                        }
                        break;
                    default:
                        throw new NotImplementedException($"Unknown worker step: {step}");
                }

            }

            private static bool NeedsApply(Span<uint> offsets)
            {
                var activeGroups = 0;
                var offset = offsets[0];
                for (int i = 1; i < offsets.Length; i++)
                {
                    if (offset != (offset = offsets[i]) && ++activeGroups == 2) return true;
                }
                return false;
            }

            public bool ComputeOffsets(int bucketOffset)
            {
                var allBuckets = _countsOffsets.Span.NonPortableCast<T, uint>();
                int bucketCount = _bucketCount,
                    batchCount = allBuckets.Length / bucketCount,
                    len = _length;

                uint offset = 0;
                for (int i = bucketOffset; i < bucketCount; i++)
                {
                    uint groupCount = 0;
                    int sourceOffset = i;
                    for (int j = 0; j < batchCount; j++)
                    {
                        var count = allBuckets[sourceOffset];
                        allBuckets[sourceOffset] = offset;
                        offset += count;
                        groupCount += count;

                        sourceOffset += bucketCount;
                    }
                    if (groupCount == len) return false; // all in one group
                }
                for (int i = 0; i < bucketOffset; i++)
                {
                    uint groupCount = 0;
                    int sourceOffset = i;
                    for (int j = 0; j < batchCount; j++)
                    {
                        var count = allBuckets[sourceOffset];
                        allBuckets[sourceOffset] = offset;
                        offset += count;
                        groupCount += count;

                        sourceOffset += bucketCount;
                    }
                    if (groupCount == len) return false; // all in one group
                }
                Debug.Assert(len == offset, "final offset should match length");
                return true;
            }

            readonly int _batchSize, _length, _bucketCount, _workerCount;
            readonly Memory<T> _countsOffsets;

            volatile WorkerStep _step;
            int _batchIndex, _shift;
            Memory<T> _keys, _workspace;
            uint _groupMask;

            static readonly WaitCallback _executeImpl = state => ((Worker<T>)state).ExecuteImpl();
            public void Execute(WorkerStep step)
            {
                lock (this)
                {
                    Interlocked.Exchange(ref _batchIndex, -1); // -1 because Interlocked.Increment returns the *incremented* value
                    _step = step;
                    if (_outstandingWorkersNeedsSync != 0) throw new InvalidOperationException("There are still outstanding workers!");
                    _outstandingWorkersNeedsSync = _workerCount;
                }
                for (int i = 1; i < _workerCount; i++) // the current thread will be worker 0
                {
                    ThreadPool.QueueUserWorkItem(_executeImpl, this);
                }
                ExecuteImpl(); // lend a hand ourselves
                lock (this)
                {
                    if (_outstandingWorkersNeedsSync != 0 && !Monitor.Wait(this, millisecondsTimeout: 10_000))
                        throw new TimeoutException("Timeout waiting for parallel workers to complete");
                }
            }
            int _outstandingWorkersNeedsSync;
            public Worker(int workerCount, int bucketCount, Memory<T> keys, Memory<T> workspace, Memory<T> countsOffsets)
            {
                if (workerCount <= 0) throw new ArgumentOutOfRangeException(nameof(workerCount));
                if (keys.Length != workspace.Length) throw new ArgumentException("Workspace size mismatch", nameof(workspace));
                _batchSize = ((keys.Length - 1) / workerCount) + 1;
                _length = keys.Length;
                _keys = keys;
                _workspace = workspace;
                _countsOffsets = countsOffsets;
                _bucketCount = bucketCount;
                _workerCount = workerCount;
            }

            public void Swap() => RadixSort.Swap(ref _keys, ref _workspace);

            internal void SetGroup(uint groupMask, int shift)
            {
                lock (this)
                {
                    _groupMask = groupMask;
                    _shift = shift;
                }
            }
        }
        static readonly int MaxWorkerCount = Environment.ProcessorCount;

        private static int WorkerCount(int count)
        {
            if (count <= 0) return 0;
            return Math.Min(((count - 1) / 1024) + 1, MaxWorkerCount);
        }

        private static int ParallelSort32<T>(Memory<T> keys, Memory<T> workspace,
            int r, bool descending, uint keyMask, NumberSystem numberSystem) where T : struct
        {
            if (keys.Length <= 1 || keyMask == 0) return 0;
            if (workspace.Length < ParallelWorkspaceSize<uint>(keys.Length, r))
                throw new ArgumentException($"The workspace provided is insufficient ({workspace.Length} vs {ParallelWorkspaceSize<uint>(keys.Length, r)} needed); the {nameof(ParallelWorkspaceSize)} method can be used to determine the minimum size required", nameof(workspace));

            int bucketCount = 1 << r, len = keys.Length;

            int countsOffsetsAsT = (((bucketCount << 2) - 1) / Unsafe.SizeOf<T>()) + 1;
            int workerCount = WorkerCount(len);
            var workerCountsOffsets = workspace.Slice(0, countsOffsetsAsT * workerCount);

            workspace = workspace.Slice(countsOffsetsAsT * workerCount, len);
            int groups = GroupCount<uint>(r);
            uint mask = (uint)(bucketCount - 1);

            var worker = new Worker<T>(workerCount, bucketCount, keys, workspace, workerCountsOffsets);

            bool reversed = false;
          
            int invertC = numberSystem != NumberSystem.Unsigned ? groups - 1 : -1;
            for (int c = 0, shift = 0; c < groups; c++, shift += r)
            {
                uint groupMask = (keyMask >> shift) & mask;
                keyMask &= ~(mask << shift); // remove those bits from the keyMask to allow fast exit
                if (groupMask == 0)
                {
                    if (keyMask == 0) break;
                    else continue;
                }

                // counting elements of the c-th group
                worker.SetGroup(groupMask, shift);
                worker.Execute(descending ? WorkerStep.BucketCountDescending : WorkerStep.BucketCountAscending);
                if (!worker.ComputeOffsets(c == invertC ? GetInvertStartIndex(32, r) : 0)) continue; // all in one group

                worker.Execute(descending ? WorkerStep.ApplyDescending : WorkerStep.ApplyAscending);
                worker.Swap();
                reversed = !reversed;
            }
            if (reversed) worker.Execute(WorkerStep.Copy);
            return workerCount;
        }

        public static int ParallelWorkspaceSize<T>(Span<T> keys, int r = DEFAULT_R) => ParallelWorkspaceSize<T>(keys.Length, r);
        public static int ParallelWorkspaceSize<T>(int count, int r = DEFAULT_R)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (r < 1 || r > MAX_R) throw new ArgumentOutOfRangeException(nameof(r));
            if (count <= 1) return 0;

            int countLength = 1 << r;
            int countsOffsetsAsT = (((countLength << 2) - 1) / Unsafe.SizeOf<T>()) + 1;

            return (countsOffsetsAsT * WorkerCount(count)) + count;
        }
    }
}
