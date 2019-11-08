namespace SqlLib

open Import2TSS
open FSharp.Data.Sql
open System
open System.Data
open System.Configuration
open FSharp.Control

module SqlDB =

    type Permission =
        static member OilWriteETS = "OilWriteETS"
    // aside from project properties for fsc build, you may need --define:OFFLINE in tools>options>f# tools> f# interactive (restart fsi) 
    #if OFFLINE 
    [<Literal>]
    let resolutionPath =  __SOURCE_DIRECTORY__ +  @"\bin\debug"
    [<Literal>]
    let myConnStr =  @"Data Source=" +   __SOURCE_DIRECTORY__ + @"/offline/offline_db.s3db;Version=3;" 
    type Sql = SqlDataProvider<
                DatabaseVendor = Common.DatabaseProviderTypes.SQLITE, 
                ConnectionString = myConnStr,
                ResolutionPath = resolutionPath // System.Data.SQLite.dll
                >
    #else
    [<Literal>]
    let mySchemaPath =
        __SOURCE_DIRECTORY__ + @"\.sqlserver.schema" // fixed misspelt .\
    [<Literal>]
    let myConnStr =    @"Data Source=;Initial Catalog=;User ID=;Password="
    type Sql = SqlDataProvider<
                DatabaseVendor = Common.DatabaseProviderTypes.MSSQLSERVER, 
                // ContextSchemaPath = mySchemaPath, // file created the 1st time you build the proj with the schema path
                // and ctx.SaveContextSchema() just compiled in the code
                // but then you need to comment it to put the provider online again
                ConnectionString = myConnStr
                >

    #endif
    #if INTERACTIVE
    FSharp.Data.Sql.Common.QueryEvents.SqlQueryEvent 
    |> Event.add (fun e -> 
        printfn  "Executing SQL: %s" (e.ToRawSql()))
    #endif

    let AppDB = "TSS_DB"
    #if INTERACTIVE 
    #if OFFLINE 
    let context = Sql.GetDataContext(myConnStr, resolutionPath)
    #else
    let context = Sql.GetDataContext(myConnStr)
    #endif
    #else
    #if OFFLINE 
    let context = Sql.GetDataContext(myConnStr, resolutionPath)
    #else
    let context = Sql.GetDataContext(ConfigurationManager.ConnectionStrings.[AppDB].ConnectionString)
    #endif
    #endif

    #if OFFLINE
    let analystsTable = context.Main.Analyst
    #else 
    let analystsTable = context.OilPhysical.Analyst
    #endif
    
    type ExtTrade = {t:TradeValid; n: NominationValid}

    type ParsedTrade = NoSelection | IntSel of int  
    [<Literal>]
    let ByCargo = "ByCargo"
    [<Literal>]
    let ByTrade = "ByTrade"
    [<Literal>]
    let ByCpty = "ByCpty"
    [<Literal>]
    let ByFee = "ByFee"
    let alertOfType (byType:string) (alert_type:string) =
        match byType with
        | x when x = ByTrade -> alert_type = ByTrade
        | x when x = ByFee -> alert_type = ByFee
        | x when x = ByCpty -> alert_type = ByCpty
        | x when x = ByCargo -> alert_type = ByCargo || (String.IsNullOrWhiteSpace alert_type)
        | _ -> failwith (byType + " is not an alert type")
    #if OFFLINE 
    // TODO: "only ONLINE, no offline table at the moment"
    #else 
    let tradesQuery (book:string) (alert_type:string) (sel: ParsedTrade) = 
        let isByTrade = alertOfType ByTrade alert_type
        let isByFee = alertOfType ByFee alert_type
        let isByCpty = alertOfType ByCpty alert_type
        let isByCargo = alertOfType ByCargo alert_type
        match sel with
        | NoSelection -> 
            query {
            for t in context.OilPhysical.EndurTradeValid do  
            join n_del in (!!) context.OilPhysical.EndurNominationValid on
                     ((t.DealNumber,t.ParcelGroupId,t.ParcelId) 
                     = (n_del.DeliveryDealNumber,n_del.DeliveryParcelGroup,n_del.DeliveryParcelId))
            join n_rec in (!!) context.OilPhysical.EndurNominationValid on
                     ((t.DealNumber,t.ParcelGroupId,t.ParcelId) 
                     = (n_rec.ReceiptDealNumber,n_rec.ReceiptParcelGroup,n_rec.ReceiptParcelId))
            join id_cost in (!!) context.OilPhysical.EndurProfitCenter on 
                    (t.CostCenterId = id_cost.InternalId)
            join id_profit in (!!) context.OilPhysical.EndurProfitCenter on 
                    (t.ProfitCenterId = id_profit.InternalId)
            where (t.BookingCompanyShortName = book)
            take 90
            select (t,n_del.CargoId, n_rec.CargoId, id_cost.EndurId, id_profit.EndurId)  
            }
        | IntSel i -> 
            query {
            for t in context.OilPhysical.EndurTradeValid do  
            join n_del in (!!) context.OilPhysical.EndurNominationValid on
                     ((t.DealNumber,t.ParcelGroupId,t.ParcelId) 
                     = (n_del.DeliveryDealNumber,n_del.DeliveryParcelGroup,n_del.DeliveryParcelId))
            join n_rec in (!!) context.OilPhysical.EndurNominationValid on
                     ((t.DealNumber,t.ParcelGroupId,t.ParcelId) 
                     = (n_rec.ReceiptDealNumber,n_rec.ReceiptParcelGroup,n_rec.ReceiptParcelId))
            join id_cost in (!!) context.OilPhysical.EndurProfitCenter on 
                    (t.CostCenterId = id_cost.InternalId)
            join id_profit in (!!) context.OilPhysical.EndurProfitCenter on 
                    (t.ProfitCenterId = id_profit.InternalId)
            where (t.BookingCompanyShortName = book 
                && ((isByTrade && t.DealNumber = i) || 
                    (isByCargo && n_del.CargoId = i) || 
                    (isByCargo && n_rec.CargoId = i) || 
                    (isByCpty && t.ExternalLegalEntityId = (string i)) ))
            take 90
            select (t,n_del.CargoId, n_rec.CargoId, id_cost.EndurId, id_profit.EndurId)  
            }
    
    let nominationsQuery (book:string) (alert_type:string) (sel: ParsedTrade) =
        let isByTrade = alertOfType ByTrade alert_type
        let isByFee = alertOfType ByFee alert_type
        let isByCpty = alertOfType ByCpty alert_type
        let isByCargo = alertOfType ByCargo alert_type
        match sel with
        | NoSelection -> 
            query {
                for n in context.OilPhysical.EndurNominationValid do
                where (n.BookingCompany = book)
                take 90
                sortByDescending n.TitleTranfertDate
                select n
            }
        | IntSel i -> 
            query {
                for n in context.OilPhysical.EndurNominationValid do
                where (n.BookingCompany = book && (
                        (isByCpty && n.DeliveryExternalLegalEntityId = i) ||
                        (isByCargo && n.CargoId = i) ||
                        (isByTrade && n.DeliveryDealNumber = i) ||
                        (isByTrade && n.ReceiptDealNumber = i)
                    ))
                take 90
                sortByDescending n.TitleTranfertDate
                select n
            }
    
    let costsQuery (book:string) (alert_type:string) (sel: ParsedTrade) =
        let isByTrade = alertOfType ByTrade alert_type
        let isByFee = alertOfType ByFee alert_type
        let isByCpty = alertOfType ByCpty alert_type
        let isByCargo = alertOfType ByCargo alert_type
        let delete_type = Cost.Operations.Delete.GetEnumDescription()
        match sel with
        | NoSelection -> 
            query {
                for c in context.OilPhysical.EndurCost do
                join n in context.OilPhysical.EndurNominationValid
                    // fix with latest version (1.1.68) of SQLProvider https://github.com/fsprojects/SQLProvider/issues/634#issuecomment-529852061
                    on ( (c.CargoId, c.DeliveryId, c.DealNumber) = (n.CargoId, n.DeliveryId, if n.DeliveryDealNumber>0 then n.DeliveryDealNumber else n.ReceiptDealNumber) )
                where (c.BookingCompany = book &&
                    c.FeeStatus <> Cost.ClosedFeeStatus &&
                    c.FeeType <> delete_type
                    )
                take 90
                select c
            }
        | IntSel i -> 
            let i_str = string i
            query {
                for c in context.OilPhysical.EndurCost do
                join n in context.OilPhysical.EndurNominationValid
                    // fix with latest version (1.1.68) of SQLProvider https://github.com/fsprojects/SQLProvider/issues/634#issuecomment-529852061
                    on ( (c.CargoId, c.DeliveryId, c.DealNumber) = (n.CargoId, n.DeliveryId, if n.DeliveryDealNumber>0 then n.DeliveryDealNumber else n.ReceiptDealNumber) )
                where (c.BookingCompany = book &&
                    c.FeeStatus <> Cost.ClosedFeeStatus &&
                    c.FeeType <> delete_type &&
                        ( (isByCpty && c.CounterpartyId = i_str) ||
                          (isByFee && c.FeeId = i) || (isByCargo && c.CargoId = i) )
                    )
                take 90
                select c
            }


    #endif 
    
    type BookLog = {AsofDate: DateTime; BookCompany: string; 
        StartDate:DateTime; EndDate: DateTime; Status: string }

    let evalLogStatus = function
        | i when i = 2 -> "Success"
        | i when i = 0 -> "Working"
        | _ -> "Fail" //should be 1
        
    #if OFFLINE 
    // TODO: "only ONLINE, no offline table at the moment"
    #else 
    let ReadImportLog (book:string) =
        query {
            for l in context.OilPhysical.ImportBookLog do
            where (l.BookCompany = book)
            sortByDescending l.AsofDate 
            select { AsofDate = l.AsofDate; BookCompany = l.BookCompany; 
                StartDate = l.StartDate; EndDate = l.EndDate; 
                Status = evalLogStatus l.Status}
        }
    #endif

    type LogLine = { Num: int; Text: string}
    #if OFFLINE 
    // TODO: "only ONLINE, no offline table at the moment"
    #else 
    let ReadLogRow (book:string) (asof: DateTime)  =
        query {
            for r in context.OilPhysical.ImportBookLogRow do
            where (r.BookCompany = book && r.AsofDate = asof)
            sortBy r.RowId
            select { Num = r.RowId; Text = r.Message }
        }
    #endif

    let ofBookCompany (book:string) (username:string)  =
        query {
            for a in analystsTable do
            exists (a.BookCompany = book && a.UserName = username)
        }

    type Analyst = { UserName: string; Name:string; Surname: string}
    let analystsQuery (book:string) =
        query {
        for a in analystsTable do
        where (a.BookCompany = book)
        sortBy a.Surname
        select { UserName = a.UserName; Name = a.Name; Surname = a.Surname }
        }
    let analystInsert (book:string) (analyst:Analyst) =
        let insertMe = analystsTable.Create()
        insertMe.BookCompany <- book
        insertMe.UserName <- analyst.UserName
        insertMe.Name <- analyst.Name
        insertMe.Surname <- analyst.Surname
        context.SubmitUpdates()
    let analystUpdate (book:string) (analyst:Analyst) =
        let foundAnalystMaybe = query {
            for a in analystsTable do
            where (a.BookCompany = book && a.UserName = analyst.UserName)
            select (Some a)
            exactlyOneOrDefault
        }
        match foundAnalystMaybe with
        | Some foundAnalyst -> 
            foundAnalyst.Name <- analyst.Name
            foundAnalyst.Surname <- analyst.Surname
            context.SubmitUpdates()
        | None -> ()
    let analystDelete (book:string) (analyst:Analyst) =
        let foundAnalystMaybe = query {
            for a in analystsTable do
            where (a.BookCompany = book && a.UserName = analyst.UserName)
            select (Some a)
            exactlyOneOrDefault
        }
        match foundAnalystMaybe with
        | Some foundAnalyst -> 
            foundAnalyst.Delete()
            context.SubmitUpdates()
        | None -> ()
    
    type RowSapPL = { CargoID: int; Currency: string; IsInternal: bool; Amount: decimal}
    
    let emptySapPl () =
        asyncSeq {
            try
                let! dels =   
                    query {
                     for pl in context.OilPhysical.SapPl do 
                     where (true)
                    } |> Seq.``delete all items from single table``
                yield Ok (dels, "deleted")
            with 
            | exc -> 
                yield Error (exc.Message, "emptySapPl failed")
        }

    let loadSapPL (rows:RowSapPL[]) : AsyncSeq<Result<int * string, string * string>> = 
        asyncSeq {
            try
                rows
                |> Array.iter( fun r ->
                    context.OilPhysical.SapPl.``Create(Amount, CargoID, Currency, InternalCurr)`` (r.Amount, r.CargoID, r.Currency, r.IsInternal) |> ignore
                )
                do! context.SubmitUpdatesAsync()
                yield Ok (rows |> Array.length, "inserted")
            with
            | exc ->
                yield Error (exc.Message, "loadSapPL failed") 
        }

    let cargoPL cargoId currency = 
        let query =
            query {
                for p in context.OilPhysical.SapPl do
                where (p.CargoId = cargoId && p.Currency = currency)
                select p
            }
        async {
            return! query |> Seq.tryHeadAsync
        }

    let findAssignedAlerts (username:string) =
        async {
            let countAssigned = 
                query {
                    for a in context.OilPhysical.Alert do
                    where (a.AssignedTo = username && a.Status = Alerting.Assigned)
                    
                }
            return! countAssigned |> Seq.lengthAsync
        }

    let manualAlertUpdate (bookingComp: string) 
        (oldKey: string) (oldCode: string) 
        (newKey: string) (newCode: string) : Async<Result<string, string>> =
        async {
            if (not(oldCode.StartsWith("M")) || not(newCode.StartsWith("M"))) then
                return Error "you can update only Manual alert keys"
            else if (oldKey = newKey && oldCode = newCode) then
                return Error "nothing to update"
            else
                let existsNewAudit = query {
                    for a in context.OilPhysical.AuditAlert do
                    where (
                        a.BookCompany = bookingComp &&
                        a.AlertCode = newCode &&
                        a.AlertKey = newKey
                    ) 
                    select a
                }
                let! existsNewAudMaybe = existsNewAudit |> Seq.tryHeadAsync
                match existsNewAudMaybe with
                | Some newaud -> return Error ("Already exists the new key audit asof " + newaud.TransactionDate.ToShortDateString())
                | None ->
                    let existsNewAlert = query {
                        for a in context.OilPhysical.Alert do
                        where (
                            a.BookCompany = bookingComp &&
                            a.AlertCode = newCode &&
                            a.AlertKey = newKey
                        ) 
                        select (Some a)
                        exactlyOneOrDefault
                    }
                    match existsNewAlert with
                    | Some newAlert ->
                        return Error ("Already exists the new alert: " + newCode + "-" + newKey)
                    | None ->
                        let alertFoundMaybe = query {
                            for a in context.OilPhysical.Alert do
                            where (
                                a.BookCompany = bookingComp &&
                                a.AlertCode = oldCode &&
                                a.AlertKey = oldKey
                            )
                            select (Some a)
                            exactlyOneOrDefault
                        }
                        match alertFoundMaybe with
                        | None ->
                            return Error ("old keys not found: " + oldCode + "-" + oldKey)
                        | Some oldAlert -> 
                            let updatedAlert = 
                                context.OilPhysical.Alert.Create(
                                    oldAlert.ColumnValues
                                    |> Seq.map(fun (k,v) ->
                                        match k with
                                        | "AlertCode" -> (k, newCode :> obj)
                                        | "AlertKey" -> (k, newKey :> obj)
                                        | _ ->(k, if v = null then (DBNull.Value :> obj) else v)
                                    )
                                )
                            do! context.SubmitUpdatesAsync()
                            let! existsOldAudit = 
                                query {
                                    for a in context.OilPhysical.AuditAlert do
                                    where (
                                        a.BookCompany = bookingComp &&
                                        a.AlertCode = oldCode &&
                                        a.AlertKey = oldKey
                                    ) 
                                    select a
                                } |> Array.executeQueryAsync
                            let audNum = existsOldAudit |> Array.length
                            let mutable loop_err = ""
                            let mutable loop_ok = ""
                            for oldAuditRec in existsOldAudit do
                                loop_ok <- loop_ok + "looping;"
                                let newAudRec = 
                                    context.OilPhysical.AuditAlert.Create(
                                        oldAuditRec.ColumnValues
                                        |> Seq.map(fun (k,v) ->
                                            match k with
                                            | "AlertCode" -> (k, newCode :> obj)
                                            | "AlertKey" -> (k, newKey :> obj)
                                            | _ ->(k, if v = null then (DBNull.Value :> obj) else v)
                                        )
                                    )
                                let curr_ok = newAudRec.AlertKey + "-" + newAudRec.AlertKey
                                let! upd = 
                                    context.SubmitUpdatesAsync()
                                    |> Async.Catch 
                                    |> fun x -> async {
                                        match! x with
                                        | Choice1Of2 _  -> return Ok ""
                                        | Choice2Of2 exc ->
                                            return Error exc.Message
                                        }
                                match upd with
                                | Ok _ ->
                                    oldAuditRec.Delete()
                                    do! context.SubmitUpdatesAsync()
                                    loop_ok <- loop_ok + curr_ok + ";"
                                | Error err ->
                                    loop_err <- loop_err + err + ";"
                                    
                            
                            oldAlert.Delete()
                            do! context.SubmitUpdatesAsync()
                            return Ok ("updated from " + oldCode + "-" + oldKey 
                                + " to " + newCode + "-" + newKey
                                + " with " + (string audNum) + " audit" 
                                + if loop_err = "" then  " ok: " + loop_ok else " error: " + loop_err)
        }
            
        

    type DBTable = (string * obj) array array
    type SqlHoverCell = { Hover: string; Base: string}
    let addHover (key:string) (value:string) (cols: seq<string * obj>) =
        cols
        |> Seq.map (fun (k, v) ->
            if k = key then
                ( key, 
                    { Base = 
                        cols
                        |> Seq.tryFind (fun (k, _) -> k = key)
                        |> Option.fold (fun _ (_,v) -> string v ) ""; 
                      Hover = value } :> obj)
            else (k, v)
        )

    
    type DB() =
        member x.loadSapPL (rows:RowSapPL[])  : AsyncSeq<Result<int * string, string * string>> =  
            asyncSeq {
                yield! emptySapPl()
                let chunks = rows |> Array.chunkBySize 500
                for chunk in chunks do
                    yield! loadSapPL chunk
            }
        
        member x.getSapPL cargoId currency = 
            async {
                let! pl = cargoPL cargoId currency
                return pl 
                |> Option.map( fun r ->
                    { CargoID = r.CargoId; Currency = r.Currency; Amount = r.Amount; IsInternal = r.InternalCurr}
                )
            }
        

        member x.findAssignedAlerts (username:string) = 
            findAssignedAlerts username

        member x.manualAlertUpdate (bookingComp: string) 
            (oldKey: string) (oldCode: string) 
            (newKey: string) (newCode: string) = 
            manualAlertUpdate bookingComp oldKey oldCode newKey newCode

        member x.trades (sel: ParsedTrade) (alert_type:string) (book: string) : Async<DBTable> = async {
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            return [||]
        #else
            let! queryRes = tradesQuery book alert_type sel |> Array.executeQueryAsync 
            return queryRes
            |> Array.map(fun (t,n_del,n_rec, cost, profit) -> 
                t.ColumnValues
                |> Seq.filter (fun (k,_) -> 
                    k.Contains("].[") |> not)
                |> addHover "CostCenterID" cost
                |> addHover "ProfitCenterID" profit
                |> Seq.append ([("Del Cargo ID", n_del :> obj)] 
                |> Seq.append [("Rec Cargo ID", n_rec :> obj)] ) 
                |> Seq.toArray)
        #endif
        } 

        member x.nominations  (sel: ParsedTrade) (alert_type:string) (book: string) : Async<DBTable> = async {
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            return [||]
        #else
            let! queryRes = nominationsQuery book alert_type sel |> Array.executeQueryAsync  
            return queryRes
            |> Array.map(fun n -> 
                n.ColumnValues
                |> Seq.toArray)
        #endif
        }
        
        member x.costs  (sel: ParsedTrade) (alert_type:string) (book: string) : Async<DBTable> = async {
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            [||]
        #else
            let! queryRes = costsQuery book alert_type sel |> Array.executeQueryAsync  
            return queryRes
            |> Array.map(fun n -> 
                n.ColumnValues
                |> Seq.toArray)
        #endif
        }
        
        member x.logsRead (book:string) : BookLog[] =
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            [||]
        #else
            ReadImportLog book |> Seq.toArray
        #endif

        member x.logDetailsRead (book:string) (asof:DateTime) : LogLine[] =
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            [||]
        #else
            ReadLogRow book asof |> Seq.toArray
        #endif

        member x.analysts (book:string) =
            analystsQuery book |> Seq.toArray
        member x.analystCreate (book:string) (analyst:Analyst) =
            analystInsert book analyst
        member x.analystUpdate (book:string) (analyst:Analyst) =
            analystUpdate book analyst
        member x.analystDelete (book:string) (analyst:Analyst) =
            analystDelete book analyst

        member x.userIdOfBookCompany = ofBookCompany

        member x.cache() =
            #if COMPILED 
            printfn "you are in compiled mode! You can set OFFLINE from project properties for fsc build" 
            #else 
            #if INTERACTIVE
            printfn "you are in fsi interactive mode! You may need --define:OFFLINE in tools>options>f# tools> f# interactive (restart fsi)" 
            #else
            printfn "your compiler mode is unknown!"
            #endif
            #endif 

            #if OFFLINE
            printfn "you are offline"
            #else 
            printfn "you are online"
            // file created the 1st time you build the proj with the schema path and ctx.SaveContextSchema() just compiled in the code
            // this seems to be a design/compile time (not runtime) feature
            let result = context.SaveContextSchema()
            printfn "check schema under %s " mySchemaPath
            if (result <> null) then
                printfn "offline-first is ok now!"
                use sw = new System.IO.StreamWriter(mySchemaPath + ".txt", true)
                sw.WriteLine(sprintf "%s %A" (DateTime.Now.ToShortTimeString()) result)
            #endif 



