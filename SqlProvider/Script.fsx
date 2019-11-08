#r @"Import2TSS.dll"
#r @"FSharp.Data.SqlProvider.dll"
#r @"FSharp.Control.AsyncSeq.dll"

#load "Library1.fs"
open SqlLib
open SqlDB
open System
open FSharp.Control
open System.IO

// Define your library scripting code here

// specific test case for  Sap P&L

//read P&L

try
    async {
        let cargoId = 21827
        let currency = "EUR"
        let db = DB()
        match! db.getSapPL cargoId currency with
        | Some pl ->
            printfn "Sap P&L: %A" pl
        | None ->
            printfn "%s Sap P&L not found for cargo %d" currency cargoId
    } |> Async.RunSynchronously
with
 | exc ->
     printfn "Error %s " (exc.ToString())

//write P&L

//let logFile = @"C:\compliance\Sap\dev\testSapPl.txt"

//let log (msg:string) = 
//    use sw = new StreamWriter(logFile, true)
//    sw.WriteLine msg
//    sw.Flush()

let action (res: Result<int * string, string * string>) = 
    //async  {
        match res with
        | Ok (i,m) -> 
            printfn "*** Ok %d %s *** " i m
            //log <| sprintf "*** Ok %d %s *** " i m
        | Error (m,e) -> 
            printfn "*** Error %s %s *** " m e
            //log <| sprintf "*** Error %s %s *** " m e
    //}

try
    async {
        let db = DB()
        let rows = [|
            { CargoID = 1; Currency = "EUR"; IsInternal = true; Amount = 223.12m};
            { CargoID = 1; Currency = "USD"; IsInternal = false; Amount = 245.89m};
        |]
        printfn "starting async seq"
        //do! AsyncSeq.iterAsync action (db.loadSapPL rows) 
        let! results = db.loadSapPL rows |> AsyncSeq.toListAsync
        results
        |> List.length
        |> printfn "result list contains items #: %d" 
        results
        |> List.iter action
        printfn "async seq ended"
    } |> Async.RunSynchronously
with
 | exc ->
     printfn "Error %s " (exc.ToString())



// specific test case for assigned alerts
try 
    async {
        let db = DB()
        let! assigned2me = db.findAssignedAlerts "userid00"
        printfn "Assigned to me: %d" assigned2me
    } |> Async.StartImmediate
with
 | exc ->
     printfn "Error %s " (exc.ToString())

// specific test case for manual alert update

let showRes title = function
    | Ok msg -> printfn "*** Result %s: ok %s ***" title msg
    | Error err -> printfn "*** Error %s: ko %s ***" title err

try 
    async {
        let db = DB()
        
        //let! check = 
        //    db.manualAlertUpdate 
        //        "Company - LE" 
        //        "101" "M01 Manual" 
        //        "102" "M03 Market Conformity" 
        //showRes "test update" check 
        
        let! check = 
            db.manualAlertUpdate 
                "Company - LE" 
                "102" "M03 Market Conformity" 
                "101" "M01 Manual" 
        showRes "test update" check 

    } |> Async.StartImmediate
with
 | exc ->
     printfn "Error %s " (exc.ToString())



try 
    async {
        let db = DB()

        let! check = 
            db.manualAlertUpdate 
                "Company - LE" 
                "1401618" "A82 Concl after" 
                "1401618" "A82 Concl after" 
        showRes "both auto" check 

        let! check = 
            db.manualAlertUpdate 
                "Company - LE" 
                "1401618" "A82 Concl after" 
                "1401618" "M01 Manual" 
        showRes "old auto" check 
        
        let! check = 
            db.manualAlertUpdate 
                "Company - LE" 
                "1401618" "M01 Manual"  
                "1401618" "A82 Concl after" 
        showRes "new auto" check 

    } |> Async.StartImmediate
with
 | exc ->
     printfn "Error %s " (exc.ToString())

