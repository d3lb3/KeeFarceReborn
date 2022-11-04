# KeeFarce Reborn

A standalone DLL that exports databases in cleartext once injected in the KeePass process. 

Heavily inspired by the great [KeeFarce](https://github.com/denandz/KeeFarce), [KeeThief](https://github.com/GhostPack/KeeThief) and [KeePassHax](https://github.com/HoLLy-HaCKeR/KeePassHax) projects.

## Yet another KeePass extraction tool, why ?

A few years ago, [@denandz](https://github.com/denandz) released [KeeFarce](https://github.com/denandz/KeeFarce), the first offensive tool designed to extract KeePass databases in cleartex. It works by injecting a DLL into the running process, then walks the heap using [ClrMD](https://github.com/microsoft/clrmd) to find the necessary objects and invoke KeePass's builtin export method using reflection. Its only downside at the time was that multiple files needed to be dropped on the target (the extraction DLL + ClrMD DLL + the injector + a bootstrap DLL).

A year later, [@tifkin_](https://twitter.com/tifkin_) and [@harmj0y](https://twitter.com/harmj0y) released an in-depth review of offensive techniques targeting KeePass (while not available on harmj0y's blog anymore, the articles can be found on Wayback Machine: [part 1](https://web.archive.org/web/20220123003835/http://www.harmj0y.net/blog/redteaming/a-case-study-in-attacking-keepass/), [part 2](https://web.archive.org/web/20220122225230/http://www.harmj0y.net/blog/redteaming/keethief-a-case-study-in-attacking-keepass-part-2/)). It resulted in the release of [KeeThief](https://github.com/GhostPack/KeeThief), a tool able to decrypt KeePass' masterkey (including when alternative authentication method are used). It worked so well that KeePass developpers [added a parameter](https://sourceforge.net/p/keepass/discussion/329220/thread/62b0b650/) to mitigate this technique (it can be disabled by editing KeePass configuration file if the user have enough rights, which is pretty common).

These tools quickly became my go-to during penetration testing, but they soon became obsolete as their injection techniques (namely, the famous Win32 APIs gang of *VirtualAllocEx*, *WriteProcessMemory*, *CreateRemoteThread*, *WriteProcessMemory*, etc) now immediately triggers detection. [@snovvcrash](https://twitter.com/snovvcrash) addressed this issue by forking KeeThief (now in a private repo, but still accessible [here](https://github.com/d3lb3/KeeThief)) to improve the injection mechanism with D/Invoke, writing a [great article](https://hackmag.com/coding/keethief/) detailing the process he followed. As this demonstrated the faisability of maintaining KeeThief, I find it difficult to implement with other injection techniques, as KeeThief's code is tightly linked to its injector.

[@holly-cracker](https://github.com/holly-hacker) also released [KeePassHax](https://github.com/HoLLy-HaCKeR/KeePassHax), which comes as a single DLL and only uses reflection to decrypt KeePass' masterkey. Inspired by this work, I decided to do the same with KeeFarce and write my own KeePass extraction tool with the following features:

- Self-sufficient ⇒ no interaction needed with the injector's code to work.
- Only uses builtin .NET libraries (in particular, no ClrMD) ⇒ better compatibility + single-file to make the injection easier.
- Exports the database ⇒ same as KeeFarce, no need to retrieve the .kdbx nor using a custom KeePass built to input the recovered masterkey.

## Building

As the code solely relies on .NET Framework with no external dependency, it should compile easily on Visual Studio 2015 and higher.

## Usage

Once the *KeePassReborn.dll* is compiled, **you will need to inject it by yourself** in the targeted KeePass process.

As I personally find it easier to stealthily inject shellcode than DLL in a remote process, the first thing I typically start with is generating a position-independent shellcode from our DLL. It appears that [@odzhan](https://twitter.com/modexpblog?lang=fr) and [@TheWover](https://twitter.com/thewover)'s [donut](https://github.com/TheWover/donut) project perfectly suits our needs !

We compile donut from a commit in the dev branch, as it fixes an [issue in application domain management](https://github.com/TheWover/donut/issues/44) that would prevent us from running in the default domain.

```
git clone https://github.com/TheWover/donut/
cd donut
git checkout 9d781d8da571eb1499122fc0e2d6e89e5a43603c
```

We can easily build from Visual Studio's *x64 Native Tools Command Prompt VS* with *nmake* utility:

```
nmake -f Makefile.msvc
```

Generating the shellcode is as simple as:

```powershell
.\donut.exe "C:\KeeFarceReborn\KeePassReborn\bin\Release\KeeFarceReborn.dll" -c KeeFarceReborn.Program -m Main -e 1

  [ Donut shellcode generator v0.9.3
  [ Copyright (c) 2019 TheWover, Odzhan

  [ Instance type : Embedded
  [ Module file   : "C:\KeeFarceReborn\KeePassReborn\bin\Release\KeeFarceReborn.dll"
  [ Entropy       : None
  [ File type     : .NET DLL
  [ Class         : KeeFarceReborn.Program
  [ Method        : Main
  [ Target CPU    : x86+amd64
  [ AMSI/WDLP     : continue
  [ Shellcode     : "loader.bin"
```

> Note that `-e` is necessary to disable entropy, otherwise the injected process won't be in the default application domain.

Let's compress it in PowerShell for easier integration in the injector's code:

```powershell
$bytes = [System.IO.File]::ReadAllBytes("C:\donut\loader.bin")
[System.IO.MemoryStream] $outStream = New-Object System.IO.MemoryStream
$deflateStream = New-Object System.IO.Compression.DeflateStream($outStream, [System.IO.Compression.CompressionLevel]::Optimal)
$deflateStream.Write($bytes, 0, $bytes.Length)
$deflateStream.Dispose()
$outBytes = $outStream.ToArray()
$outStream.Dispose()
$b64 = [System.Convert]::ToBase64String($outBytes)
Write-Output $b64 | clip
```

You now have a payload ready to be injected with your favourite technique. If you don't know what to do now, I suggest you check [ired.team Code & Process Injection page](https://www.ired.team/offensive-security/code-injection-process-injection) to get familiar with the concept, then have a look into [direct syscalls](https://jhalon.github.io/utilizing-syscalls-in-csharp-2/) and [D/Invoke](https://thewover.github.io/Dynamic-Invoke/) which will probably do the job in most cases. [@SEKTOR7](https://institute.sektor7.net/)'s malware development courses are full of great learnings if you can afford them. 

As an example, let's inject our payload using [snovvcrash](https://twitter.com/snovvcrash)'s VeraCrypt code (itself inspired by  SEKTOR7 courses) that makes use of D/Invoke. To demonstrate, I copied his project in the [SampleInjector](https://github.com/d3lb3/KeeFarceReborn/tree/main/SampleInjector) folder, we only need to paste our compressed shellcode then compile in x64.

> While it still bypasses Defender at the moment (november 2022), tinkering your own injector will of course be needed in order to bypass modern EDRs. This code is just here to demonstrate that everything behaves as expected.

By running *.\SampleInjector.exe* alongside an open KeePass database, you will see debug messages being printed in MessageBox (which should obviously be removed when used in a real penetration testing scenario) then find the exported database in the current user's *%APPDATA%* (choosed by default, as KeePass will be sure to have write access). The exported XML file can later be imported in any KeePass database without asking for a password.

>  If the export functionnality is disabled by policy, it can still be enabled by editing the KeePass.config.xml :
>
> ```xml
> <Policy>
>     <Export>false</Export>
> </Policy>
> ```

## Possible Caveats

### Detection

While the main issue concerning KeePass extraction tools detection is injectors, in-memory scan may also trigger alerts when analyzing the shellcode. You may want to obfuscate the shellcode and/or encrypt it when using with your injector.

Also, the code uses `Assembly.Load` to perform reflection once injected, and this behavior may be considered as malicious. I noticed that KeePass itself was sometimes using this method so I guess most protection won't be alerted. If you have insights on this, feel free to let me know.

### Compatibility

- The code only relies on .NET Framework with no external dependency, so should be fairly compatible with most targets.
- By default, KeeFarce Reborn Visual Studio solution targets .NET 4.6, which is installed by default on Windows 10 but can easily be changed.
- If .NET Framework is not installed on the target system, you can still install it manually from command line using `DISM.exe`.
- I only tested x64 payloads, but this should work as well for 32 bits architectures.

## Roadmap

- [ ] Check in details what parts of the DLL can trigger EDRs
- [ ] Provide an obfuscated version of the DLL
- [ ] Add option to log events into a file to debug while attacking a target, without using noisy message boxes

## Contribute

Pull requests are welcome. Feel free to open an issue or DM me on Twitter to suggest improvement.

## Credits

Bits of code where taken from the [KeeFarce](https://github.com/denandz/KeeFarce), [KeeThief](https://github.com/GhostPack/KeeThief) and [KeePassHax](https://github.com/HoLLy-HaCKeR/KeePassHax) projects. 

The sample injection mechanism was directely taken from [snovvcrash](https://twitter.com/snovvcrash)'s projects.
