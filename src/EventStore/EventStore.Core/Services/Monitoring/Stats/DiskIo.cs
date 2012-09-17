﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.Monitoring.Stats
{
    public class DiskIo
    {
        public readonly ulong ReadBytes;
        public readonly ulong WrittenBytes;
        public readonly ulong ReadOps;
        public readonly ulong WriteOps;

        public readonly string ReadBytesFriendly;
        public readonly string WrittenBytesFriendly;
        public readonly string ReadOpsFriendly;
        public readonly string WriteOpsFriendly;

        public DiskIo(ulong bytesRead, ulong bytesWritten, ulong readOps, ulong writeOps)
        {
            ReadBytes = bytesRead;
            WrittenBytes = bytesWritten;
            ReadOps = readOps;
            WriteOps = writeOps;

            ReadBytesFriendly = bytesRead.ToFriendlySizeString();
            WrittenBytesFriendly = bytesWritten.ToFriendlySizeString();
            ReadOpsFriendly = ReadOps.ToFriendlyNumberString();
            WriteOpsFriendly = WriteOps.ToFriendlyNumberString();
        }

        public static DiskIo GetDiskIo(int procId, ILogger logger)
        {
            DiskIo io;

            if (OS.IsLinux)
            {
                var procIo = File.ReadAllText(string.Format("/proc/{0}/io", procId));
                io = ParseOnLinux(procIo, logger);
            }
            else
            {
                io = GetOnWindows(logger);
            }

            return io;
        }


        // http://stackoverflow.com/questions/3633286/understanding-the-counters-in-proc-pid-io
        public static DiskIo ParseOnLinux(string procIoStr, ILogger log)
        {
            ulong readBytes, writtenBytes, readOps, writeOps;

            try
            {
                var dict = procIoStr.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .ToDictionary(s => s.Split(':')[0].Trim(), s => s.Split(':')[1].Trim());
                readBytes = ulong.Parse(dict["read_bytes"]);
                writtenBytes = ulong.Parse(dict["write_bytes"]);
                readOps = ulong.Parse(dict["syscr"]);
                writeOps = ulong.Parse(dict["syscw"]);
            }
            catch (Exception ex)
            {
                log.InfoException(ex, "Couldn't parse linux stats");
                return null;
            }

            return new DiskIo(readBytes, writtenBytes, readOps, writeOps);
        }

        public static DiskIo GetOnWindows(ILogger log)
        {
            Ensure.NotNull(log, "log");

            IO_COUNTERS counters;
            Process proc = null;
            try
            {
                proc = Process.GetCurrentProcess();
                GetProcessIoCounters(proc.Handle, out counters);
            }
            catch (Exception ex)
            {
                log.InfoException(ex, "error while reading disk io on windows");
                return null;
            }
            finally
            {
                if (proc != null)
                    proc.Dispose();
            }
            return new DiskIo(counters.ReadTransferCount, counters.WriteTransferCount,
                              counters.ReadOperationCount, counters.WriteOperationCount);
        }


        // http://msdn.microsoft.com/en-us/library/ms683218%28VS.85%29.aspx
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll")]
        static extern bool GetProcessIoCounters(IntPtr ProcessHandle, out IO_COUNTERS IoCounters);

    }
}
