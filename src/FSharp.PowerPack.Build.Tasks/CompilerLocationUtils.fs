namespace Internal.Utilities
open System
open System.IO
open System.Configuration
open System.Reflection
open Microsoft.Win32
open System.Runtime.InteropServices

#nowarn "44" // ConfigurationSettings is obsolete but the new stuff is horribly complicated. 

module internal FSharpEnvironment =

    let FSharpCoreLibRunningVersion = 
        try match (typeof<Microsoft.FSharp.Collections.List<int>>).Assembly.GetName().Version.ToString() with
            | null -> None
            | "" -> None
            | s  -> Some(s)
        with _ -> None

    // Returns:
    // -- on 2.0:  "v2.0.50727"
    // -- on 4.0:  "v4.0.30109" (last 5 digits vary by build)
    let MSCorLibRunningRuntimeVersion = 
        typeof<int>.Assembly.ImageRuntimeVersion

    // The F# team version number. This version number is used for
    //     - the F# version number reported by the fsc.exe and fsi.exe banners in the CTP release
    //     - the F# version number printed in the HTML documentation generator
    //     - the .NET DLL version number for all VS2008 DLLs
    //     - the VS2008 registry key, written by the VS2008 installer
    //         HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework\AssemblyFolders\Microsoft.FSharp-" + FSharpTeamVersionNumber
    // Also
    //     - for Beta2, the language revision number indicated on the F# language spec
    //
    // It is NOT the version number listed on FSharp.Core.dll
    let FSharpTeamVersionNumber = "1.9.9.9"

    // The F# binary format revision number. The first three digits of this form the significant part of the 
    // format revision number for F# binary signature and optimization metadata. The last digit is not significant.
    //
    // WARNING: Do not change this revision number unless you absolutely know what you're doing.
    let FSharpBinaryMetadataFormatRevision = "2.0.0.0"

    [<DllImport("Advapi32.dll", CharSet = CharSet.Unicode, BestFitMapping = false)>]
    extern uint32 RegOpenKeyExW(UIntPtr _hKey, string _lpSubKey, uint32 _ulOptions, int _samDesired, UIntPtr & _phkResult);

    [<DllImport("Advapi32.dll", CharSet = CharSet.Unicode, BestFitMapping = false)>]
    extern uint32 RegQueryValueExW(UIntPtr _hKey, string _lpValueName, uint32 _lpReserved, uint32 & _lpType, IntPtr _lpData, int & _lpchData);

    [<DllImport("Advapi32.dll")>]
    extern uint32 RegCloseKey(UIntPtr _hKey)

    module Option = 
        /// Convert string into Option string where null and String.Empty result in None
        let ofString s = 
            if String.IsNullOrEmpty(s) then None
            else Some(s)

            
        

    // MaxPath accounts for the null-terminating character, for example, the maximum path on the D drive is "D:\<256 chars>\0". 
    // See: ndp\clr\src\BCL\System\IO\Path.cs
    let maxPath = 260;
    let maxDataLength = (new System.Text.UTF32Encoding()).GetMaxByteCount(maxPath)
    let KEY_WOW64_DEFAULT = 0x0000
    let KEY_WOW64_32KEY = 0x0200
    let HKEY_LOCAL_MACHINE = UIntPtr(0x80000002u)
    let KEY_QUERY_VALUE = 0x1
    let REG_SZ = 1u

    let GetDefaultRegistryStringValueViaDotNet(subKey: string)  =
        Option.ofString
            (try
                downcast Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\"+subKey,null,null)
             with e->
                System.Diagnostics.Debug.Assert(false, sprintf "Failed in GetDefaultRegistryStringValueViaDotNet: %s" (e.ToString()))
                null)

    let Get32BitRegistryStringValueViaPInvoke(subKey:string) = 
        Option.ofString
            (try 
                // 64 bit flag is not available <= Win2k
                let options = 
                    match Environment.OSVersion.Version.Major with
                    | major when major >= 5 -> KEY_WOW64_32KEY
                    | _ -> KEY_WOW64_DEFAULT


                let mutable hkey = UIntPtr.Zero;
                let pathResult = Marshal.AllocCoTaskMem(maxDataLength);

                try
                    let res = RegOpenKeyExW(HKEY_LOCAL_MACHINE,subKey, 0u, KEY_QUERY_VALUE ||| options, & hkey)
                    if res = 0u then
                        let mutable uType = REG_SZ;
                        let mutable cbData = maxDataLength;

                        let res = RegQueryValueExW(hkey, null, 0u, &uType, pathResult, &cbData);

                        if (res = 0u && cbData > 0 && cbData <= maxDataLength) then
                            Marshal.PtrToStringUni(pathResult, (cbData - 2)/2);
                        else 
                            null
                    else
                        null
                finally
                    if hkey <> UIntPtr.Zero then
                        RegCloseKey(hkey) |> ignore
                
                    if pathResult <> IntPtr.Zero then
                        Marshal.FreeCoTaskMem(pathResult)
             with e->
                System.Diagnostics.Debug.Assert(false, sprintf "Failed in Get32BitRegistryStringValueViaPInvoke: %s" (e.ToString()))
                null)

    let is32Bit = IntPtr.Size = 4
    
    let tryRegKey(subKey:string) = 

        if is32Bit then
            let s = GetDefaultRegistryStringValueViaDotNet(subKey)
            // If we got here AND we're on a 32-bit OS then we can validate that Get32BitRegistryStringValueViaPInvoke(...) works
            // by comparing against the result from GetDefaultRegistryStringValueViaDotNet(...)
