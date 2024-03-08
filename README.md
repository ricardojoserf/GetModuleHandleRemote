# GetModuleHandleRemote

It works like the [GetModuleHandle](https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulehandlea) WinAPI but for remote processes: it takes a PID and DLL name, walks the PEB structure and returns the remote DLL base address. 

It uses the [NtQueryInformationProcess](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntqueryinformationprocess) and [NtReadVirtualMemory](http://undocumented.ntinternals.net/index.html?page=UserMode%2FUndocumented%20Functions%2FMemory%20Management%2FVirtual%20Memory%2FNtReadVirtualMemory.html) NTAPIs to get the base address and [NtOpenProcess](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddk/nf-ntddk-ntopenprocess) to open the process handle.

![img](https://raw.githubusercontent.com/ricardojoserf/ricardojoserf.github.io/master/images/getmodulehandleremote/Screenshot_1.png)
