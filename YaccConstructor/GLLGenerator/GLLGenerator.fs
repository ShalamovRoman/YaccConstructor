﻿//CHANGE IN STATES

namespace Yard.Generators.RNGLR

open Yard.Core
open IL
open Constraints
open Yard.Generators.RNGLR
open InitialConvert
open Yard.Generators.RNGLR.FinalGrammar
open States
open Printer
open TranslatorPrinter
open Option

type GLL() = 
    inherit Generator()
        override this.Name = "GLLGenerator"
        override this.Constraints = [|noEbnf; noMeta; noInnerAlt; noInnerAlt; noBrackets; needAC; singleModule|]
        override this.Generate (definition, args) =
            let start = System.DateTime.Now
            let args = args.Split([|' ';'\t';'\n';'\r'|]) |> Array.filter ((<>) "")
            let pairs = Array.zeroCreate <| args.Length / 2
            for i = 0 to pairs.Length-1 do
                pairs.[i] <- args.[i * 2], args.[i * 2 + 1]
            let getOption name either f =
                match definition.options.TryFind name with
                | Some v -> f v
                | None -> either
            let getBoolOption opt either =
                getOption opt either <| function
                    | "true" -> true
                    | "false" -> false
                    | x -> failwithf "Option %s expected values true or false, but %s found." opt x
            let mapFromType t = Map.ofList ["_", Some t]
            let mutable moduleName = getOption "module" "" id

            let mutable tokenType = getOption "token" definition.tokens mapFromType
            let mutable table = getOption "table" LL <| function
                                    | "LL" -> LL
                                    | x -> failwithf "Unsupported table type: %s." x
            let mutable fullPath = getBoolOption "fullpath" false
            let mutable positionType = getOption "pos" "" id
            let mutable needTranslate = getBoolOption "translate" true
            let mutable light = getBoolOption "light" true
            let mutable printInfiniteEpsilonPath = getOption "infEpsPath" "" id
            let mutable caseSensitive = getBoolOption "caseSensitive" false
            let mutable output =
                let fstVal = getOption "output" (definition.info.fileName + ".fs") id
                getOption "o" fstVal id
            let mutable targetLanguage =
                getOption "lang" FSharp <| function
                    | "fsharp" -> FSharp
                    | x -> failwithf "Unsupported output language: %s." x
            let getBoolValue name = function
                    | "true" -> true
                    | "false" -> false
                    | value -> failwithf "Unexpected %s value %s" name value

            for opt, value in pairs do
                match opt with
                | "-module" -> moduleName <- value
                | "-token" -> tokenType <- mapFromType value
                | "-pos" -> positionType <- value
                | "-o" -> if value.Trim() <> "" then output <- value
                | "-output" -> if value.Trim() <> "" then output <- value
                | "-table" -> table <- LL
                | "-caseSensitive" -> caseSensitive <- getBoolValue "caseSensitive" value
                | "-fullpath" -> fullPath <- getBoolValue "fullPath" value
                | "-translate" -> needTranslate <- getBoolValue "translate" value
                | "-light" -> light <- getBoolValue "light" value
                | "-infEpsPath" -> printInfiniteEpsilonPath <- value
                | "-lang" -> targetLanguage <- FSharp
                 
            let newDefinition = initialConvert definition
            let grammar = new FinalGrammar(newDefinition.grammar.[0].rules, caseSensitive)

            let printRules () =
                let printSymbol (symbol : int) =
                    if symbol < grammar.indexator.nonTermCount then
                        grammar.indexator.indexToNonTerm symbol
                    elif symbol >= grammar.indexator.termsStart && symbol <= grammar.indexator.termsEnd then
                        grammar.indexator.indexToTerm symbol
                    else grammar.indexator.indexToLiteral symbol
                printfn "\nrules:"
                for i = 0 to grammar.rules.rulesCount-1 do
                    printf "%4d: %s = " i <| printSymbol (grammar.rules.leftSide i)
                    for j = 0 to grammar.rules.length i - 1 do
                        printf "%s " <| printSymbol (grammar.rules.symbol i j)
                    printfn ""
            printRules ()

            if grammar.EpsilonCyclicNonTerms.Length > 0 then
                eprintfn "Grammar contains non-terminals, which can infinitely infer epsilon:"
                grammar.EpsilonCyclicNonTerms
                |> List.map (String.concat " <- ")
                |> List.iter (eprintfn "%s")
                eprintfn ""
                if printInfiniteEpsilonPath <> "" then
                    System.IO.Directory.CreateDirectory printInfiniteEpsilonPath |> ignore
                    for cycle in grammar.EpsilonCyclicNonTerms do
                        let nonTerm = List.head cycle
                        grammar.epsilonTrees.[grammar.indexator.nonTermToIndex nonTerm].AstToDot
                            grammar.indexator.indexToNonTerm (fun _ -> 0) grammar.rules.leftSideArr
                            (System.IO.Path.Combine (printInfiniteEpsilonPath, nonTerm + ".dot"))
                grammar.epsilonTrees |> Array.iter (fun t -> if t <> null then t.EliminateCycles())
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            let statesInterpreter = buildStates table grammar
  
            let tables = new Tables(grammar, statesInterpreter)
            use out = new System.IO.StreamWriter (output)
            let res = new System.Text.StringBuilder()
            let dummyPos = char 0
            let println (x : 'a) =
                Printf.kprintf (fun s -> res.Append(s).Append "\n" |> ignore) x
            let print (x : 'a) =
                Printf.kprintf (fun s -> res.Append(s) |> ignore) x
  //          let package, _class  =
  //                      match moduleName with
  //                      | "" -> "RNGLR","Parse"
  //                      | s when s.Contains "." -> s.Split '.' |> Array.rev |> (fun a -> a.[0], String.concat "." a.[1..])
  //                      | s -> "RNGLR",s
            let printHeaders moduleName fullPath light output targetLanguage =
                let fsHeaders() = 
                    println "%s" <| getPosFromSource fullPath dummyPos (defaultSource output)
                    println "module %s"
                    <|  match moduleName with
                        | "" -> "RNGLR.Parse"
                        | s -> s
                    if not light then
                        println "#light \"off\""
                    println "#nowarn \"64\";; // From fsyacc: turn off warnings that type variables used in production annotations are instantiated to concrete type"

                    println "open Yard.Generators.RNGLR.Parser"
                    println "open Yard.Generators.RNGLR"
                    println "open Yard.Generators.RNGLR.AST"

                    match definition.head with
                    | None -> ()
                    | Some (s : Source.t) ->
                        println "%s" <| getPosFromSource fullPath dummyPos s
                        println "%s" <| s.text + getPosFromSource fullPath dummyPos (defaultSource output)

               
            printHeaders moduleName fullPath light output targetLanguage
            let tables = printTables grammar definition.head tables moduleName tokenType res targetLanguage _class positionType caseSensitive
            let res = if not needTranslate || targetLanguage = Scala then tables
                      else tables + printTranslator grammar newDefinition.grammar.[0].rules
                                        positionType fullPath output dummyPos caseSensitive
            let res = 
                match definition.foot with
                | None -> res
                | Some (s : Source.t) ->
                    res + (getPosFromSource fullPath dummyPos s + "\n"
                                + s.text + getPosFromSource fullPath dummyPos (defaultSource output) + "\n")
            let res =
                match targetLanguage with
                | FSharp ->
                    let init = res.Replace("\r\n", "\n")
                    let curLine = ref 1// Must be 2, but there are (maybe) some problems with F# compiler, causing to incorrect warning
                    let res = new System.Text.StringBuilder(init.Length * 2)
                    for c in init do
                        match c with
                        | '\n' -> incr curLine; res.Append System.Environment.NewLine
                        | c when c = dummyPos -> res.Append (string !curLine)
                        | x -> res.Append x
                        |> ignore
                    res.ToString()
                | Scala -> res + "\n}"
            out.WriteLine res
            out.Close()
            eprintfn "Generation time: %A" <| System.DateTime.Now - start
            //(new YardPrinter()).Generate newDefinition
            box ()
        override this.Generate definition = this.Generate (definition, "")