#if DEBUG
            let viaPinvoke = Get32BitRegistryStringValueViaPInvoke(subKey)
            System.Diagnostics.Debug.Assert((s = viaPinvoke), sprintf "32bit path: pi=%A def=%A" viaPinvoke s)
#endif
            s
        else
            Get32BitRegistryStringValueViaPInvoke(subKey) 
               

    let internal tryCurrentDomain() = 
        let pathFromCurrentDomain = System.AppDomain.CurrentDomain.BaseDirectory
        if not(String.IsNullOrEmpty(pathFromCurrentDomain)) then 
            Some pathFromCurrentDomain
        else
            None
    
    let internal tryAppConfig (appConfigKey:string) = 

        let locationFromAppConfig = ConfigurationSettings.AppSettings.[appConfigKey]
        System.Diagnostics.Debug.Print(sprintf "Considering appConfigKey %s which has value '%s'" appConfigKey locationFromAppConfig) 

        if String.IsNullOrEmpty(locationFromAppConfig) then 
            None
        else
            let exeAssemblyFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            let locationFromAppConfig = locationFromAppConfig.Replace("{exepath}", exeAssemblyFolder)
            System.Diagnostics.Debug.Print(sprintf "Using path %s" locationFromAppConfig) 
            Some locationFromAppConfig

    let BinFolderOfDefaultFSharpCoreReferenceAssembly = 
        try
            let result = tryAppConfig "fsharp-core-referenceassembly-location"
            match result with
            | Some _ -> result
            | None ->
                let keyCTP = @"Software\Microsoft\.NETFramework\AssemblyFolders\Microsoft.FSharp-" + FSharpTeamVersionNumber 
                let keyRedist = @"SOFTWARE\Microsoft\.NETFramework\" + MSCorLibRunningRuntimeVersion + @"\AssemblyFoldersEx\Microsoft Visual F# 4.0"
                
                let result = tryRegKey keyRedist
                match result with 
                | Some _ ->  result 
                | None -> 
                    let result = tryRegKey keyCTP
                    match result with
                    | Some _ -> result
                    | None -> 
                        tryCurrentDomain ()

        with e -> 
            System.Diagnostics.Debug.Assert(false, "Error while determining default location of FSharp.Core reference assembly")
            None

    // The default location of FSharp.Core.dll and fsc.exe based on the version of fsc.exe that is running
    // Used for
    //     - location of design-time copies of FSharp.Core.dll and FSharp.Compiler.Interactive.Settings.dll for the default assumed environment for scripts
    //     - default ToolPath in tasks in FSharp.Build.dll (for Fsc tasks)
    //     - default F# binaries directory in service.fs (REVIEW: check this)
    //     - default location of fsi.exe in FSharp.VS.FSI.dll
    //     - default location of fsc.exe in FSharp.Compiler.CodeDom.dll
    let BinFolderOfDefaultFSharpCompiler = 
        // Check for an app.config setting to redirect the default compiler location
        // Like fsharp-compiler-location
        try 
            let result = tryAppConfig "fsharp-compiler-location"
            match result with 
            | Some _ ->  result 
            | None -> 

                // Note: If the keys below change, be sure to update code in:
                // Property pages (ApplicationPropPage.vb)

                let key20 = @"Software\Microsoft\.NETFramework\AssemblyFolders\Microsoft.FSharp-" + FSharpTeamVersionNumber 
                let key40 = @"Software\Microsoft\FSharp\2.0\Runtime\v4.0"
                let key1,key2 = 
                    match FSharpCoreLibRunningVersion with 
                    | None -> key20,key40 
                    | Some v -> if v.Length > 1 && v.[0] <= '3' then key20,key40 else key40,key20
                
                let result = tryRegKey key1
                match result with 
                | Some _ ->  result 
                | None -> 
                    let result =  tryRegKey key2
                    match result with 
                    | Some _ ->  result 
                    | None -> 

            // This was failing on rolling build for staging because the prototype compiler doesn't have the key. Disable there.
            #if FX_ATLEAST_40_COMPILER_LOCATION
                        System.Diagnostics.Debug.Assert(result<>None, sprintf "Could not find location of compiler at '%s' or '%s'" key1 key2)
            #endif                                
                          
                            // For the prototype compiler, we can just use the current domain
                        tryCurrentDomain()
        with e -> 
            System.Diagnostics.Debug.Assert(false, "Error while determining default location of F# compiler")
            None

    let BinFolderOfFSharpPowerPack = 
        try 
            // Check for an app.config setting to redirect the default compiler location
            // Like fsharp-compiler-location
            let result = tryAppConfig "fsharp-powerpack-location"
            match result with 
            | Some _ ->  result 
            | None -> 

                let key20 = @"Software\Microsoft\.NETFramework\AssemblyFolders\FSharp.PowerPack-" + FSharpTeamVersionNumber 
                let result = tryRegKey key20
                match result with 
                | Some _ ->  result 
                | None -> 
                      
                    tryCurrentDomain()

        with e -> 
            System.Diagnostics.Debug.Assert(false, "Error while determining default location of F# power pack tools")
            None
