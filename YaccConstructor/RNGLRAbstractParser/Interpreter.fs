﻿//  Parser.fs contains methods, needed to build an AST
//
//  Copyright 2011-2012 Avdyukhin Dmitry
//
//  This file is part of YaccConctructor.
//
//  YaccConstructor is free software:you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

module Yard.Generators.RNGLR.Parser

open Yard.Generators.RNGLR
open Yard.Generators.RNGLR.AST
open System.Collections.Generic
open Microsoft.FSharp.Text.Lexing
open Yard.Generators.RNGLR.DataStructures
open Yard.Generators.RNGLR.Parser


[<Struct>]
type TokenWithInfo<'token> =
    val Token  : 'token    
    val NxtLvl : int
    val CurNum : int
    new (t,nl,cn) = {Token=t;NxtLvl=nl;CurNum=cn}

[<Struct>]
type TokensInfo<'token> =
    val Tokens  : array<TokenWithInfo<'token>>
    val CurLvl : int
    new (ts,cl) = {Tokens=ts;CurLvl=cl}

let buildAstAbstract<'TokenType> (parserSource : ParserSource<'TokenType>) (tokens : seq<int*array<'TokenType*int>>) =
    let processChunk (cl,ts) =
        new TokensInfo<_>(Array.map (fun (t,nl) -> new TokenWithInfo<_>(t,nl, parserSource.TokenToNumber t))ts,cl)
    
    let pushesMap = new Dictionary<_,ResizeArray<_>>(10)

    let enum = tokens.GetEnumerator()
    // Change if it doesn't equal to zero. Now it's true according to states building algorithm
    let startState = 0
    let startNonTerm = parserSource.LeftSide.[parserSource.StartRule]
    let nonTermsCountLimit = 1 + (Array.max parserSource.LeftSide)
    let getEpsilon =
        let epsilons = Array.init nonTermsCountLimit (fun i -> box (-i-1))
        fun i -> epsilons.[i]
                          
    // If input stream is empty or consists only of EOF token
    if not <| enum.MoveNext() || Array.forall (fun t -> parserSource.EofIndex = parserSource.TokenToNumber (fst t)) (snd enum.Current) then
        if parserSource.AcceptEmptyInput then
            Success <| new Tree<_>(null, getEpsilon startNonTerm, null)
        else
            Error (0, Unchecked.defaultof<_>, "This grammar cannot accept empty string",
                    {
                        drawGSSDot = fun _ -> ()
                        lastTokens = fun _ -> [||]
                    })
    else                                     
        // Currently processed token
        let curTokens = processChunk enum.Current |> ref
        let curLvl = ref (!curTokens).CurLvl
        let isEOF = ref false
        /// Here all tokens from the input will be collected
        let tokens = new BlockResizeArray<_>()
        let reductions = new Stack<_>(10)
        let statesCount = parserSource.Gotos.Length
        // New edges can be created only from last level.
        /// Temporary storage for edges data (after all reductions real edges will be created).
        let edges = Array.init statesCount (fun _ -> new ResizeArray<Vertex * Family * AST>())
        let simpleEdges = Array.init statesCount (fun _ -> new ResizeArray<Vertex * obj>())
        let pushes = ref [||]
        let pushesInitFun l = 
            pushes := Array.init l (fun _ -> new Stack<_> (statesCount * 2 + 10))  
        pushesInitFun curTokens.Value.Tokens.Length
        /// Stores states, used on current level. Instead statesCount must be number of non-terminals, but doesn't matter
        let usedStates = new Stack<_>(statesCount)
        let stateToVertex : Vertex[] = Array.zeroCreate statesCount

        //let newRules = new BlockResizeArray<_>()

        let addVertex vertData =
            [|
            for (state, level, (edgeOpt : option<Vertex * obj>)) in vertData |> Array.sortBy (fun (_,x,_) -> -x) do
                let curNums = if !isEOF then [|parserSource.EofIndex|] else curTokens.Value.Tokens |> Array.map (fun t -> t.CurNum)
                if stateToVertex.[state] = null then
                    let v = new Vertex(state, level)
                    stateToVertex.[state] <- v
                    curNums
                    |> Array.iteri (fun i curNum ->
                        let push = parserSource.Gotos.[state].[curNum]
                        // Push to init state is impossible
                        if push <> 0 then
                            pushes.Value.[i].Push (v, push)
                        let arr = parserSource.ZeroReduces.[state].[curNum]
                        if arr <> null then
                            for prod in arr do
                                reductions.Push (v, prod, 0, None)
                    )
                    usedStates.Push state
                let v = stateToVertex.[state]
                if edgeOpt.IsSome then
                    for curNum in curNums do
                        let arr = parserSource.Reduces.[state].[curNum]
                        if arr <> null then
                            for (prod, pos) in arr do
                                reductions.Push (v, prod, pos, edgeOpt)
                yield v
            |]

        ignore <| addVertex [|startState, 0, None|]
        let inline trd (_,_,x) = x
        let makeReductions () =
            while reductions.Count > 0 do
                let vertex, prod, pos, edgeOpt = reductions.Pop()
                let nonTerm = parserSource.LeftSide.[prod]

                let handlePath (path : obj[]) (final : Vertex) =
                    let state = parserSource.Gotos.[final.State].[nonTerm]
                    let newVertex = (addVertex [|state, !curLvl, None|]).[0]
                    
                    let family = new Family(prod, new Nodes(Array.copy path))
                    if not <| containsEdge final family edges.[state] then
                        let isCreated, edgeLabel = addEdge final family edges.[state]
                        let curNums = 
                            if (pos > 0 && isCreated) 
                            then [|parserSource.EofIndex|]
                            else curTokens.Value.Tokens |> Array.map (fun x -> x.CurNum )                            
                        for curNum in curNums do 
                            let arr = parserSource.Reduces.[state].[curNum]
                            if arr <> null then
                                for (prod, pos) in arr do
                                    reductions.Push (newVertex, prod, pos, Some (final, box edgeLabel))

                let rec walk remainLength (vertex : Vertex) path =
                    if remainLength = 0 then handlePath path vertex
                    else
                        if vertex.Level <> !curLvl then
                            if vertex.OutEdges.other <> null then
                                vertex.OutEdges.other |> Array.iter
                                    (fun e ->
                                        path.[remainLength - 1] <- e.Ast
                                        walk (remainLength - 1) e.Dest path)
                            path.[remainLength - 1] <- vertex.OutEdges.first.Ast
                            walk (remainLength - 1) vertex.OutEdges.first.Dest path
                        else
                            simpleEdges.[vertex.State] |> ResizeArray.iter(fun (v,a) ->
                                    path.[remainLength - 1] <- a
                                    walk (remainLength - 1) v path)
                            
                            let mutable i = 0
                            let edges = edges.[vertex.State]
                            let mutable count = 0
                            while i < edges.Count do
                                let (v,_,a) = edges.[i]
                                let mutable j = i+1
                                path.[remainLength - 1] <- box a
                                walk (remainLength - 1) v path
                                while j < edges.Count && trd edges.[j] = a do
                                    j <- j + 1
                                i <- j
                
                if pos = 0 then
                    let state = parserSource.Gotos.[vertex.State].[nonTerm]
                    addVertex [|state, !curLvl, None|] |> ignore
                    let ast = getEpsilon parserSource.LeftSide.[prod]
                    if not <| containsSimpleEdge vertex ast simpleEdges.[state] then
                        addSimpleEdge vertex ast simpleEdges.[state]
                else 
                    let path = Array.zeroCreate pos
                    path.[pos - 1] <- snd edgeOpt.Value
                    walk (pos - 1) (fst edgeOpt.Value) path

        let curInd = ref 0
        let isEnd = ref false
        let attachEdges () =
            let inline snd3 (_,x,_) = x
            for vertex in usedStates do
                let mutable i = 0
                let edges = edges.[vertex]
                let mutable count = -1
                while i < edges.Count do
                    let k = trd edges.[i]
                    let mutable j = i+1
                    while j < edges.Count && trd edges.[j] = k do
                        j <- j + 1
                    i <- j
                    count <- count + 1
                count <- count + simpleEdges.[vertex].Count
                let vEdges =
                    if count > 0 then Array.zeroCreate count
                    else null
                let mutable first = Unchecked.defaultof<_>
                i <- 0
                count <- -1
                while i < edges.Count do
                    let (v,_,a) = edges.[i]
                    let mutable j = i+1
                    while j < edges.Count && trd edges.[j] = a do
                        j <- j + 1
                    let other = 
                        if j <> i + 1 then
                            let res = Array.zeroCreate (j - i - 1)
                            for k = i + 1 to j-1 do
                                res.[k-i-1] <- snd3 edges.[k]
                            res
                        else
                            null
                    if count >= 0 then
                        vEdges.[count] <- new Edge(v, box a)
                    else
                        first <- new Edge(v, box a)
                    count <- count + 1
                    a.first <- snd3 edges.[i]
                    a.other <- other
                    i <- j

                for i = 0 to simpleEdges.[vertex].Count - 1 do
                    let v, a = simpleEdges.[vertex].[i]
                    if count >= 0 then
                        vEdges.[count] <- new Edge(v, a)
                    else
                        first <- new Edge(v, a)
                    count <- count + 1

                stateToVertex.[vertex].OutEdges <- UsualOne<_>(first, vEdges)
                edges.Clear()
                simpleEdges.[vertex].Clear()

        let shift () =
            let newAstNodes = curTokens.Value.Tokens |> Array.mapi (fun i _ -> tokens.Count + i |> box)
            let oldTokens = !curTokens
            curTokens.Value.Tokens |> Array.iter (fun t -> tokens.Add t.Token)
            if enum.MoveNext() then
                curTokens := processChunk enum.Current
                curLvl := (!curTokens).CurLvl
                isEOF := curTokens.Value.Tokens.Length = 0  
            else
                isEOF := true
            let oldPushes = !pushes |> Array.map (fun p -> p.ToArray())
            for i in 0..oldTokens.Tokens.Length-1 do
                let newAstNode = newAstNodes.[i]
                let oldPushes = oldPushes.[i]
                let num = oldTokens.Tokens.[i].NxtLvl                
                if pushesMap.ContainsKey num
                then 
                    pushesMap.[num].Add((oldPushes,newAstNode)) 
                else pushesMap.Add(num,new ResizeArray<_>([|oldPushes,newAstNode|]))
            pushesInitFun curTokens.Value.Tokens.Length

        let applyStashedPushes () =
            for vertex in usedStates do
                stateToVertex.[vertex] <- null
            usedStates.Clear()
            for  oldPushes,newAstNode in pushesMap.[!curLvl] do
                for (vertex, state) in oldPushes do
                    let newVertex = addVertex [|state, !curLvl,  Some (vertex, newAstNode)|]
                    addSimpleEdge vertex newAstNode simpleEdges.[state]
            pushesMap.Remove(!curLvl) |> ignore
            makeReductions ()
            attachEdges()
            let vertices = usedStates.ToArray() |> Array.map (fun i -> stateToVertex.[i])                    
            drawDot parserSource.TokenToNumber tokens parserSource.LeftSide vertices parserSource.NumToString parserSource.ErrorIndex
                        <| sprintf "dot/stack_%d_4.dot" !curLvl             

        let mutable errorList = []                    
        let errorRuleExist = parserSource.ErrorRulesExists
        let mutable wasError = ref false
        let lastErr = ref -1

        while not !isEnd && not !wasError do        
            if usedStates.Count = 0 && reductions.Count = 0
            then
                let errInfo = !curTokens
                errorList <- errInfo :: errorList
                wasError <- ref true
            else
                if !isEOF
                then 
                    applyStashedPushes ()
                    isEnd := true
                (*elif pushes.Count = 0 then 
                    if !curLvl - !lastErr > 1 
                    then
                        let errInfo =  !curLvl, !curToken
                        errorList <- errInfo :: errorList
                    if errorRuleExist 
                    then 
                        //recovery()
                        lastErr := !curLvl
                    else wasError <- ref true*)
                else
                    if !curLvl > 0
                    then
                        if pushesMap.ContainsKey !curLvl
                        then applyStashedPushes ()
                        else
                            for vertex in usedStates do
                                stateToVertex.[vertex] <- null
                            usedStates.Clear()                                        
                    shift ()
        
        let isAcceptState() = 
            let flag = ref false
            for state in usedStates do
                if not !flag 
                then
                    flag := parserSource.AccStates.[state]
            !flag                    

        // if finish isn't accepting state then error
        if !isEnd && usedStates.Count > 0 && not <| isAcceptState() 
        then
            //recovery()
            makeReductions ()
            attachEdges()

        let lastTokens count =
            [| for i = max 0 (tokens.Count-count) to tokens.Count-1 do
                yield tokens.[i]|]
        let debugFuns () =
            let vertices = usedStates.ToArray() |> Array.map (fun i -> stateToVertex.[i])
            {
                drawGSSDot = drawDot parserSource.TokenToNumber tokens parserSource.LeftSide vertices parserSource.NumToString parserSource.ErrorIndex
                lastTokens = lastTokens
            }
                    
        if not errorList.IsEmpty 
        then
            errorList <- List.rev errorList
            let tokenToString token = token |> parserSource.TokenToNumber |> parserSource.NumToString
            for i = 0 to errorList.Length-1 do
                printfn "Parse error in %A position in %A token. " <| errorList.[i].CurLvl <| tokenToString (errorList.[i].Tokens.[0].Token)
            //Error (errorIndexes.Head, errorTokenTypes.Head, "Parse error", debugFuns ())
        if !wasError 
        then 
            Error (!curInd , curTokens.Value.Tokens.[0].Token , "Parse Error", debugFuns ())
        else
            let root = ref None
            let addTreeTop res =
                let children = new Family(parserSource.StartRule,  new Nodes(res, null, null))
                new AST(children, null)
            for vertex in usedStates do
                if parserSource.AccStates.[vertex] 
                then
                    root :=
                        stateToVertex.[vertex].OutEdges.first.Ast
                        |> addTreeTop
                        |> Some
            match !root with
            | None -> Error (!curInd, curTokens.Value.Tokens.[0].Token, "Input was fully processed, but it's not complete correct string.", debugFuns ())
            | Some res -> 
            //    debugFuns().drawGSSDot "res.dot"
                Success <| new Tree<_>(tokens.ToArray() , res, parserSource.Rules)