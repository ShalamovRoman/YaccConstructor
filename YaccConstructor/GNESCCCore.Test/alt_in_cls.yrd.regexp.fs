//this file was generated by GNESCC
//source grammar:../../../Tests/GNESCC/regexp/complex/alt_in_cls/alt_in_cls.yrd
//date:11/4/2011 2:49:31 PM

module GNESCC.Regexp_alt_in_cls

open Yard.Generators.GNESCCGenerator
open System.Text.RegularExpressions

let buildIndexMap kvLst =
    let ks = List.map (fun (x:string,y) -> x.Length + 2,y) kvLst
    List.fold (fun (bl,blst) (l,v) -> bl+l,((bl,v)::blst)) (0,[]) ks
    |> snd
    |> dict

let buildStr kvLst =
    let sep = ";;"
    List.map fst kvLst 
    |> String.concat sep
    |> fun s -> ";" + s + ";"

let s childsLst = 
    let str = buildStr childsLst
    let idxValMap = buildIndexMap childsLst
    let re = new Regex("(((((;5;))|((;6;))))*)")
    let elts =
        let res = re.Match(str)
        if Seq.fold (&&) true [for g in res.Groups -> g.Success]
        then res.Groups
        else (new Regex("(((((;5;))|((;6;))))*)",RegexOptions.RightToLeft)).Match(str).Groups
    let e0 =
        let ofset = ref 0
        let e i =
            let str = elts.[2].Captures.[i].Value
            let re = new Regex("(((;5;))|((;6;)))")
            let elts =
                let res = re.Match(str)
                if Seq.fold (&&) true [for g in res.Groups -> g.Success]
                then res.Groups
                else (new Regex("(((;5;))|((;6;)))",RegexOptions.RightToLeft)).Match(str).Groups
            let res =
                if elts.[3].Value = ""
                then
                    let e2 =
                        let e0 =
                            idxValMap.[!ofset + elts.[5].Captures.[0].Index] |> RELeaf
                        RESeq [e0]
                    None, Some (e2)
                else
                    let e1 =
                        let e0 =
                            idxValMap.[!ofset + elts.[3].Captures.[0].Index] |> RELeaf
                        RESeq [e0]
                    Some (e1),None
                |> REAlt

            ofset := !ofset + str.Length
            res
        REClosure [for i in [0..elts.[2].Captures.Count-1] -> e i]

    RESeq [e0]

let ruleToRegex = dict [|(1,s)|]
