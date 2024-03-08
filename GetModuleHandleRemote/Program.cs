using System;
using System.Runtime.InteropServices;

namespace GetModuleHandleRemote
{
    class Program
    {
        [DllImport("ntdll.dll")] public static extern uint NtOpenProcess(ref IntPtr ProcessHandle, uint DesiredAccess, ref OBJECT_ATTRIBUTES ObjectAttributes, ref CLIENT_ID processId);
        [DllImport("ntdll.dll")] public static extern bool NtReadVirtualMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);
        [DllImport("ntdll.dll", SetLastError = true)] public static extern uint NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, IntPtr pbi, uint processInformationLength, out uint returnLength);

        public const uint PROCESS_QUERY_INFORMATION = 0x0400;
        public const uint PROCESS_VM_READ = 0x0010;

        [StructLayout(LayoutKind.Sequential)] public struct CLIENT_ID { public IntPtr UniqueProcess; public IntPtr UniqueThread; }
        [StructLayout(LayoutKind.Sequential, Pack = 0)] public struct OBJECT_ATTRIBUTES { public int Length; public IntPtr RootDirectory; public IntPtr ObjectName; public uint Attributes; public IntPtr SecurityDescriptor; public IntPtr SecurityQualityOfService; }


        public static IntPtr ReadIntPtr(IntPtr hProcess, IntPtr mem_address)
        {
            byte[] buff = new byte[8];
            NtReadVirtualMemory(hProcess, mem_address, buff, buff.Length, out _);
            long value = BitConverter.ToInt64(buff, 0);
            return (IntPtr)value;
        }


        public static string ReadWStr(IntPtr hProcess, IntPtr mem_address)
        {
            byte[] buff = new byte[256];
            NtReadVirtualMemory(hProcess, mem_address, buff, buff.Length, out _);
            string unicode_str = "";
            for (int i = 0; i < buff.Length - 1; i += 2)
            {
                if (buff[i] == 0 && buff[i + 1] == 0) { break; }
                unicode_str += BitConverter.ToChar(buff, i);
            }
            return unicode_str;
        }


        public unsafe static IntPtr CustomGetModuleHandleEx(IntPtr hProcess, String dll_name)
        {
            uint process_basic_information_size = 48;
            int peb_offset = 0x8;
            int ldr_offset = 0x18;
            int inInitializationOrderModuleList_offset = 0x30;
            int flink_dllbase_offset = 0x20;
            int flink_buffer_offset = 0x50;
            // If 32-bit process these offsets change
            if (IntPtr.Size == 4)
            {
                process_basic_information_size = 24;
                peb_offset = 0x4;
                ldr_offset = 0x0c;
                inInitializationOrderModuleList_offset = 0x1c;
                flink_dllbase_offset = 0x18;
                flink_buffer_offset = 0x30;
            }

            // Create byte array with the size of the PROCESS_BASIC_INFORMATION structure
            byte[] pbi_byte_array = new byte[process_basic_information_size];

            // Create a PROCESS_BASIC_INFORMATION structure in the byte array
            IntPtr pbi_addr = IntPtr.Zero;
            fixed (byte* p = pbi_byte_array)
            {
                pbi_addr = (IntPtr)p;

                NtQueryInformationProcess(hProcess, 0x0, pbi_addr, process_basic_information_size, out uint ReturnLength);
                Console.WriteLine("[+] Process_Basic_Information Address: \t\t0x" + pbi_addr.ToString("X"));
            }

            // Get PEB Base Address
            IntPtr peb_pointer = pbi_addr + peb_offset;
            Console.WriteLine("[+] PEB Address Pointer:\t\t\t0x" + peb_pointer.ToString("X"));
            IntPtr pebaddress = Marshal.ReadIntPtr(peb_pointer);
            Console.WriteLine("[+] PEB Address:\t\t\t\t0x" + pebaddress.ToString("X"));

            // Get Ldr 
            IntPtr ldr_pointer = pebaddress + ldr_offset;
            IntPtr ldr_adress = ReadIntPtr(hProcess, ldr_pointer); // Marshal.ReadIntPtr(ldr_pointer);

            Console.WriteLine("[+] LDR Pointer:\t\t\t\t0x" + ldr_pointer.ToString("X"));
            Console.WriteLine("[+] LDR Address:\t\t\t\t0x" + ldr_adress.ToString("X"));

            // Get InInitializationOrderModuleList (LIST_ENTRY) inside _PEB_LDR_DATA struct
            IntPtr InInitializationOrderModuleList = ldr_adress + inInitializationOrderModuleList_offset;
            Console.WriteLine("[+] InInitializationOrderModuleList:\t\t0x" + InInitializationOrderModuleList.ToString("X"));

            IntPtr next_flink = ReadIntPtr(hProcess, InInitializationOrderModuleList); // Marshal.ReadIntPtr(InInitializationOrderModuleList);

            IntPtr dll_base = (IntPtr)1337;
            while (dll_base != IntPtr.Zero)
            {
                next_flink = next_flink - 0x10;
                // Get DLL base address
                dll_base = ReadIntPtr(hProcess, (next_flink + flink_dllbase_offset)); // Marshal.ReadIntPtr(next_flink + flink_dllbase_offset);
                IntPtr buffer = ReadIntPtr(hProcess, (next_flink + flink_buffer_offset)); //Marshal.ReadIntPtr(next_flink + flink_buffer_offset);
                string base_dll_name = ReadWStr(hProcess, buffer);
                next_flink = ReadIntPtr(hProcess, (next_flink + 0x10)); // Marshal.ReadIntPtr(next_flink + 0x10);

                // Compare with DLL name we are searching
                if (dll_name.ToLower() == base_dll_name.ToLower())
                {
                    return dll_base;
                }
            }
            return IntPtr.Zero;
        }


        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("[-] Usage: GetModuleHandlerRemote.exe PROCESS_PID DLL_NAME.dll");
                System.Environment.Exit(0);
            }
            uint processPID = uint.Parse(args[0]);
            string dll_name = args[1];

            // Get process handle with NtOpenProcess
            IntPtr hProcess = IntPtr.Zero;
            CLIENT_ID client_id = new CLIENT_ID { UniqueProcess = (IntPtr)processPID, UniqueThread = IntPtr.Zero };
            OBJECT_ATTRIBUTES objAttr = new OBJECT_ATTRIBUTES();
            uint ntstatus = NtOpenProcess(ref hProcess, PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, ref objAttr, ref client_id);
            if (ntstatus != 0)
            {
                Console.WriteLine("[-] Could not open process with NtOpenProcess. Maybe you need to run the cmd as administrator or extra privileges (for example, SeDebugPrivilege for lsass)");
                System.Environment.Exit(0);
            }

            IntPtr dll_baseaddress = CustomGetModuleHandleEx(hProcess, dll_name);
            Console.WriteLine("[+] RESULT:\t\t\t\t\t0x" + dll_baseaddress.ToString("X"));
        }
    }
}