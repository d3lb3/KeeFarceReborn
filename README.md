# KeeFarce Reborn

A standalone DLL that exports databases in cleartext once injected in the KeePass process.

Heavily inspired by the great [KeeFarce](https://github.com/denandz/KeeFarce), [KeeThief](https://github.com/GhostPack/KeeThief) and [KeePassHax](https://github.com/HoLLy-HaCKeR/KeePassHax) projects.

## Yet another KeePass extraction tool, why ?

A few years ago, [@denandz](https://github.com/denandz) released [KeeFarce](https://github.com/denandz/KeeFarce), the first offensive tool designed to extract KeePass databases in cleartex. It works by injecting a DLL into the running process, then walks the heap using [ClrMD](https://github.com/microsoft/clrmd) to find the necessary objects and invoke KeePass's builtin export method using reflection. Its only downside at the time was that multiple files needed to be dropped on the target (the extraction DLL + ClrMD DLL + the injector + a bootstrap DLL).

A year later, [@tifkin_](https://twitter.com/tifkin_) and [@harmj0y](https://twitter.com/harmj0y) released an in-depth review of offensive techniques targeting KeePass (while not available on harmj0y's blog anymore, the articles can be found on Wayback Machine: [part 1](https://web.archive.org/web/20220123003835/http://www.harmj0y.net/blog/redteaming/a-case-study-in-attacking-keepass/), [part 2](https://web.archive.org/web/20220122225230/http://www.harmj0y.net/blog/redteaming/keethief-a-case-study-in-attacking-keepass-part-2/)). It resulted in the release of [KeeThief](https://github.com/GhostPack/KeeThief), a tool able to decrypt KeePass' masterkey (including when alternative authentication method are used). It worked so well that KeePass developpers [added a parameter](https://sourceforge.net/p/keepass/discussion/329220/thread/62b0b650/) to mitigate this technique (it can be disabled by editing KeePass configuration file if the user have enough rights, which is pretty common).

These tools quickly became my go-to during penetration testing, but they soon became obsolete as their injection techniques (namely, the famous Win32 APIs gang of *VirtualAllocEx*, *WriteProcessMemory*, *CreateRemoteThread*, *WriteProcessMemory*, etc) now immediately triggers detection. [@snovvcrash](https://twitter.com/snovvcrash) addressed this issue by forking KeeThief (now in a private repo, but still accessible [here](https://github.com/d3lb3/KeeThief)) to improve the injection mechanism with D/Invoke, writing a [great article](https://hackmag.com/coding/keethief/) detailing the process he followed. While it demonstrated the faisability of maintaining KeeThief, I find it difficult to regularly implement new injection techniques, as KeeThief's code is tightly linked to its injector.

[@holly-cracker](https://github.com/holly-hacker) also released [KeePassHax](https://github.com/HoLLy-HaCKeR/KeePassHax), which comes as a single DLL and only uses reflection to decrypt KeePass' masterkey. Inspired by this work, I decided to do the same with KeeFarce and write my own KeePass extraction tool with the following features:

- Self-sufficient ⇒ no interaction needed with the injector's code to work.
- Only uses builtin .NET libraries (no ClrMD) ⇒ better compatibility + single-file DLL makes the injection process easier.
- Exports the database (like KeeFarce) ⇒ no need to retrieve the .kdbx nor using a custom KeePass build to input the recovered masterkey.

KeeFarce Reborn also provides a KeePass plugin DLL to make KeePass inject itself without bothering with injectors!

## Building

As the code solely relies on .NET Framework with no external dependency, it should compile easily on Visual Studio 2015 and higher.

## Usage Example

**KeeFarce Reborn does not include an injector to load the DLL in KeePass process.** This is deliberate, as injectors become obsolete every few months you will have to use your own. The following parts demonstrate two typical ways to perform injection.

### Make KeePass inject KeeFarce Reborn as a plugin

*<u>Pre-requisite: write access to KeePass plugin directory.</u>*

KeePass features a [plugin framework](https://keepass.info/help/v2/plugins.html) to provide additional functionalities to users. It works by loading a DLL into KeePass process, allowing plugin developers to perform actions within KeePass' application domain. 

As a result, we can abuse this functionality to load KeeFarce Reborn as a plugin, without even having to use an external injector! You just need to compile  [KeeFarceRebornPlugin](https://github.com/d3lb3/KeeFarceReborn/tree/main/KeeFarceRebornPlugin) project and copy the DLL into the plugins directory  (located at at KeePass root, namely *"C:\Program Files\KeePass Password Safe 2\Plugins"* for a global install). 

> For the project to build correctly, you will need to copy the targeted KeePass.exe assembly version to KeeFarceRebornPlugin directory, or use the PLGX if you want the resulting plugin to be compatible with any KeePass version.

Next time KeePass is started and a database unlocked, the DLL will be loaded and the injection performed. If KeePass is already running, you will need to wait for its next restart for the injection to occur (or force the restart yourself).

⚠️ **Once a plugin is loaded, the DLL file will be write-protected until KeePass is closed. Keep that in mind to make sure that you don't leave malicious plugins behind you during assessments.**

>  If you don't have write access to KeePass plugin directory, you can have a look at [Quarkslab's article](https://blog.quarkslab.com/post-exploitation-abusing-the-keepass-plugin-cache.html) which demonstrates how to load plugins with less privileges through the plugin cache.

### Perform shellcode injection

*<u>Pre-requisite: KeePass is running + a database in unlocked by the user + you have enough rights to inject in the KeePass process.</u>*

Because I personally find it easier to stealthily inject shellcode than DLL in a remote process, the first thing I typically start with is generating a position-independent shellcode from our DLL. It appears that [@odzhan](https://twitter.com/modexpblog?lang=fr) and [@TheWover](https://twitter.com/thewover)'s [donut](https://github.com/TheWover/donut) project perfectly suits our needs !

We compile donut from a commit in the dev branch, as it fixes an [issue in application domain management](https://github.com/TheWover/donut/issues/44) that would prevent us from performing reflection in the default domain.

```
git clone https://github.com/TheWover/donut/
cd donut
git checkout 9d781d8da571eb1499122fc0e2d6e89e5a43603c
```

We can easily build from Visual Studio's *x64 Native Tools Command Prompt* with *nmake* utility:

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

> Note that `-e 1` is necessary to disable entropy, otherwise the injected process won't be in the default application domain.

Let's compress it using PowerShell for easier integration in the injector's code:

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

You now have a payload ready to be injected with your favourite technique. If you don't know what to do now, I suggest you check [ired.team Code & Process Injection page](https://www.ired.team/offensive-security/code-injection-process-injection) to get familiar with the concept. [@SEKTOR7](https://institute.sektor7.net/)'s malware development courses are full of great learnings if you can afford them. 

As an example, let's inject our payload using [snovvcrash](https://twitter.com/snovvcrash)'s VeraCryptThief code (itself inspired by  SEKTOR7 courses) which makes use of D/Invoke. To demonstrate, I copied his project in the [SampleInjector](https://github.com/d3lb3/KeeFarceReborn/tree/main/SampleInjector) folder, we only need to paste our compressed shellcode then compile in x64.

> While it still bypasses Defender at the moment (november 2022), tinkering your own injector will of course be needed in order to bypass modern EDRs. This sample injector is just here to demonstrate that everything behaves as expected.

By running *.\SampleInjector.exe* alongside an open KeePass database, the DLL will be loaded and the injection performed.

### Post-injection steps

Once the injection is performed, you will see debug messages being printed in MessageBox (which should obviously be removed when used in a real penetration testing scenario) then find the exported database in the current user's *%APPDATA%* (choosed by default, as KeePass will be sure to have write access). The exported XML file can later be imported in any KeePass database without asking for a password

## Possible Caveats

### KeePass policy prevents database export

If plugin or export functionnalities are disabled by policy, they can still be enabled by editing the *KeePass.config.xml* providing you have write access :

```xml
<Policy>
    <Plugins>true</Plugins>
    <Export>true</Export>
</Policy>
```

### Detection

While the main issue concerning KeePass extraction tools detection is injectors, in-memory scan may also trigger alerts when analyzing the shellcode or the plugin DLL. You may have to obfuscate and/or encrypt the shellcode to use with your injector.

 If plugin DLLs eventually get flagged by static analysis, you may still use KeePass' [PLGX plugin format](https://keepass.info/help/v2_dev/plg_index.html#plgx) to avoid detection.

### Compatibility

- KeeFarce Reborn only relies on .NET Framework with no external dependency, so it should be fairly compatible with most targets.
- By default, KeeFarce Reborn's Visual Studio solution targets .NET 4.6 (installed by default on Windows 10) but can be retargeted edited.
- If .NET Framework is not installed on the target system, you can still set it up manually from command line using `DISM.exe`.
- I only tested x64 payloads injection, but this should work as well with 32 bits architectures.

## Contribute

Pull requests are welcome. Feel free to open an issue or DM me on Twitter to suggest improvement.

## Credits

Bits of code where taken from the [KeeFarce](https://github.com/denandz/KeeFarce), [KeeThief](https://github.com/GhostPack/KeeThief) and [KeePassHax](https://github.com/HoLLy-HaCKeR/KeePassHax) projects. 

The sample injection mechanism was directely taken from [snovvcrash](https://twitter.com/snovvcrash)'s projects.
