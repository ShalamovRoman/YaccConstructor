//this file was generated by GNESCC
//source grammar:../../../Tests/GNESCC/action_code/checkers/simple_checker/simple_checker.yrd
//date:11/4/2011 2:49:29 PM

module GNESCC.Actions_simple_checker

open Yard.Generators.GNESCCGenerator

let getUnmatched x expectedType =
    "Unexpected type of node\nType " + x.ToString() + " is not expected in this position\n" + expectedType + " was expected." |> failwith

let value x = (x:>Lexer_simple_checker.MyLexeme).MValue

type OpType =
   | Undef
   | Mult
   | Plus
   | Minus

let s0 expr = 
    let inner  = 
        match expr with
        | RESeq [x0] -> 
            let (res:int) =
                let yardElemAction expr = 
                    match expr with
                    | RELeaf e -> (e :?> _ )Undef 
                    | x -> getUnmatched x "RELeaf"

                yardElemAction(x0)
            (res)
        | x -> getUnmatched x "RESeq"
    box (inner)
let e1 expr = 
    let inner (prevOp) = 
        match expr with
        | REAlt(Some(x), None) -> 
            let yardLAltAction expr = 
                match expr with
                | RESeq [x0] -> 
                    let (n) =
                        let yardElemAction expr = 
                            match expr with
                            | RELeaf tNUMBER -> tNUMBER :?> 'a
                            | x -> getUnmatched x "RELeaf"

                        yardElemAction(x0)
                    (value n |> int)
                | x -> getUnmatched x "RESeq"

            yardLAltAction x 
        | REAlt(None, Some(x)) -> 
            let yardRAltAction expr = 
                match expr with
                | RESeq [x0; x1; x2] -> 
                    let (l) =
                        let yardElemAction expr = 
                            match expr with
                            | RELeaf e -> (e :?> _ )Undef 
                            | x -> getUnmatched x "RELeaf"

                        yardElemAction(x0)
                    let (op,opType) =
                        let yardElemAction expr = 
                            match expr with
                            | REAlt(Some(x), None) -> 
                                let yardLAltAction expr = 
                                    match expr with
                                    | RESeq [gnescc_x0] -> 
                                        let (gnescc_x0) =
                                            let yardElemAction expr = 
                                                match expr with
                                                | RELeaf tPLUS -> tPLUS :?> 'a
                                                | x -> getUnmatched x "RELeaf"

                                            yardElemAction(gnescc_x0)
                                        ( (+),Plus )
                                    | x -> getUnmatched x "RESeq"

                                yardLAltAction x 
                            | REAlt(None, Some(x)) -> 
                                let yardRAltAction expr = 
                                    match expr with
                                    | REAlt(Some(x), None) -> 
                                        let yardLAltAction expr = 
                                            match expr with
                                            | RESeq [gnescc_x0] -> 
                                                let (gnescc_x0) =
                                                    let yardElemAction expr = 
                                                        match expr with
                                                        | RELeaf tMULT -> tMULT :?> 'a
                                                        | x -> getUnmatched x "RELeaf"

                                                    yardElemAction(gnescc_x0)
                                                ( ( * ),Mult )
                                            | x -> getUnmatched x "RESeq"

                                        yardLAltAction x 
                                    | REAlt(None, Some(x)) -> 
                                        let yardRAltAction expr = 
                                            match expr with
                                            | RESeq [gnescc_x0] -> 
                                                let (gnescc_x0) =
                                                    let yardElemAction expr = 
                                                        match expr with
                                                        | RELeaf tMINUS -> tMINUS :?> 'a
                                                        | x -> getUnmatched x "RELeaf"

                                                    yardElemAction(gnescc_x0)
                                                ( (-),Minus )
                                            | x -> getUnmatched x "RESeq"

                                        yardRAltAction x 
                                    | x -> getUnmatched x "REAlt"

                                yardRAltAction x 
                            | x -> getUnmatched x "REAlt"

                        yardElemAction(x1)
                    if not (match prevOp,opType with | Undef,_ | _,Mult | ((Plus|Minus),(Plus|Minus)) -> true | _,_ -> false) then raise Constants.CheckerFalse

                    let (r) =
                        let yardElemAction expr = 
                            match expr with
                            | RELeaf e -> (e :?> _ )opType 
                            | x -> getUnmatched x "RELeaf"

                        yardElemAction(x2)
                    (op l r)
                | x -> getUnmatched x "RESeq"

            yardRAltAction x 
        | x -> getUnmatched x "REAlt"
    box (inner)

let ruleToAction = dict [|(2,e1); (1,s0)|]