try 
    async {
        let db = DB()

        let! check = 
            db.manualAlertUpdate 
                "Company - LE" 
                "101" "M01 Manual" 
                "101" "M01 Manual" 
        showRes "same keys" check 

        
        let! check = 
            db.manualAlertUpdate 
                "Company - LE" 
                "100" "M01 Manual" 
                "101" "M01 Manual" 
        showRes "new exists" check 

        
        let! check = 
            db.manualAlertUpdate 
                "Company - LE" 
                "102" "M01 Manual" 
                "100" "M01 Manual" 
        showRes "wrong old" check 

    } |> Async.StartImmediate
with
 | exc ->
     printfn "Error %s " (exc.ToString())
// specific test case for table cells with hover
try 
    async {
        let db = DB()
        let! check = db.trades (IntSel 1592194) "Company - LE"
        printfn "%A" (check |> Array.head)
    } |> Async.StartImmediate
with
 | exc ->
     printfn "Error %s " (exc.ToString())


// specific test case for bookcompany authorization
try 
    let db = DB()
    let check = db.userIdOfBookCompany "US_Company INC - LE" "userid00"
    printfn "auth result for ets inc: %b" check
    let check = db.userIdOfBookCompany "Company - LE" "userid00"
    printfn "auth result for ets spa: %b" check

with
 | exc ->
     printfn "Error %s " (exc.ToString())


// specific test case for https://github.com/giuliohome/TradingCompliance/issues/5
// sql lib not retrieving all the correct records for a specific cargo id?
async {
    try 
        let db = DB()
        let! costs = db.costs (IntSel 20052) "US_ETS INC - LE"
        printfn "number of cost records: %d" (costs |> Array.length)
        costs
        |> Array.iter (printfn "%A")
   
        printfn "Regression Test for costs"
        let! costs = db.costs NoSelection "US_ETS INC - LE"
        costs
        |> Array.take 10
        |> Array.iter (printfn "%A")
    with
     | exc ->
         printfn "Error %s " (exc.ToString())
} |> Async.StartImmediate

// complete test case
async {
try 
    let book = "Company - LE"
    let db = DB()
    //only ONLINE, no offline table at the moment
    // you may need  --define:OFFLINE in tools>options>f# tools> f# interactive (restart fsi)
    #if !OFFLINE
    printfn "Test trades"
    let! trades = db.trades NoSelection "Company - LE"
    trades
    |> Array.take 10
    |> Array.iter (printfn "%A")
    printfn "Test nominations"
    let! nominations = db.nominations NoSelection "Company - LE"
    nominations
    |> Array.take 10
    |> Array.iter (printfn "%A")    
    printfn "Test costs"
    let! costs = db.costs NoSelection "Company - LE"
    costs
    |> Array.take 10
    |> Array.iter (printfn "%A")
    #endif
    db.analysts book |> Array.iter (printfn "%A")
    let test = {UserName="usr"; Name="test"; Surname="me"}
    db.analystCreate book test
    Printf.printfn "***** read after insert ***** "
    db.analysts book |> Array.iter (printfn "%A")
    let upd = {UserName="usr"; Name="updated"; Surname="now"} 
    db.analystUpdate book upd
    Printf.printfn "*****  read after update ***** "
    db.analysts book |> Array.iter (printfn "%A")
    db.analystDelete book upd
    Printf.printfn "***** read after delete ***** "
    db.analysts book |> Array.iter (printfn "%A")

    #if !OFFLINE
    Printf.printfn "***** Let's test log import read now ***** "
    db.logsRead "Company - LE"
    |> Array.take 3
    |> Array.iter (printfn "%A")
    #endif
    Printf.printfn "***** Details for on a single log ***** "
    db.logDetailsRead "Company - LE" (DateTime(2019,6,27).Date)
    |> Array.iter (printfn "%A")

    //just one time
    db.cache()
with
 | exc ->
     printfn "Error %s " (exc.ToString())
} |> Async.StartImmediate

