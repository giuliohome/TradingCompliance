#I "bin/Debug"

#r @"Import2TSS.dll"
#r @"FSharp.Data.SqlProvider.dll"

#load "Library1.fs"
open SqlLib
open SqlDB
open System

// Define your library scripting code here

// specific test case for table cells with hover
try 
    async {
        let db = DB()
        let! check = db.trades (IntSel 1592194) "COMPANY1 - LE"
        printfn "%A" (check |> Array.head)
    } |> Async.StartImmediate
with
 | exc ->
     printfn "Error %s " (exc.ToString())


// specific test case for bookcompany authorization
try 
    let db = DB()
    let check = db.userIdOfBookCompany "COMPANY2 INC - LE" "user1"
    printfn "auth result for ets inc: %b" check
    let check = db.userIdOfBookCompany "COMPANY1 - LE" "user1"
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
    let book = "ENI - LE"
    let db = DB()
    //only ONLINE, no offline table at the moment
    // you may need  --define:OFFLINE in tools>options>f# tools> f# interactive (restart fsi)
    #if !OFFLINE
    printfn "Test trades"
    let! trades = db.trades NoSelection "ENI - LE"
    trades
    |> Array.take 10
    |> Array.iter (printfn "%A")
    printfn "Test nominations"
    let! nominations = db.nominations NoSelection "ENI - LE"
    nominations
    |> Array.take 10
    |> Array.iter (printfn "%A")    
    printfn "Test costs"
    let! costs = db.costs NoSelection "ENI - LE"
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
    db.logsRead "ENI - LE"
    |> Array.take 3
    |> Array.iter (printfn "%A")
    #endif
    Printf.printfn "***** Details for on a single log ***** "
    db.logDetailsRead "ENI - LE" (DateTime(2019,6,27).Date)
    |> Array.iter (printfn "%A")

    //just one time
    db.cache()
with
 | exc ->
     printfn "Error %s " (exc.ToString())
} |> Async.StartImmediate

