﻿namespace System
open System.Reflection
[<assembly: AssemblyCopyright("Copyright © 2013. YaccConstructor Software Foundation https://code.google.com/p/recursive-ascent/")>]

[<assembly: AssemblyTitleAttribute("YaccConstructor")>]
[<assembly: AssemblyProductAttribute("YaccConstructor")>]
[<assembly: AssemblyDescriptionAttribute("Platform for parser generators and other grammarware research and development.")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
    let [<Literal>] InformationalVersion = "1.0"
